using System;
using System.Runtime.Intrinsics.Arm;

namespace OBUTxn
{
    public class Root
    {
        public required Sdp sdp { get; set; }
        public required Header header { get; set; }
        public required Body body { get; set; }
        public required AdditionalInfo additionalInfo { get; set; }
    }

    public class Sdp
    {
        public required string realm { get; set; }
        public required string topic { get; set; }
        public string? token { get; set; }
        public string? param { get; set; }
    }

    public class Header
    {
        public required string serialNum { get; set; }
        public string? hmac { get; set; }
        public DateTime timestamp { get; set; }
        public string? counter { get; set; }
        public string? version { get; set; }
    }

    public class Body
    {
        public string? transactionCode { get; set; }
        public string? transactionType { get; set; }
        public string? transactionId { get; set; }
        public string? transactionAmount { get; set; }
        public string? mediaID { get; set; }
        public string? accId { get; set; }
        public string? accType { get; set; }
        public DateTime entryTimestamp { get; set; }
        public string? entrySPId { get; set; }
        public string? entryPlazaId { get; set; }
        public string? entryLaneId { get; set; }
        public string? entryClass { get; set; }
        public DateTime exitTimestamp { get; set; }
        public string? exitSPId { get; set; }
        public string? exitPlazaId { get; set; }
        public string? exitLaneId { get; set; }
        public string? exitClass { get; set; }
        public string? registeredVechPlate { get; set; }
        public DateTime parameterTimestamp { get; set; }
    }

    public class AdditionalInfo
    {
        public DateTime operationalDate { get; set; }
        public string? operationMode { get; set; }
        public DateTime bojDateTime { get; set; }
        public string? tcBadgeNo { get; set; }
        public string? jobType { get; set; }
        public int? jobNo { get; set; }
        public int? commonTrxNo { get; set; }
        public int? trxNo { get; set; }
        public string? ANPRPlateNo { get; set; }
        public string? journeyExceptionCode { get; set; }
        public string? fareType { get; set; }
        public string? fareGroupId { get; set; }
        public string? fareFlexiId { get; set; }
        public string? farePlaza { get; set; }
        public string? fareAmount { get; set; }
        public DateTime detectionTimestamp { get; set; }
        public DateTime completionTimestamp { get; set; }
    }
}
