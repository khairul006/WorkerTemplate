using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using WorkerTemplate.Configs;

namespace WorkerTemplate.Providers
{
    public class RabbitMQService : IAsyncDisposable
    {
        private readonly ILogger<RabbitMQService> _logger;
        private readonly RabbitMQSettings _settings;

        private IConnection? _consumerConnection;
        private IModel? _consumerChannel;

        private IConnection? _publisherConnection;
        private IModel? _publisherChannel;

        public RabbitMQService(
            IOptions<RabbitMQSettings> options,
            ILogger<RabbitMQService> logger
        )
        {
            _logger = logger;
            _settings = options.Value;
        }

        private static ConnectionFactory CreateFactory(
            string host,
            int port,
            string vhost,
            string username,
            string password
        )
        {
            var factory = new ConnectionFactory
            {
                // Default settings
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
                DispatchConsumersAsync = true
            };

            // URI-based (CloudAMQP, managed RMQ, etc.)
            if (Uri.TryCreate(host, UriKind.Absolute, out var baseUri) && (baseUri.Scheme == "amqp" || baseUri.Scheme == "amqps"))
            {
                // If username/password are NOT in URI, inject them safely
                if (string.IsNullOrEmpty(baseUri.UserInfo) && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    var encodedUser = Uri.EscapeDataString(username);
                    var encodedPass = Uri.EscapeDataString(password);
                    // vhost MUST keep leading slash, then be encoded
                    var vhostValue = string.IsNullOrWhiteSpace(vhost) ? "/" : vhost;
                    if (!vhostValue.StartsWith("/"))
                        vhostValue = "/" + vhostValue;

                    var encodedVhost = Uri.EscapeDataString(vhostValue);

                    var builder = new UriBuilder(baseUri)
                    {
                        Port = port,
                        UserName = encodedUser,
                        Password = encodedPass,
                        Path = encodedVhost
                    };
                    factory.Uri = builder.Uri;
                }
                else
                {
                    // Credentials already inside URI (must already be encoded!)
                    factory.Uri = baseUri;
                }

                // TLS handling
                if (baseUri.Scheme == "amqps")
                {
                    factory.Ssl.Enabled = true;
                    factory.Ssl.AcceptablePolicyErrors =
                        System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors |
                        System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch;
                }
            }
            else
            {
                // Fallback to manual configuration for IP-based connections
                factory.HostName = host;
                factory.Port = port;
                factory.VirtualHost = vhost;
                factory.UserName = username;
                factory.Password = password;
            }

            return factory;
        }

        public async Task ConnectConsumer(CancellationToken stoppingToken)
        {
            var c = _settings.Consumer;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var factory = CreateFactory(c.Host, c.Port, c.VirtualHost, c.Username, c.Password);
                    _consumerConnection = factory.CreateConnection();
                    _consumerChannel = _consumerConnection.CreateModel();
                    _consumerChannel.BasicQos(
                        prefetchSize: 0,
                        prefetchCount: c.Prefetch,
                        global: false
                    );

                    var args = new Dictionary<string, object>();
                    if (c.QueueType == "quorum")
                        args["x-queue-type"] = "quorum";

                    _consumerChannel.QueueDeclare(
                        queue: c.Queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: args
                    );

                    _logger.LogInformation("RabbitMQ consumer connected (host={Host}, port={Port}, vhost={VirtualHost}, queue={Queue})",
                        c.Host, c.Port, c.VirtualHost, c.Queue);

                    break; // success, exit loop
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to establish RabbitMQ connection");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); ; // let hosting retry or fail fast (throw;)
                }
            }
        }


        public async Task StartConsumingAsync(Func<string, Task<bool>> handler, CancellationToken cancellationToken)
        {
            try
            {
                if (_consumerChannel == null)
                    throw new InvalidOperationException("RabbitMQ consumer channel not initialized.");

                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.Received += async (sender, ea) =>
                {
                    try
                    {
                        var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                        _logger.LogInformation("Consumed json message: {msg}", msg);

                        // Only check if it’s valid JSON
                        try
                        {
                            using var doc = JsonDocument.Parse(msg);
                        }
                        catch (JsonException)
                        {
                            _logger.LogWarning("Invalid JSON, dropping message: {msg}", msg);
                            _consumerChannel.BasicAck(ea.DeliveryTag, false);
                            return;
                        }

                        bool success = await handler(msg);

                        if (success)
                            _consumerChannel.BasicAck(ea.DeliveryTag, false);
                        else
                            _consumerChannel.BasicNack(ea.DeliveryTag, false, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling RabbitMQ consume message");
                        _consumerChannel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                _consumerChannel.BasicConsume(queue: _settings.Consumer.Queue, autoAck: false, consumer: consumer);

                _logger.LogInformation("Started consuming from RabbitMQ.");

                // Keep alive using TaskCompletionSource
                var tcs = new TaskCompletionSource();

                cancellationToken.Register(() =>
                {
                    _logger.LogInformation("RabbitMQ consumption canceled.");
                    tcs.SetResult();
                });

                await tcs.Task; // Wait here until cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start consumer");
                throw;
            }
        }

        public async Task ConnectPublisher(CancellationToken stoppingToken)
        {
            var p = _settings.Publisher;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var factory = CreateFactory(p.Host, p.Port, p.VirtualHost, p.Username, p.Password);

                    _publisherConnection = factory.CreateConnection();
                    _publisherChannel = _publisherConnection.CreateModel();

                    var args = new Dictionary<string, object>();
                    _publisherChannel.ExchangeDeclare(
                        exchange: p.Exchange,
                        type: p.ExchangeType,
                        durable: true,
                        autoDelete: false,
                        arguments: args
                    );

                    _logger.LogInformation("RabbitMQ publisher connected (host={Host}, port={Port}, vhost={VirtualHost}, exchange={Exchange})", p.Host, p.Port, p.VirtualHost, p.Exchange);

                    break; // success, exit loop
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to establish RabbitMQ publisher connection");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); ; // let hosting retry or fail fast (throw;)
                }
            }
        }


        public Task<bool> PublishAsync(string message)
        {
            if (_publisherChannel == null)
                throw new InvalidOperationException("RabbitMQ publisher channel not initialized.");

            // Offload the blocking publish to a thread pool thread
            return Task.Run(() =>
            {
                try
                {
                    var body = Encoding.UTF8.GetBytes(message);

                    // create basic properties
                    var props = _publisherChannel.CreateBasicProperties();
                    props.Persistent = true;              // make message survive broker restart
                    props.ContentType = "application/json"; // optional, for clarity

                    _publisherChannel.BasicPublish(
                        exchange: _settings.Publisher.Exchange,
                        routingKey: _settings.Publisher.RoutingKey,
                        basicProperties: props,
                        body: body
                    );

                    //_logger.LogDebug("Message published to {exchange} / {routingKey}", _settings.Publisher.Exchange, _settings.Publisher.RoutingKey);
                    _logger.LogInformation("Published json message: {msg}", message);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish to RabbitMQ");
                    return false;
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _consumerChannel?.Close();
                _consumerConnection?.Close();

                _publisherChannel?.Close();
                _publisherConnection?.Close();
                await Task.CompletedTask;
            }
            catch (ChannelClosedException)
            {
                // Expected during shutdown, ignore
                _logger.LogDebug("RabbitMQ channel already closed during shutdown.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RabbitMQ cleanup");
            }
        }

    }
}
