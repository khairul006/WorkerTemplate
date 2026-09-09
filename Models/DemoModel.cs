using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using WorkerTemplate.Converters;

namespace WorkerTemplate.Models;

public class DemoModel
{
    public DateTimeOffset exitTimestamp { get; set; }
    public required string transactionId { get; set; }
    public required string transactionAmount { get; set; }
    public int commonTrxNo { get; set; }
    public required string responseCode { get; set; }

    [JsonConverter(typeof(IsoUtcDateTimeOffsetConverter))]
    public DateTimeOffset datetime { get; set; }
    public object? errors { get; set; }
}
