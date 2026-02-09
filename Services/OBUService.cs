using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using OBUTxnPub.Converters;
using System.Text.Encodings.Web;
using System.Text.Json;
using WorkerTemplate.Providers;
using WorkerTemplate.Utils;
using WorkerTemplate.Models;

namespace WorkerTemplate
{
    public class OBUService
    {
        private readonly ILogger<OBUService> _logger;
        private readonly PostgresService _postgresService;

        public OBUService(
            PostgresService postgresService,
            ILogger<OBUService> logger
        )
        {
            _postgresService = postgresService;
            _logger = logger;
        }

        private static readonly JsonSerializerOptions RmqJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters =
            {
                new CanonicalDateTimeOffsetConverter()
            }
        };

        public async Task<bool> ProcessOBUMessageAsync(OBUTxnMsg payload)
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

                string sql = @"
                    INSERT INTO public.obu_txn(
                        oper_date,
                        txn_date,
                        exit_job,
                        exit_spid,                   
                        exit_plaza,
                        exit_lane,
                        txn_id,
                        entry_spid,
                        entry_plaza,
                        entry_lane,
                        entry_txn_id, 
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
                        @entry_spid,
                        @entry_plaza,
                        @entry_lane,
                        @entry_txn_id, 
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
                    ON CONFLICT (txn_id)
                    DO UPDATE SET
                        oper_date = EXCLUDED.oper_date,
                        txn_date = EXCLUDED.txn_date,
                        exit_job = EXCLUDED.exit_job,
                        exit_spid = EXCLUDED.exit_spid,                   
                        exit_plaza = EXCLUDED.exit_plaza,
                        exit_lane = EXCLUDED.exit_lane,
                        entry_spid = EXCLUDED.entry_spid,
                        entry_plaza = EXCLUDED.entry_plaza,
                        entry_lane = EXCLUDED.entry_lane,
                        entry_txn_id = EXCLUDED.entry_txn_id, 
                        entry_class = EXCLUDED.entry_class,
                        entry_timestamp = EXCLUDED.entry_timestamp,
                        transaction_type = EXCLUDED.transaction_type,
                        txn_date_time = EXCLUDED.txn_date_time,
                        txn_json = EXCLUDED.txn_json,
                        media_id = EXCLUDED.media_id,
                        accountid = EXCLUDED.accountid,
                        vehicle_plateno = EXCLUDED.vehicle_plateno,
                        obu_parametertimestamp = EXCLUDED.obu_parametertimestamp,
                        account_type = EXCLUDED.account_type,
                        vehicle_class = EXCLUDED.vehicle_class,
                        obu_detectedtimestamp = EXCLUDED.obu_detectedtimestamp;
                ";

                // Map payload to parameters
                var parameters = new Dictionary<string, object?>
                {
                    ["oper_date"] = PostgresUtil.Date("oper_date", payload.additionalInfo.operationalDate),
                    ["txn_date"] = PostgresUtil.Date("txn_date", payload.body.exitTimestamp),
                    ["exit_job"] = payload.additionalInfo.jobNo,
                    ["exit_spid"] = payload.body.exitSPId,
                    ["exit_plaza"] = payload.body.exitPlazaId,

                    ["exit_lane"] = payload.body.exitLaneId,
                    ["txn_id"] = payload.body.transactionId,
                    ["entry_spid"] = payload.body.entrySPId,
                    ["entry_plaza"] = payload.body.entryPlazaId,

                    ["entry_lane"] = payload.body.entryLaneId,
                    ["entry_txn_id"] = payload.body.transactionId,
                    ["entry_class"] = payload.body.entryClass,
                    ["entry_timestamp"] = PostgresUtil.TimestampTz("entry_timestamp", payload.body.entryTimestamp),
                    ["transaction_type"] = payload.body.transactionType,

                    ["txn_date_time"] = PostgresUtil.TimestampTz("txn_date_time", payload.body.exitTimestamp),
                    ["media_id"] = payload.body.mediaID,
                    ["accountid"] = payload.body.accId?.Trim(),
                    ["vehicle_plateno"] = payload.body.registeredVechPlate,
                    ["obu_parametertimestamp"] = PostgresUtil.TimestampTz("obu_parametertimestamp", payload.body.parameterTimestamp),

                    ["account_type"] = payload.body.accType,
                    ["vehicle_class"] = payload.body.exitClass,
                    ["obu_detectedtimestamp"] = PostgresUtil.TimestampTz("obu_detectedtimestamp", payload.additionalInfo.detectionTimestamp),
                    ["txn_json"] = PostgresUtil.Jsonb("txn_json", payload)
                };

                var result = await _postgresService.ExecuteAsync(sql, parameters);

                if (result.RowsAffected > 0)
                {
                    _logger.LogInformation("OBU transaction inserted successfully.");
                    return true;
                }
                else
                {
                    _logger.LogWarning("OBU transaction insert returned 0 rows affected.");
                    return false;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to insert OBU transaction.");
                return false;
            }
        }

    }
}
