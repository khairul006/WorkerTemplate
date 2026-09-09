using System.Text.Json;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;

namespace WorkerTemplate.Services;

public class DemoRetryService : IDemoRetryService
{
    private readonly ILogger<DemoRetryService> _logger;

    public DemoRetryService(
        ILogger<DemoRetryService> logger
    )
    {
        _logger = logger;
    }

    // Define retry policies
    public static readonly Dictionary<string, RetryPolicy> RetryPolicies = new()
    {
        // Default retry every 60 seconds
        ["Default"] = new RetryPolicy
        {
            DelaysMs = new[] { 60000 }
        }
    };


    // Processing message from RabbitMQ
    public async Task<RabbitmqHandlerResult> ProcessMessageWithRetryAsync(string payload, int retryCount)
    {
        try
        {
            // Simulate processing logic (Call Redis, DB, etc.) and any error it will retry indifinitely every 1 minutes
            var serializedPayload = JsonSerializer.Serialize(payload);
            _logger.LogInformation("Processing message. RetryCount={retryCount} Payload={payload}", retryCount, serializedPayload);

            if (payload == "error")
            {
                throw new Exception("Simulated processing error");
            }

            // If successful, return true to acknowledge the message
            _logger.LogInformation("Successfully processed message");
            return new RabbitmqHandlerResult
            {
                Action = MessageAction.Ack
            };

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message. Requeue message.");
            return new RabbitmqHandlerResult
            {
                Action = MessageAction.Retry,
                RetryDelayMs = RetryPolicies["Default"].DelaysMs[0]
            };
        }
    }

   
}