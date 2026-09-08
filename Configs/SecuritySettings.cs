namespace WorkerTemplate.Configs;

public class SecuritySettings
{
    public MbbSecurityOptions Mbb { get; set; } = new();
}

public class MbbSecurityOptions
{
    public string Algorithm { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;
}