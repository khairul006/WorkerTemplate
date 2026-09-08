namespace WorkerTemplate.Configs;

public class ApplicationSettings
{
    // This is example of application settings, you can add your own settings here based on your application needs.
    public required string SupplierLabel { get; set; }
    public required string MerchantId { get; set; }
    public required string MGateApi { get; set; }
    public required string FareServiceApi { get; set; }
    public bool? IgnoreServerCert { get; set; } = true;
    public required string RedisKeyLPP { get; set; } = "";
    public required string RedisKeyLPPJPP { get; set; } = "";

}