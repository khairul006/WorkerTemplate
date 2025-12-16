using System;
using System.Text;
using OBUTxnPst.Configs;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OBUTxnPst.Providers
{
    public class RabbitMQService : IAsyncDisposable
    {
        private readonly ILogger<RabbitMQService> _logger;
        private readonly RabbitMQSettings _settings;

        private IConnection? _connection;
        private IModel? _channel;

        public RabbitMQService(
            IOptions<RabbitMQSettings> options,
            ILogger<RabbitMQService> logger
        )
        {
            _logger = logger;
            _settings = options.Value;
        }

        public void Connect()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _settings.ConsumeBrokerHost,
                    UserName = _settings.ConsumeBrokerUsername,
                    Password = _settings.ConsumeBrokerPassword,
                    VirtualHost = _settings.ConsumeVirtualHost,
                    Port = _settings.ConsumeBrokerPort,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                    RequestedHeartbeat = TimeSpan.FromSeconds(30),
                    DispatchConsumersAsync = true // enables async consumers
                };

                _connection = factory.CreateConnection();
                _connection.ConnectionShutdown += (_, e) =>
                {
                    _logger.LogWarning("RabbitMQ connection closed: {reason}", e.ReplyText);
                };

                _channel = _connection.CreateModel();

                _logger.LogInformation("RabbitMQ connected successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish RabbitMQ connection");
                throw; // let hosting retry or fail fast
            }
        }

        public void DeclareQueue()
        {
            try
            {
                if (_channel == null)
                    throw new InvalidOperationException("RabbitMQ channel not initialized.");

                var args = new Dictionary<string, object>();

                if (_settings.ConsumeBrokerQueueType?.ToLower() == "quorum")
                    args.Add("x-queue-type", "quorum");

                _channel.QueueDeclare(
                    queue: _settings.ConsumeBrokerQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: args
                );

                if (!string.IsNullOrWhiteSpace(_settings.ConsumeBrokerExchange))
                {
                    _channel.ExchangeDeclare(_settings.ConsumeBrokerExchange, "direct", durable: true);
                    _channel.QueueBind(_settings.ConsumeBrokerQueue, _settings.ConsumeBrokerExchange, _settings.ConsumeBrokerRK);
                }

                _logger.LogInformation("Queue and exchange declared successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare RabbitMQ queue/exchange");
                throw;
            }
        }

        public async Task StartConsumingAsync(Func<string, Task<bool>> handler, CancellationToken cancellationToken)
        {
            try
            {
                if (_channel == null)
                    throw new InvalidOperationException("RabbitMQ channel not initialized.");

                _channel.BasicQos(0, _settings.Prefetch, false);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += async (sender, ea) =>
                {
                    try
                    {
                        var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                        _logger.LogInformation("Consumed json message: {msg}", msg);

                        bool success = await handler(msg);

                        if (success)
                            _channel.BasicAck(ea.DeliveryTag, false);
                        else
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling RabbitMQ message");
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                _channel.BasicConsume(queue: _settings.ConsumeBrokerQueue, autoAck: false, consumer: consumer);

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

        public bool Publish(string exchange, string routingKey, string message)
        {
            try
            {
                if (_channel == null)
                    throw new InvalidOperationException("RabbitMQ channel not initialized.");

                var body = Encoding.UTF8.GetBytes(message);

                // create basic properties
                var props = _channel.CreateBasicProperties();
                props.Persistent = true;              // make message survive broker restart
                props.ContentType = "application/json"; // optional, for clarity

                _channel.BasicPublish(exchange, routingKey, props, body);

                _logger.LogDebug("Message published to {exchange} / {routingKey}", exchange, routingKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish to RabbitMQ");
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RabbitMQ cleanup");
            }
        }

    }
}
