using Serilog;
using System;

namespace OBUTxnPst.Providers
{
    public static class Logging
    {
        public static void writeInformationLog(string strInfo)
        {
            Console.WriteLine(strInfo);         // Always print
            Log.Information(strInfo);           // Also log to Serilog sinks
        }

        public static void writeWarningLog(string strWarn)
        {
            Console.WriteLine(strWarn);
            Log.Warning(strWarn);
        }

        public static void writeErrLog(string strErr)
        {
            Console.WriteLine(strErr);
            Log.Error(strErr);
        }
    }
}
