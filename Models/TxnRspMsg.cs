namespace WorkerTemplate.Models
{
    public class TxnRspMsg
    {
        public required Header header { get; set; }
        public required Body body { get; set; }
        public string? remarks { get; set; }
        public string? signature { get; set; }


        public class Header
        {
            public DateTimeOffset? timestamp { get; set; }
        }

        public class Body
        {
            public string? transactionId { get; set; }
            public DateTimeOffset salesTimestamp { get; set; }
            public string? responseCode { get; set; }
            public string? detailResponseCode { get; set; }
        }
    }

}
