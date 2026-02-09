namespace WorkerTemplate.Configs
{
    public class HashSettings
    {
        public required bool EnableHash { get; set; } = true;
        public string? SecretKey { get; set; }
    }
}
