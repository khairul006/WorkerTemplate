using Npgsql;
using NpgsqlTypes;
using Newtonsoft.Json;

namespace WorkerTemplate.Utils
{
    internal static class PostgresUtil
    {
        public static NpgsqlParameter TimestampTz(string name, DateTimeOffset? value) =>
            new(name, NpgsqlDbType.TimestampTz)
            {
                Value = value.HasValue ? value.Value.ToUniversalTime() : (object)DBNull.Value
            };

        public static NpgsqlParameter Timestamp(string name, DateTimeOffset? value) =>
            new(name, NpgsqlDbType.Timestamp)
            {
                Value = value.HasValue ? value.Value.DateTime : (object)DBNull.Value
            };

        public static NpgsqlParameter Date(string name, DateTimeOffset? value) =>
            new(name, NpgsqlDbType.Date)
            {
                Value = value.HasValue ? value.Value.Date : (object)DBNull.Value // strips time & offset
            };

        public static NpgsqlParameter Jsonb(string name, object value) =>
            new(name, NpgsqlDbType.Jsonb)
            {
                Value = JsonConvert.SerializeObject(value)
            };
    }
}
