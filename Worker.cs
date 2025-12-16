using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OBUTxnPst.Configs;
using OBUTxnPst.Providers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OBUTxnPst
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMQService _rabbitMQService;
        private readonly OBUService _obuService;

        public Worker(
            ILogger<Worker> logger,
            RabbitMQService rabbitMQService,
            OBUService obuService
        )
        {
            _logger = logger;
            _rabbitMQService = rabbitMQService;
            _obuService = obuService;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker service starting at {time}", DateTimeOffset.Now);

            // Connect to RabbitMQ and declare queue/exchange
            _rabbitMQService.Connect();
            _rabbitMQService.DeclareQueue();

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
                        var payload = JsonConvert.DeserializeObject<OBUTxn.Root>(message);
                        if (payload == null)
                        {
                            _logger.LogWarning("Failed to deserialize message: {msg}", message);
                            return false; // will Nack the message
                        }
                        bool success = await _obuService.ProcessOBUMessageAsync(payload);


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
