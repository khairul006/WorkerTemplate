namespace WorkerTemplate.Configs;

public class ElasticSearchSettings
{
    // Elastic settings
    public required string BaseUrl { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Index { get; set; }
}