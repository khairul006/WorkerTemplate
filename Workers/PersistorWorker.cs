using Microsoft.Extensions.Options;
using System.Runtime;
using System.Text.Json;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;

namespace WorkerTemplate.Workers;

public class PersistorWorker : BackgroundService
{
    private readonly ILogger<PersistorWorker> _logger;
    private readonly IRabbitMQService _rabbitMQService;
    private readonly IPostgresService _postgresService;
    private readonly RabbitMQSettings _queueSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public PersistorWorker(
        ILogger<PersistorWorker> logger,
        IRabbitMQService rabbitMQService,
        IPostgresService postgresService,
        IOptions<RabbitMQSettings> options,
        IServiceScopeFactory scopeFactory
    )
    {
        _logger = logger;
        _rabbitMQService = rabbitMQService;
        _postgresService = postgresService;
        _queueSettings = options.Value;
        _scopeFactory = scopeFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Persistor Worker service starting at {time}", DateTimeOffset.Now);

        // Connect to RabbitMQ consumer
        await _rabbitMQService.GetConnectionAsync(_queueSettings.Default);
        // Test postgres connection
        await _postgresService.CheckConnectionAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Persistor Worker running at: {time}", DateTimeOffset.Now);

        try
        {
            // Start consuming asynchronously
            // Define your Queue based on appsettings.json (Persistor)
            await _rabbitMQService.StartConsumingAsync(
                _queueSettings.Default,
                _queueSettings.Default.Queues["Persistor"],
                async (message, retryCount) =>
                {
                    LPPJustgoResPayload? payload;
                    try
                    {
                        // Deserialize message
                        payload = JsonSerializer.Deserialize<LPPJustgoResPayload>(message);
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
                        {
                            Action = MessageAction.Ack
                        };
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var persistorService = scope.ServiceProvider.GetRequiredService<IPersistorService>();

                    try
                    {
                        var result = await persistorService.SaveToDbAsync(payload, message, retryCount);
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
            _logger.LogInformation("Persistor Worker execution canceled.");
        }
        finally
        {
            _logger.LogInformation("Persistor Worker stopping at: {time}", DateTimeOffset.Now);
        }
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Persistor Worker service stopping at {time}", DateTimeOffset.Now);

        if (_rabbitMQService != null)
            await _rabbitMQService.DisposeAsync(); // async disposal

        await base.StopAsync(cancellationToken);
    }
}