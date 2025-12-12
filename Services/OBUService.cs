using Microsoft.Extensions.Options;
using System.Data;
using System;
using Microsoft.Extensions.Logging;
using Npgsql;
using OBUTxnPst.Configs;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OBUTxnPst
{
    public class OBUService
    {
        private readonly ILogger<OBUService> _logger;
        private readonly PostgreSQLSettings _postgresSettings;

        public OBUService(IOptions<PostgreSQLSettings> postgresOptions, ILogger<OBUService> logger)
        {
            _postgresSettings = postgresOptions.Value;
            _logger = logger;
        }

        private string BuildConnectionString()
        {
            // Handle adding Pooling, MaxPoolSize etc. here if needed
            return _postgresSettings.SslMode
                ? $"Host={_postgresSettings.Host};Port={_postgresSettings.Port};Username={_postgresSettings.Username};Password={_postgresSettings.Password};Database={_postgresSettings.Database};SSL Mode=Require;"
                : $"Host={_postgresSettings.Host};Port={_postgresSettings.Port};Username={_postgresSettings.Username};Password={_postgresSettings.Password};Database={_postgresSettings.Database};";
        }


        public bool InsertDataToDB(OBUTxn.Root p, string json)
        {
            try
            {
                using var conn = new NpgsqlConnection(BuildConnectionString());
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = @"
                    INSERT INTO public.obu_txn(
                        oper_date,
                        txn_date,
                        exit_job,
                        exit_spid,                   
                        exit_plaza,
                        exit_lane,
                        txn_id,
                        entry_job??,
                        entry_spid,
                        entry_plaza,
                        entry_lane,
                        entry_txn_id??, 
                        entry_class,
                        entry_timestamp,
                        transaction_type,
                        txn_date_time,
                        txn_json,
                        media_id,
                        accountid,
                        vehicle_plateno,
                        obu_parametertimestamp,
                        account_type,
                        vehicle_class,
                        obu_detectedtimestamp
                    ) 
                    VALUES (
                        @oper_date,
                        @txn_date,
                        @exit_job,
                        @exit_spid,                   
                        @exit_plaza,
                        @exit_lane,
                        @txn_id,
                        @entry_job??,
                        @entry_spid,
                        @entry_plaza,
                        @entry_lane,
                        @entry_txn_id??, 
                        @entry_class,
                        @entry_timestamp,
                        @transaction_type,
                        @txn_date_time,
                        @txn_json,
                        @media_id,
                        @accountid,
                        @vehicle_plateno,
                        @obu_parametertimestamp,
                        @account_type,
                        @vehicle_class,
                        @obu_detectedtimestamp
                    )
                ";

                cmd.Parameters.AddWithValue("oper_date", (object?)p.additionalInfo.operationalDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("txn_date", (object?)p.body.exitTimestamp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("exit_job", (object?)p.additionalInfo.jobNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("exit_spid", (object?)p.body.exitSPId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("exit_plaza", (object?)p.body.exitPlazaId ?? DBNull.Value);

                cmd.Parameters.AddWithValue("exit_lane", (object?)p.body.exitLaneId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("txn_id", (object?)p.body.transactionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("entry_job", (object?)p.body.entrySPId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("entry_spid", (object?)p.body.entrySPId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("entry_plaza", (object?)p.body.entryPlazaId ?? DBNull.Value);

                cmd.Parameters.AddWithValue("entry_lane", (object?)p.body.entryLaneId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("entry_txn_id,", (object?)p.body.transactionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("entry_class", (object?)p.body.entryClass ?? DBNull.Value);
                cmd.Parameters.AddWithValue("entry_timestamp", (object?)p.body.entryTimestamp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("transaction_type", (object?)p.body.transactionType ?? DBNull.Value);

                cmd.Parameters.AddWithValue("txn_date_time", (object?)p.body.exitTimestamp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("media_id", (object?)p.body.mediaID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("accountid", (object?)p.body.accId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("vehicle_plateno", (object?)p.body.registeredVechPlate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("obu_parametertimestamp", (object?)p.body.parameterTimestamp ?? DBNull.Value);
                
                cmd.Parameters.AddWithValue("account_type", (object?)p.body.accType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("vehicle_class", (object?)p.body.exitClass ?? DBNull.Value);
                cmd.Parameters.AddWithValue("obu_detectedtimestamp", (object?)p.additionalInfo.detectionTimestamp ?? DBNull.Value);

                // json
                string txnJson = Newtonsoft.Json.JsonConvert.SerializeObject(p);
                cmd.Parameters.AddWithValue("txn_json", txnJson);


                cmd.ExecuteNonQuery();
                _logger.LogInformation("Insert successful.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to insert OBU transaction.");
                return false;
            }
        }

    }
}
