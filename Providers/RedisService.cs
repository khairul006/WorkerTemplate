using GG.EntryInfo.Setter.Configs;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace WorkerTemplate.Providers
{
    public class RedisService
    {
        private readonly ILogger<RedisService> _logger;
        private readonly RedisSettings _settings;
        private IConnectionMultiplexer? _redis;
        private IDatabase? _db;

        public RedisService(IOptions<RedisSettings> options, ILogger<RedisService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task ConnectRedisAsync(CancellationToken stoppingToken)
        {
            var options = new ConfigurationOptions
            {
                EndPoints = { { _settings.Host, _settings.Port } },
                User = _settings.Username,
                Password = _settings.Password,
                DefaultDatabase = _settings.Database,
                Ssl = _settings.Ssl,
                SslHost = _settings.SslHost, // Usually same as Host
                AbortOnConnectFail = false   // Recommended for long-running workers
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _redis = await ConnectionMultiplexer.ConnectAsync(options);
                    _db = _redis.GetDatabase();

                    _logger.LogInformation("Redis connected: {Host}:{Port} (DB: {Db})",
                        _settings.Host, _settings.Port, _settings.Database);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Redis connection failed. Retrying in 10s...");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }

        // --- String Methods ---

        public async Task<string?> GetStringAsync(string key)
        {
            var value = await _db!.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }

        public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            await _db!.StringSetAsync(key, value, expiry, When.Always);
        }

        // --- Hash Methods ---

        public async Task<string?> GetHashFieldAsync(string key, string field)
        {
            var value = await _db!.HashGetAsync(key, field);
            return value.HasValue ? value.ToString() : null;
        }

        public async Task SetHashFieldsAsync(string key, HashEntry[] entries)
        {
            await _db!.HashSetAsync(key, entries);
        }

        public async Task<Dictionary<string, string>> GetAllHashAsync(string key)
        {
            var entries = await _db!.HashGetAllAsync(key);
            return entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        }

        // --- Common Methods ---

        public async Task<bool> DeleteKeyAsync(string key)
        {
            return await _db!.KeyDeleteAsync(key);
        }
    }
}
