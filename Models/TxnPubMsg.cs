namespace WorkerTemplate.Models
{
    public class TxnPubMsg
    {
        public required Header header { get; set; }
        public required Body body { get; set; }
        public required string signature { get; set; }
    

        public class Header
        {
            public DateTimeOffset? timestamp { get; set; }
        }

        public class Body
        {
            public required string transactionId { get; set; }
            public string? merchantId { get; set; }
            public DateTimeOffset? entryTimestamp { get; set; }
            public DateTimeOffset? exitTimestamp { get; set; }
            public string? vehicleClass { get; set; }
        }
    }

}

