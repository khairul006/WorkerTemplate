using Npgsql;
using NpgsqlTypes;
using Newtonsoft.Json;

namespace OBUTxnPst.Utils
{
    internal static class PostgresUtil
    {
        public static NpgsqlParameter TimestampTz(string name, DateTimeOffset value) =>
            new(name, NpgsqlDbType.TimestampTz) { Value = value.ToUniversalTime() };

        public static NpgsqlParameter Timestamp(string name, DateTimeOffset value) =>
            new(name, NpgsqlDbType.Timestamp) { Value = value.DateTime };

        public static NpgsqlParameter Date(string name, DateTimeOffset value) =>
            new(name, NpgsqlDbType.Date) { Value = value.Date }; // strips time & offset

        public static NpgsqlParameter Jsonb(string name, object value) =>
            new(name, NpgsqlDbType.Jsonb)
            {
                Value = JsonConvert.SerializeObject(value)
            };
    }
}
