using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;
using System.Data;
using System.Net;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;

namespace WorkerTemplate.Providers
{
    public class RedisService : IRedisService, IAsyncDisposable
    {
        private readonly RedisSettings _settings;
        private readonly ILogger<RedisService> _logger;
        private ConnectionMultiplexer? _connection;
        private IDatabase? _database;

        public RedisService(
            IOptions<RedisSettings> options,
            ILogger<RedisService> logger)
        {
            _logger = logger;
            _settings = options.Value;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.Host))
                throw new InvalidOperationException("Redis Host is not configured.");

            var port = int.TryParse(_settings.Port, out var p) ? p : 6379;

            var config = new ConfigurationOptions
            {
                EndPoints = { { _settings.Host, port } },
                Password = _settings.Password,
                DefaultDatabase = _settings.Database,
                ConnectTimeout = 30_000,
                SyncTimeout = 10_000,
                AbortOnConnectFail = false,
                KeepAlive = 60, // periodic ping to prevent idle-connection drops behind LBs/firewalls
                                // Single retry mechanism: let the library own retries. Don't also wrap this
                                // in a manual retry loop — stacking both makes failure timing unpredictable.
                ConnectRetry = 5,
                ReconnectRetryPolicy = new ExponentialRetry(5000)
            };

            if (_settings.UseSsl)
            {
                config.Ssl = true;
                if (!string.IsNullOrWhiteSpace(_settings.SslHost))
                    config.SslHost = _settings.SslHost;
                else if (!IPAddress.TryParse(_settings.Host, out _))
                    config.SslHost = _settings.Host;
                else
                    _logger.LogWarning(
                        "Redis Host is an IP address and no SslHost was configured. TLS certificate hostname validation may fail.");
            }

            try
            {
                var conn = await ConnectionMultiplexer.ConnectAsync(config).ConfigureAwait(false);

                if (!conn.IsConnected)
                    throw new InvalidOperationException("ConnectionMultiplexer did not report a connected state.");

                _connection = conn;
                _database = conn.GetDatabase(_settings.Database);

                _logger.LogInformation("Redis connected (host={Host}, port={Port}, db={db}, useSsl={useSsl})",
                    _settings.Host, port, _settings.Database, _settings.UseSsl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis (host={Host}, port={Port}, db={db}, useSsl={useSsl})",
                    _settings.Host, port, _settings.Database, _settings.UseSsl);
                throw;
            }
        }


        private IDatabase GetDatabase()
        {
            if (_database == null)
                throw new InvalidOperationException("RedisService is not connected. Call ConnectAsync first.");
            return _database;
        }


        // Health check connection
        public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken)
        {
            string safeLogInfo = $"{_settings.Host}:{_settings.Port}/db{_settings.Database}";
            try
            {
                var db = GetDatabase();
                var latency = await db.PingAsync();
                _logger.LogInformation(
                    "Redis health check passed. Connected to: {RedisInfo} (Latency: {Latency}ms)",
                    safeLogInfo, latency.TotalMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed.");
                return false;
            }
        }

        // Get raw string
        public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                var value = await db.StringGetAsync(key);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis GetStringAsync failed for key {Key}.", key);
                throw;
            }
        }

        // Set raw string
        public async Task<bool> SetStringAsync(
            string key,
            string value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                return await db.StringSetAsync(key, value, expiry, keepTtl: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis SetStringAsync failed for key {Key}.", key);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                return await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis DeleteAsync failed for key {Key}.", key);
                throw;
            }
        }

        public async Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                return await db.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis KeyExistsAsync failed for key {Key}.", key);
                throw;
            }
        }


        // Hash
        public async Task<string?> HashGetAsync(string key, string field, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                var value = await db.HashGetAsync(key, field);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis HashGetAsync failed for key {Key}, field {Field}.", key, field);
                throw;
            }
        }

        public async Task<bool> HashSetAsync(string key, string field, string value, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                await db.HashSetAsync(key, field, value);
                return true; // HashSet returns true=new field / false=updated existing; both are success
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis HashSetAsync failed for key {Key}, field {Field}.", key, field);
                throw;
            }
        }

        public async Task<bool> HashDeleteAsync(string key, string field, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                return await db.HashDeleteAsync(key, field);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis HashDeleteAsync failed for key {Key}, field {Field}.", key, field);
                throw;
            }
        }

        public async Task<bool> HashExistsAsync(string key, string field, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = GetDatabase();
                return await db.HashExistsAsync(key, field);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis HashExistsAsync failed for key {Key}, field {Field}.", key, field);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await _connection.CloseAsync().ConfigureAwait(false);
                    _connection.Dispose();
                    _connection = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Redis connection.");
            }
        }
    }
}
