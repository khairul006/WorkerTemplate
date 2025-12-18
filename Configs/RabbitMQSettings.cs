using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerTemplate.Configs
{
    public class RabbitMQSettings
    {
        public required ConsumerSettings Consumer { get; set; }
        public required PublisherSettings Publisher { get; set; }
    }

    public class ConsumerSettings
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string VirtualHost { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }

        public required string Queue { get; set; }
        public required string QueueType { get; set; }

        public ushort Prefetch { get; set; } = 10;
    }

    public class PublisherSettings
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string VirtualHost { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }

        public required string Exchange { get; set; }
        public required string ExchangeType { get; set; }
        public required string RoutingKey { get; set; }

        public bool Persistent { get; set; } = true;
    }
}
