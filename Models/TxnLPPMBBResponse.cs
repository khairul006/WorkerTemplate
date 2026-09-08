using System.Text.Json.Serialization;
using WorkerTemplate.Converters;

namespace WorkerTemplate.Models;

public class TxnLPPMBBResponse
{
    public string? correlationId { get; set; }
    public string? merchantAccountId { get; set; }
    public string? transactionReference { get; set; }
    public string? customerReference { get; set; }
    public required string responseCode { get; set; }
    public required string responseDesc { get; set; }

    [JsonConverter(typeof(IsoUtcDateTimeOffsetConverter))]
    public DateTimeOffset datetime { get; set; }
    public object? errors { get; set; }
}

public class RetryPolicy
{
    public int[] DelaysMs { get; set; } = Array.Empty<int>();
}