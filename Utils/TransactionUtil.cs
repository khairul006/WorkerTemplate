using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerTemplate.Utils;

public class TransactionUtil
{
    // ttype mapping
    private static readonly Dictionary<string, string> _ttypeMap = new()
    {
        { "B", "Barrier (Open System)" },
        { "C", "Complete (Closed System - populate the Entry and Exit information)" },
        { "TP", "Toll Penalty (Close System – tpReasonCode must be provided) " },
        { "E", "Entry (Closed System Multi SPs - Entry SP send Entry info)" },
        { "X", "Exit (Closed System Multi SPs - Exit SP send Exit)" },
        { "RC", "Foreign Vehicle Road Charges (VEP)" },
        { "BR", "Open Toll, Toll Credit Adjustment for the Initial Entry" },
        { "BU", "Open Toll Payment for U-Turn Exit of Same Plaza" },
        { "SU", "Special U-Turn" }
    };

    public static string? GetTtypeDescription(string ttype)
    {
        return _ttypeMap.TryGetValue(ttype, out var description) ? description : null;
    }

    // SPID mapping
    private static readonly Dictionary<string, string> _spidMap = new()
    {
        { "02", "JPP" },
        { "03", "ELITE" },
        { "04", "PLUS" },
        { "05", "LINKEDUA" },
        { "11", "BKE" },
        { "18", "LPT1" },
        { "48", "JKSB" },
        { "49", "LPT2" }
    };

    public static string? GetSpName(string? spid)
    {
        if (spid is null)
            return null;

        return _spidMap.TryGetValue(spid, out var spName) ? spName : null;
    }
}