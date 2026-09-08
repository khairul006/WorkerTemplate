using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;

namespace WorkerTemplate.Providers;

public class RabbitMQService : IRabbitMQService
{
    private readonly ILogger<RabbitMQService> _logger;
    private readonly RabbitMQSettings _settings;

    // Mapping a unique connection per target broker cluster/vhost name
    private readonly ConcurrentDictionary<string, IConnection> _connections = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _connectLocks = new();

    // Warm pool of idle channels segregated by broker target for publishing
    private readonly ConcurrentDictionary<string, ConcurrentQueue<IChannel>> _channelPools = new();
    // Prevent unbounded channel growth (tune this based on maximum expected parallel workers)
    private const int MaxPoolSize = 50;

    public RabbitMQService(
    IOptions<RabbitMQSettings> settingsOptions, // Renamed parameter to avoid variable shadowing
    ILogger<RabbitMQService> logger
)
    {
        _logger = logger;
        _settings = settingsOptions.Value; // Extract the underlying options object
    }

    private SemaphoreSlim GetConnectLock(string key) =>
        _connectLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));


    public async Task<IConnection> GetConnectionAsync(
        BrokerSettings config,
        string brokerName = "default")
    {
        if (_connections.TryGetValue(brokerName, out var existing) && existing.IsOpen)
            return existing;

        var gate = GetConnectLock(brokerName);
        await gate.WaitAsync();
        try
        {
            if (_connections.TryGetValue(brokerName, out existing) && existing.IsOpen)
                return existing;

            var factory = new ConnectionFactory
            {
                HostName = config.Host,
                Port = config.Port,
                VirtualHost = config.VirtualHost,
                UserName = config.Username,
                Password = config.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30), // default is often too short across slower networks
                SocketReadTimeout = TimeSpan.FromSeconds(30),
                SocketWriteTimeout = TimeSpan.FromSeconds(30),
            };

            if (config.Protocol.Equals("amqps", StringComparison.OrdinalIgnoreCase))
            {
                factory.Ssl.Enabled = true;
                factory.Ssl.AcceptablePolicyErrors =
                    System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors |
                    System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch;
                factory.Ssl.ServerName = config.Host;

                factory.Ssl.Version = SslProtocols.Tls12;
            }

            var connection = await factory.CreateConnectionAsync($"broker-{brokerName}");
            _connections[brokerName] = connection;
            _logger.LogInformation("RabbitMQ connected (name={name}, protocol={Protocol}, host={Host}, port={Port}, vhost={VirtualHost})",
                brokerName, config.Protocol, config.Host, config.Port, config.VirtualHost);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish RabbitMQ connection (name={name}, protocol={Protocol}, host={Host}, port={Port}, vhost={VirtualHost})",
                brokerName, config.Protocol, config.Host, config.Port, config.VirtualHost);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }


    public async Task StartConsumingAsync(
        BrokerSettings connConfig,
        QueueConfig queueConfig,
        Func<string, int, Task<RabbitmqHandlerResult>> handler,
        string brokerName = "default",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get shared long-lived connection for this broker
            var connection = await GetConnectionAsync(connConfig, brokerName);

            // Open a isolated private channel for THIS consumer instance
            var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: (ushort)queueConfig.Prefetch, global: false, cancellationToken: cancellationToken); // check prefetch

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                    _logger.LogInformation("Consumed json message: {msg}", msg);

                    // Perform quick validation syntax checks
                    try { using var doc = JsonDocument.Parse(msg); }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Malformed JSON dropped: {msg}", msg);
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                        return;
                    }

                    int retryCount = GetRetryCount(ea);
                    var result = await handler(msg, retryCount);

                    switch (result.Action)
                    {
                        case MessageAction.Ack:
                            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                            return;
                        case MessageAction.Retry:
                            if (result.RetryDelayMs == null)
                            {
                                _logger.LogWarning("Retry action requested but no RetryDelayMs provided. Requeue to the main queue.");
                                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                                return;
                            }
                            await HandleRetryAsync(channel, queueConfig.QueueName, ea, retryCount, result.RetryDelayMs.Value);
                            return;
                        case MessageAction.Nack:
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                            return;
                        case MessageAction.Dead:
                            _logger.LogWarning("Failed to process message. Dropping message. Msg={msg}", msg);
                            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                            return;
                        default:
                            _logger.LogError("Unknown handler action: {action}", result.Action);
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                            return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling RabbitMQ consume message");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            // Start reading. The method completes immediately while the consumer runs continuously in background.
            await channel.BasicConsumeAsync(queue: queueConfig.QueueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
            _logger.LogInformation("Started consuming from RabbitMQ. Broker={name}, Queue={q}", brokerName, queueConfig.QueueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed consuming from RabbitMQ: Broker={name}, Queue={q}", brokerName, queueConfig.QueueName);
            throw;
        }
    }

    public async Task<bool> PublishAsync(
        ExchangeConfig exchangeConfig,
        string message,
        string brokerName = "default",
        CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(brokerName, out var connection) || !connection.IsOpen)
            throw new InvalidOperationException($"No active connection initialized for broker: {brokerName}");

        var pool = _channelPools.GetOrAdd(brokerName, _ => new ConcurrentQueue<IChannel>());
        IChannel? channel = null;

        // Acquire or Create Channel Safely
        while (pool.TryDequeue(out var extractedChannel))
        {
            if (extractedChannel.IsOpen)
            {
                channel = extractedChannel;
                break;
            }
            // Explicitly dispose dead channels to avoid leaks
            await TryDisposeChannelAsync(extractedChannel);
        }

        if (channel == null)
        {
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            );
            channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
        }

        // Setup Short-Lived Timeout for High Throughput
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var body = Encoding.UTF8.GetBytes(message);

            // OPTIMIZATION: v7 prefers direct properties modification via BasicProperties implementation
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            // Execution blocks asynchronously until ACK/NACK arrives due to publisher confirmations
            await channel.BasicPublishAsync(
                exchange: exchangeConfig.ExchangeName,
                routingKey: exchangeConfig.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: linkedCts.Token
            );

            _logger.LogInformation("Published json message. Broker={broker} Ex={exchange} RK={routingKey} Msg={msg}", brokerName, exchangeConfig.ExchangeName, exchangeConfig.RoutingKey, message);

            // Return to pool only if healthy and pool isn't oversized
            if (channel.IsOpen && pool.Count < MaxPoolSize)
                pool.Enqueue(channel);
            else
                await TryDisposeChannelAsync(channel);

            return true;
        }
        catch (RabbitMQ.Client.Exceptions.PublishException pubEx)
        {
            // Broker rejected message (e.g., unroutable & mandatory flag set)
            _logger.LogError(pubEx, "Message rejected or unroutable by broker. Broker={Publisher} Route:{route}", brokerName, exchangeConfig);
            await TryDisposeChannelAsync(channel);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Publication timed out waiting for ACK. Broker={Publisher} Route:{route}", brokerName, exchangeConfig);
            await TryDisposeChannelAsync(channel);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message. Broker={Publisher} Route:{route}", brokerName, exchangeConfig);
            await TryDisposeChannelAsync(channel);
            throw;
        }
    }

    private async Task TryDisposeChannelAsync(IChannel? channel)
    {
        if (channel is null) return;
        try
        {
            await channel.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to smoothly dispose RabbitMQ channel.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            try
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to cleanly tear down connection."); }
        }

        foreach (var gate in _connectLocks.Values)
            gate.Dispose();

        _connections.Clear();
        _connectLocks.Clear();
    }

    public async Task EnsureRetryQueuesAsync(
        string baseQueue,
        IEnumerable<int> delayIntervalsMs,
        string brokerName = "default")
    {
        if (!_connections.TryGetValue(brokerName, out var connection) || !connection.IsOpen)
            throw new InvalidOperationException($"No open connection for consumer '{brokerName}'. Call ConnectConsumer first.");

        await using var channel = await connection.CreateChannelAsync();

        foreach (var ms in delayIntervalsMs.Distinct())
            await CreateRetryQueueAsync(channel, baseQueue, ms);
    }

    // -----------------------------------------------------------------------------------------------------
    // @ Private methods
    // -----------------------------------------------------------------------------------------------------

    private static string ToDelayName(int ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);

        if (ts.TotalHours >= 1)
            return $"{ts.TotalHours:0.#}h";
        if (ts.TotalMinutes >= 1)
            return $"{ts.TotalMinutes:0.#}m";
        if (ts.TotalSeconds >= 1)
            return $"{ts.TotalSeconds:0.#}s";

        return $"{ms}ms";
    }


    private async Task CreateRetryQueueAsync(IChannel channel, string baseQueue, int ttlMs)
    {
        try
        {
            if (channel == null)
                throw new InvalidOperationException("RabbitMQ consumer channel not initialized.");

            var queueName = $"{baseQueue}.retry.{ToDelayName(ttlMs)}";

            // Dictionary arguments now accept object values directly, method call is async
            var arguments = new Dictionary<string, object?>
            {
                { "x-message-ttl", ttlMs },
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", baseQueue }
            };

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments
            );

            _logger.LogInformation("Retry queue ensured: {queue}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed create retry queues");
        }
    }

    private async Task HandleRetryAsync(IChannel channel, string baseQueue, BasicDeliverEventArgs ea, int currentRetryCount, int delayMs)
    {
        try
        {
            var delayQueue = $"{baseQueue}.retry.{ToDelayName(delayMs)}";

            var headers = ea.BasicProperties?.Headers != null
                ? new Dictionary<string, object?>(ea.BasicProperties.Headers)
                : new Dictionary<string, object?>();

            headers["retry-count"] = currentRetryCount + 1;

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = ea.BasicProperties?.ContentType,
                CorrelationId = ea.BasicProperties?.CorrelationId,
                Headers = headers
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: delayQueue,
                mandatory: true,
                basicProperties: properties,
                body: ea.Body.ToArray()
            );

            await channel.BasicAckAsync(ea.DeliveryTag, false);
            _logger.LogInformation("Retrying message. RetryCount={retry}; Queue={queue}", currentRetryCount, delayQueue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handle retry message. Queue={queue}", baseQueue);
            await channel.BasicNackAsync(ea.DeliveryTag, false, true);
        }
    }

    private int GetRetryCount(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties?.Headers == null)
            return 0;

        if (ea.BasicProperties.Headers.TryGetValue("retry-count", out var value) && value != null)
        {
            return Convert.ToInt32(value);
        }

        return 0;
    }

}