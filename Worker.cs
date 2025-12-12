using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly QueueManager _queueManager;
        private readonly OBUService _obuService;

        public Worker(
            ILogger<Worker> logger,
            QueueManager queueManager,
            OBUService obuService
        )
        {
            _logger = logger;
            _queueManager = queueManager;
            _obuService = obuService;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker service starting at {time}", DateTimeOffset.Now);
            _queueManager.StartConsuming();

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            try
            {
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
                _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
            }
        }


        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _queueManager.Dispose();
            _logger.LogInformation("Worker service stopping at {time}", DateTimeOffset.Now);
            await base.StopAsync(cancellationToken);
        }
    }
}
