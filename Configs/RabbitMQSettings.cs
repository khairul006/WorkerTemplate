using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerTemplate.Configs
{
    public class RabbitMQSettings
    {
        public required BrokerSettings Default { get; set; }

        // add multi RMQ as needed
    }

    public class BrokerSettings
    {
        public string Protocol { get; set; } = "amqp";
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string VirtualHost { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }

        public Dictionary<string, QueueConfig> Queues { get; set; } = new();
        public Dictionary<string, ExchangeConfig> Exchanges { get; set; } = new();
    }

    public class QueueConfig
    {
        public bool Enabled { get; set; } = true;
        public required string QueueName { get; set; }
        public string QueueType { get; set; } = "classic";
        public ushort Prefetch { get; set; } = 10;
    }


    public class ExchangeConfig
    {
        public required string ExchangeName { get; set; }
        public required string ExchangeType { get; set; }
        public required string RoutingKey { get; set; }
    }
}
