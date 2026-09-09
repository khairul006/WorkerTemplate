namespace WorkerTemplate.Workers;

public class DemoWorker : BackgroundService
{
    private readonly ILogger<DemoWorker> _logger;
    private readonly PeriodicTimer _timer;

    public DemoWorker(
        ILogger<DemoWorker> logger
    )
    {
        _logger = logger;
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Demo Worker starting at {time}", DateTimeOffset.Now);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Demo Worker running at: {time}", DateTimeOffset.Now);

        try
        {
            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogInformation("Hello from Demo Worker");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Demo Worker execution canceled.");
        }
        finally
        {
            _logger.LogInformation("Demo Worker stopping at: {time}", DateTimeOffset.Now);
        }
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Demo Worker service stopping at {time}", DateTimeOffset.Now);

        await base.StopAsync(cancellationToken);
    }
}