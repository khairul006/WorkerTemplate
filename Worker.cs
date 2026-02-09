using System.Text.Json;
using WorkerTemplate;
using WorkerTemplate.Models;
using WorkerTemplate.Providers;
using WorkerTemplate.Services;

namespace WorkerTemplate
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMQService _rabbitMQService;
        private readonly PostgresService _postgresService;
        private readonly TxnService _txnService;

        public Worker(
            ILogger<Worker> logger,
            RabbitMQService rabbitMQService,
            PostgresService postgresService,
            TxnService txnService
        )
        {
            _logger = logger;
            _rabbitMQService = rabbitMQService;
            _postgresService = postgresService;
            _txnService = txnService;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker service starting at {time}", DateTimeOffset.Now);

            // Connect to RabbitMQ consumer
            await _rabbitMQService.ConnectConsumer(cancellationToken);
            // Connect to RabbitMQ publisher
            await _rabbitMQService.ConnectPublisher(cancellationToken);
            // Test postgres connection
            await _postgresService.ConnectPostgresAsync(cancellationToken);

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            try
            {
                // Start consuming asynchronously
                await _rabbitMQService.StartConsumingAsync(async message =>
                {
                    try
                    {
                        // Deserialize message
                        var payload = JsonSerializer.Deserialize<TxnMsg>(message);

                        if (payload == null)
                        {
                            _logger.LogWarning("Failed to deserialize message (null payload): {msg}", message);
                            return true; // ack and drop
                        }

                        bool success = await _txnService.PublishTxnAsync(payload);

                        if (!success)
                        {
                            _logger.LogWarning("Failed to insert message into DB: {msg}", message);
                        }

                        return success;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing RabbitMQ message");
                        return false;
                    }
                }, stoppingToken);

                // Keep running until cancelled
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker execution canceled.");
            }
            finally
            {
                _logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
            }
        }


        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker service stopping at {time}", DateTimeOffset.Now);

            if (_rabbitMQService != null)
                await _rabbitMQService.DisposeAsync(); // async disposal

            await base.StopAsync(cancellationToken);
        }
    }
}
