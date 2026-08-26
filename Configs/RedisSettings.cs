using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerTemplate.Configs
{
    public class RedisSettings
    {
        public string Host { get; set; } = "localhost";
        public string Port { get; set; } = "6379";
        public string? Password { get; set; }
        public int Database { get; set; } = 0;
        public bool UseSsl { get; set; } = false;
        public string? SslHost { get; set; }
        public int ConnectTimeoutMs { get; set; } = 5000;
    }
}
