using System.Text.Json.Serialization;

namespace WorkerTemplate.Models
{
    public class TxnLPPMBBRequest
    {
        public required string transactionReference { get; set; }
        public required string payload { get; set; }

    }

}

