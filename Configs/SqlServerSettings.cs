namespace WorkerTemplate.Configs
{
    public class SqlServerSettings
    {
        public required string Host { get; set; }
        public int Port { get; set; } = 1433; // Default SQL Server port
        public required string Database { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public bool Encrypt { get; set; } = true;
        public bool TrustServerCertificate { get; set; } = false;
    }

}
