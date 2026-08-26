namespace WorkerTemplate.Models
{
    public class TxnLPPMsg
    {
        public Sdp? sdp { get; set; }
        public required Header header { get; set; }
        public required Body body { get; set; }
        public required AdditionalInfo additionalInfo { get; set; }


        public class Sdp
        {
            public required string realm { get; set; }
            public required string topic { get; set; }
            public object? token { get; set; }
            public object? param { get; set; }
        }

        public class Header
        {
            public required string serialNum { get; set; }
            public object? hmac { get; set; }
            public object? timestamp { get; set; }
            public object? counter { get; set; }
            public string? version { get; set; }
        }

        public class Body
        {
            public required string transactionCode { get; set; }
            public required string transactionType { get; set; }
            public required string transactionId { get; set; }
            public required string transactionAmount { get; set; }
            public required string mediaID { get; set; }
            public required string mediaTokenId { get; set; }
            public required string LPRId { get; set; }
            public required string accId { get; set; }
            public required string accType { get; set; }
            public DateTimeOffset entryTimestamp { get; set; }
            public string? entrySPId { get; set; }
            public string? entryPlazaId { get; set; }
            public string? entryLaneId { get; set; }
            public string? entryClass { get; set; }
            public DateTimeOffset exitTimestamp { get; set; }
            public required string exitSPId { get; set; }
            public required string exitPlazaId { get; set; }
            public required string exitLaneId { get; set; }
            public required string exitClass { get; set; }
            public DateTimeOffset parameterTimestamp { get; set; }
        }

        public class AdditionalInfo
        {

            public DateTimeOffset operationalDate { get; set; }
            public required string operationMode { get; set; }
            public DateTimeOffset bojDateTime { get; set; }
            public required string tcBadgeNo { get; set; }
            public required string jobType { get; set; }
            public int jobNo { get; set; }
            public int commonTrxNo { get; set; }
            public int trxNo { get; set; }
            public required string ANPRPlateNo { get; set; }
            public required string journeyExceptionCode { get; set; }
            public required string fareType { get; set; }
            public required string fareGroupId { get; set; }
            public required string fareFlexiId { get; set; }
            public required string farePlaza { get; set; }
            public string? fareAmount { get; set; }
            public DateTimeOffset detectionTimestamp { get; set; }
            public DateTimeOffset completionTimestamp { get; set; }
        }
    }
}
