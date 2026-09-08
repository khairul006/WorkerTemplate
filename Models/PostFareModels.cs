using System.Text.Json.Serialization;

namespace WorkerTemplate.Models;

public class PostFareRequest
{
    public required string serialNum { get; set; }
    public required string entryPlazaId { get; set; }
    public required string exitPlazaId { get; set; }
    public required string exitSPId { get; set; }
    public required string exitTimestamp { get; set; }
    public required string vehicleClass { get; set; }
    public required string groupId { get; set; }
    public required string flexiId { get; set; }
}

public class HttpResponse<T>
{
    public required int statusCode { get; set; }
    public required string code { get; set; }
    public required string message { get; set; }
    public required string timestamp { get; set; }
    public T? data { get; set; }
    public object? error { get; set; }
}

public class PostFareData
{
    public required string fareType { get; set; }
    public required decimal fare { get; set; }
    public List<Apportionment> apportionment { get; set; } = [];
}

public class Apportionment
{
    public required string spid { get; set; }
    public decimal fare { get; set; }
}