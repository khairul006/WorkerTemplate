using Microsoft.Extensions.Options;
using System.Runtime;
using System.Text.Json;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;
using WorkerTemplate.Providers;
using WorkerTemplate.Services;

namespace WorkerTemplate.Workers;

public class ClientWorker : BackgroundService
{
    private readonly ILogger<ClientWorker> _logger;
    private readonly IRabbitMQService _rabbitMQService;
    private readonly IRedisService _redisService;
    private readonly RabbitMQSettings _queueSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public ClientWorker(
        ILogger<ClientWorker> logger,
        IRabbitMQService rabbitMQService,
        IRedisService redisService,
        IOptions<RabbitMQSettings> options,
        IServiceScopeFactory scopeFactory
    )
    {
        _logger = logger;
        _rabbitMQService = rabbitMQService;
        _redisService = redisService;
        _queueSettings = options.Value;
        _scopeFactory = scopeFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Client Worker service starting at {time}", DateTimeOffset.Now);

        // Connect to RabbitMQ consumer (TERAS)
        await _rabbitMQService.GetConnectionAsync(_queueSettings.Default);
        // Connect Redis
        await _redisService.ConnectAsync(cancellationToken);
        // Load plaza mappings into memory before processing any messages
        //await _plazaService.InitializeAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Client Worker running at: {time}", DateTimeOffset.Now);

        var clientQueue = _queueSettings.Default.Queues["Client"];

        // DECISION: EnsureRetryQueues called before StartConsumingAsync.
        // Queues must exist before any message can be routed to them.
        // If a retry is triggered before the queue exists, RabbitMQ drops the message silently.
        // TxnService derives the required delays from its own policies � RmqService knows nothing about them.
        var requiredDelays = TxnService.RetryPolicies.Values
            .SelectMany(p => p.DelaysMs)
            .Distinct();

        await _rabbitMQService.EnsureRetryQueuesAsync(clientQueue.QueueName, requiredDelays);

        try
        {
            // Define your Queue based on appsettings.json (Client/Persistor)
            await _rabbitMQService.StartConsumingAsync(
                _queueSettings.Default,
                _queueSettings.Default.Queues["Client"],
                async (message, retryCount) =>
                {
                    TxnLPPMsg? payload;
                    try
                    {
                        payload = JsonSerializer.Deserialize<TxnLPPMsg>(message);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize message (invalid JSON/shape): {msg}", message);
                        // ack and drop - message will never deserialize correctly, no point retrying
                        return new RabbitmqHandlerResult
                        {
                            Action = MessageAction.Ack
                        };
                    }

                    if (payload == null)
                    {
                        _logger.LogWarning("Failed to deserialize message (null payload): {msg}", message);
                        // ack and drop
                        return new RabbitmqHandlerResult
                        { Action = MessageAction.Ack };
                    }

                    // Process the message using a scoped service
                    using var scope = _scopeFactory.CreateScope();
                    var txnService = scope.ServiceProvider.GetRequiredService<ITxnService>();

                    try
                    {
                        var result = await txnService.ProcessLPPMessageAsync(payload, retryCount);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing RabbitMQ message");
                        return new RabbitmqHandlerResult { Action = MessageAction.Nack };
                    }
                },
                //"default", // default connection
                cancellationToken: stoppingToken);

            // Keep running until cancelled
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client Worker execution canceled.");
        }
        finally
        {
            _logger.LogInformation("Client Worker stopping at: {time}", DateTimeOffset.Now);
        }
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Client Worker service stopping at {time}", DateTimeOffset.Now);

        if (_rabbitMQService != null)
            await _rabbitMQService.DisposeAsync(); // async disposal

        await base.StopAsync(cancellationToken);
    }
}