using Microsoft.Extensions.Options;
using Npgsql;
using OBUTxnPst.Configs;
using OBUTxnPst.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OBUTxnPst.Providers
{
    public class PostgresService
    {
        private readonly ILogger<PostgresService> _logger;
        private readonly PostgreSQLSettings _settings;

        public PostgresService(
            IOptions<PostgreSQLSettings> options, 
            ILogger<PostgresService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        private string BuildConnectionString()
        {
            return _settings.SslMode
                ? $"Host={_settings.Host};Port={_settings.Port};Username={_settings.Username};Password={_settings.Password};Database={_settings.Database};SSL Mode=Require;"
                : $"Host={_settings.Host};Port={_settings.Port};Username={_settings.Username};Password={_settings.Password};Database={_settings.Database};";
        }
        

        public async Task<PostgresResult> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
        {
            var result = new PostgresResult();

            try
            {
                await using var conn = new NpgsqlConnection(BuildConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                if (parameters != null)
                {
                    foreach (var kv in parameters)
                        if (kv.Value is NpgsqlParameter npgsqlParam)
                        {
                            cmd.Parameters.Add(npgsqlParam);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                        }
                }

                // Determine if query returns rows (SELECT or INSERT ... RETURNING)
                var trimmedSql = sql.TrimStart();
                bool returnsRows = trimmedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                                || trimmedSql.IndexOf("RETURNING", StringComparison.OrdinalIgnoreCase) >= 0;


                if (returnsRows)
                {
                    await using var reader = await cmd.ExecuteReaderAsync();
                    result.Rows.Load(reader);
                    result.RowsAffected = result.Rows.Rows.Count; // approximate rows affected
                }
                else
                {
                    // For non-RETURNING DML, get rows affected
                    // If command is SELECT or INSERT ... RETURNING, ExecuteReader already executed,
                    // so RowsAffected will be 0, which is fine
                    result.RowsAffected = await cmd.ExecuteNonQueryAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Postgres ExecuteAsync failed");
                throw;
            }
        }
    }
}
