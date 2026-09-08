using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;

namespace WorkerTemplate.Providers;

public class PostgresService : IPostgresService
{
    private readonly ILogger<PostgresService> _logger;
    private readonly string _connectionString;

    public PostgresService(
        IOptions<PostgreSQLSettings> options,
        ILogger<PostgresService> logger)
    {
        _logger = logger;
        // Build the connection string ONCE during initialization
        _connectionString = BuildConnectionString(options.Value);
    }

    private static string BuildConnectionString(PostgreSQLSettings settings)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = settings.Host,
            Port = int.TryParse(settings.Port, out var port) ? port : 5432, // default to 5432 if parsing fails
            Username = settings.Username,
            Password = settings.Password,
            Database = settings.Database,
            SearchPath = settings.Schema ?? "public", // optional schema setting, default to "public" if not provided
            SslMode = Enum.TryParse<Npgsql.SslMode>(settings.SslMode, true, out var sslMode) ? sslMode : Npgsql.SslMode.Disable,

            // Ensure connection pooling is optimized for multi-threaded workers
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 10
        };
        return builder.ConnectionString;
    }

    // Health check connection
    public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        var parser = new NpgsqlConnectionStringBuilder(_connectionString);
        string safeLogInfo = $"{parser.Host}:{parser.Port}/{parser.Database} (Schema: {parser.SearchPath ?? "public"})";

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cancellationToken);

            _logger.LogInformation("PostgreSQL health check passed. Connected to: {DatabaseInfo}", safeLogInfo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed.");
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
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            // Pass the CancellationToken to the Dapper command definition
            var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
            return await conn.QueryAsync<T>(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Postgres QueryAsync failed execution.");
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
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
            return await conn.ExecuteAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Postgres ExecuteAsync failed execution.");
            throw;
        }
    }

}