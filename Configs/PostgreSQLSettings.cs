using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OBUTxnPst.Configs
{
    public class PostgreSQLSettings
    {
        // PostgreSQL settings
        public required string Host { get; set; }
        public required string Port { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string Database { get; set; }
        public required bool SslMode { get; set; }
    }
}
