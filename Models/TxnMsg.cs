namespace WorkerTemplate.Models
{
    public class TxnMsg
    {
        public required Sdp sdp { get; set; }
        public required Header header { get; set; }
        public required Body body { get; set; }
        public required AdditionalInfo additionalInfo { get; set; }


        public class Sdp
        {
            public required string topic { get; set; }
        }

        public class Header
        {
            public required string serialNum { get; set; }
            public DateTimeOffset? timestamp { get; set; }
        }

        public class Body
        {
            public required string transactionId { get; set; }
            public string? transactionAmount { get; set; }
            public DateTimeOffset entryTimestamp { get; set; }
            public DateTimeOffset exitTimestamp { get; set; }
            public string? vehicleClass { get; set; }
        }

        public class AdditionalInfo
        {
            
            public string? plateNo { get; set; }
        }
    }
}
