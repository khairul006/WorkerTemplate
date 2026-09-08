using System.Text.Json.Serialization;

namespace WorkerTemplate.Models;

public class TxnMBBResponsePst
{
    public required TxnLPPMsg txnPayload { get; set; }
    public TxnLPPMBBPayload? mbbRequest { get; set; }
    public TxnLPPMBBResponse? mbbResponse { get; set; }
}