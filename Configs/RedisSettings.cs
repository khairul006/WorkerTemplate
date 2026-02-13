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
        public int Port { get; set; } = 6379;
        public string? Username { get; set; } // Redis 6+ ACL support
        public string? Password { get; set; }
        public int Database { get; set; } = 0;
        public bool Ssl { get; set; } = false;
        public string? SslHost { get; set; }
        public string LPPKey { get; set; } = "LPP";
    }
}
