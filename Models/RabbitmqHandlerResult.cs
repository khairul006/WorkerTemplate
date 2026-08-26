using System.Data;

namespace WorkerTemplate.Models
{

    public class RabbitmqHandlerResult
    {
        public MessageAction Action { get; set; }
        public int? RetryDelayMs { get; set; }
    }

    public enum MessageAction
    {
        Ack,       // message completed
        Nack,      // message failed and requeu directly
        Retry,     // retry with policy
        Dead       // permanent failure, drop message
    }
}
