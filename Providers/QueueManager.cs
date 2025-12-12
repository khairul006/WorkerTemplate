using System;
using System.Text;
using OBUTxnPst.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OBUTxnPst.Providers
{

    public class QueueManager : IDisposable
    {
        private readonly ILogger<QueueManager> _logger;
        private readonly RabbitMQSettings _settings;
        private readonly OBUService _obuService;

        private IConnection? _connection;
        private IModel? _channel;

        public QueueManager(
            IOptions<RabbitMQSettings> options,
            ILogger<QueueManager> logger,
            OBUService obuService)
        {
            _logger = logger;
            _settings = options.Value;
            _obuService = obuService;
        }

        public void StartConsuming()
        {
            Task.Run(async () =>
            {
                while (true)
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
                            RequestedConnectionTimeout = TimeSpan.FromSeconds(20),
                            RequestedHeartbeat = TimeSpan.FromSeconds(30)
                        };

                        _connection = factory.CreateConnection();
                        _connection.ConnectionShutdown += OnConnectionShutdown;

                        _channel = _connection.CreateModel();
                        _channel.BasicQos(0, _settings.Prefetch, _settings.GlobalQOS);
                        QueueDeclare();

                        var consumer = new EventingBasicConsumer(_channel);
                        consumer.Received += OnMessageReceived;

                        _channel.BasicConsume(queue: _settings.ConsumeBrokerQueue, autoAck: false, consumer: consumer);

                        _logger.LogInformation("Connected and consuming from RabbitMQ.");
                        break; // Exit loop on success
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error connecting to RabbitMQ. Retrying in 5 seconds...");
                        await Task.Delay(5000);
                    }
                }
            });
        }


        private void OnMessageReceived(object? sender, BasicDeliverEventArgs e)
        {
            if (_channel == null)
            {
                _logger.LogWarning("RabbitMQ channel is not initialized.");
                return;
            }

            try
            {
                var message = Encoding.UTF8.GetString(e.Body.ToArray());
                _logger.LogInformation("Consumed json message: {msg}", message);

                var payload = JsonConvert.DeserializeObject<OBUTxn.Root>(message);
                if (payload is null)
                {
                    _logger.LogWarning("Deserialization failed for message: {msg}", message);
                    _channel.BasicNack(e.DeliveryTag, false, true);
                    return;
                }

                bool success = _obuService.InsertDataToDB(payload, message);

                if (success)
                {
                    _channel.BasicAck(e.DeliveryTag, false);
                }
                else
                {
                    _channel.BasicNack(e.DeliveryTag, false, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message.");

                if (_channel != null)
                {
                    _channel.BasicNack(e.DeliveryTag, false, true);
                }
            }
        }


        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shut down: {reason}", e.ReplyText);
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }

        private void QueueDeclare()
        {
            if (_channel == null)
            {
                //throw new InvalidOperationException("RabbitMQ channel is not initialized.");
                // Or log an error and return:
                _logger.LogError("RabbitMQ channel is null. Queue declaration skipped.");
                return;
            }

            var arguments = new Dictionary<string, object>();

            if (_settings.ConsumeBrokerQueueType?.ToLower() == "quorum")
            {
                arguments.Add("x-queue-type", "quorum");
            }

            // Declare the queue with the right type (classic/quorum)
            _channel.QueueDeclare(
                queue: _settings.ConsumeBrokerQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments // <--- This is the key fix
            );

            // Always declare the queue (safe to do)
            //_channel.QueueDeclare(_settings.ConsumeBrokerQueue, durable: true, exclusive: false, autoDelete: false);

            // Only declare and bind exchange if it's not empty
            if (!string.IsNullOrWhiteSpace(_settings.ConsumeBrokerExchange))
            {
                _channel.ExchangeDeclare(_settings.ConsumeBrokerExchange, "direct", durable: true, autoDelete: false);
                _channel.QueueBind(_settings.ConsumeBrokerQueue, _settings.ConsumeBrokerExchange, _settings.ConsumeBrokerRK);
            }
            
        }
    }
}
