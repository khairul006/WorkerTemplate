using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OBUTxnPst.Configs
{
    public class RabbitMQSettings
    {
        public required string ConsumeVirtualHost { get; set; }
        public required string ConsumeBrokerHost { get; set; }
        public required int ConsumeBrokerPort { get; set; }
        public required string ConsumeBrokerUsername { get; set; }
        public required string ConsumeBrokerPassword { get; set; }
        public required string ConsumeBrokerQueue { get; set; }
        public required string ConsumeBrokerQueueType { get; set; }
        public required string ConsumeBrokerExchange { get; set; }
        public required string ConsumeBrokerRK { get; set; }
        public required bool GlobalQOS { get; set; }
        public required ushort Prefetch { get; set; }
    }
}
