using Microsoft.Extensions.Options;
using System.Text.Json;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;
using WorkerTemplate.Services;

namespace WorkerTemplate.Workers;

public class DemoRetryWorker : BackgroundService
{
    private readonly ILogger<DemoRetryWorker> _logger;
    private readonly IRabbitMQService _rabbitMQService;
    private readonly RabbitMQSettings _queueSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public DemoRetryWorker(
        ILogger<DemoRetryWorker> logger,
        IRabbitMQService rabbitMQService,
        IOptions<RabbitMQSettings> options,
        IServiceScopeFactory scopeFactory
    )
    {
        _logger = logger;
        _rabbitMQService = rabbitMQService;
        _queueSettings = options.Value;
        _scopeFactory = scopeFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DemoRetry Worker starting at {time}", DateTimeOffset.Now);
        // Connect to RabbitMQ consumer (Default)
        await _rabbitMQService.GetConnectionAsync(_queueSettings.Default);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DemoRetry Worker running at: {time}", DateTimeOffset.Now);

        var queue = _queueSettings.Default.Queues["DemoRetry"];

        // DECISION: EnsureRetryQueues called before StartConsumingAsync.
        // Queues must exist before any message can be routed to them.
        // If a retry is triggered before the queue exists, RabbitMQ drops the message silently.
        // DemoRetryService owns the retry policies, RmqService knows nothing about them.
        var requiredDelays = DemoRetryService.RetryPolicies.Values
            .SelectMany(p => p.DelaysMs)
            .Distinct();

        await _rabbitMQService.EnsureRetryQueuesAsync(queue.QueueName, requiredDelays);

        try
        {
            // Define your Queue based on appsettings.json (DemoRetry)
            await _rabbitMQService.StartConsumingAsync(
                _queueSettings.Default,
                _queueSettings.Default.Queues["DemoRetry"],
                async (message, retryCount) =>
                {
                    string? payload;
                    try
                    {
                        payload = JsonSerializer.Deserialize<string>(message);
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
                    var demoRetryService = scope.ServiceProvider.GetRequiredService<IDemoRetryService>();

                    try
                    {
                        var result = await demoRetryService.ProcessMessageWithRetryAsync(payload, retryCount);
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
            _logger.LogInformation("DemoRetry Worker execution canceled.");
        }
        finally
        {
            _logger.LogInformation("DemoRetry Worker stopping at: {time}", DateTimeOffset.Now);
        }
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DemoRetry Worker service stopping at {time}", DateTimeOffset.Now);

        if (_rabbitMQService != null)
            await _rabbitMQService.DisposeAsync(); // async disposal

        await base.StopAsync(cancellationToken);
    }
}