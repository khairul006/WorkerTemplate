using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;

namespace WorkerTemplate.Providers;

public class SqlServerService : ISqlServerService
{
    private readonly ILogger<SqlServerService> _logger;
    private readonly string _connectionString;

    public SqlServerService(
        IOptions<SqlServerSettings> options,
        ILogger<SqlServerService> logger)
    {
        _logger = logger;

        // Build the connection string ONCE during initialization
        _connectionString = BuildConnectionString(options.Value);
    }

    private static string BuildConnectionString(SqlServerSettings settings)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{settings.Host},{settings.Port}",
            InitialCatalog = settings.Database,
            UserID = settings.Username,
            Password = settings.Password,

            // Connection pooling
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 10,

            // Timeout for opening a connection
            ConnectTimeout = 30,

            // Encrypt connection
            Encrypt = settings.Encrypt,

            // Useful when using internal/self-signed certificates
            TrustServerCertificate = settings.TrustServerCertificate
        };

        return builder.ConnectionString;
    }

    // Health check connection
    public async Task<bool> CheckConnectionAsync(
        CancellationToken cancellationToken)
    {
        var parser = new SqlConnectionStringBuilder(_connectionString);

        string safeLogInfo = $"{parser.DataSource}/{parser.InitialCatalog}";

        try
        {
            await using var conn = new SqlConnection(_connectionString);

            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT 1";

            await cmd.ExecuteScalarAsync(cancellationToken);

            _logger.LogInformation(
                "SQL Server health check passed. Connected to: {DatabaseInfo}",
                safeLogInfo);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SQL Server database health check failed.");

            return false;
        }
    }

    // Querying (Data returning)
    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);

            await conn.OpenAsync(cancellationToken);

            var command = new CommandDefinition(
                sql,
                parameters,
                cancellationToken: cancellationToken);

            return await conn.QueryAsync<T>(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SQL Server QueryAsync failed execution.");

            throw;
        }
    }

    // Executing (Commands)
    public async Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);

            await conn.OpenAsync(cancellationToken);

            var command = new CommandDefinition(
                sql,
                parameters,
                cancellationToken: cancellationToken);

            return await conn.ExecuteAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SQL Server ExecuteAsync failed execution.");

            throw;
        }
    }
}