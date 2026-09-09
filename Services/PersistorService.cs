using System.Text.Encodings.Web;
using System.Text.Json;
using WorkerTemplate.Converters;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;

namespace WorkerTemplate.Services;

public class PersistorService : IPersistorService
{
    private readonly ILogger<PersistorService> _logger;
    private readonly IPostgresService _postgresService;

    public PersistorService(
        IPostgresService postgresService,
        ILogger<PersistorService> logger
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

    public async Task<RabbitmqHandlerResult> SaveToDbAsync(
        DemoModel payload,
        int retryCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string sql = @"
                INSERT INTO table_demo(                        
                    txn_date, transaction_id, transaction_amount, exit_timestamp,
                    common_trx_no, mbb_rsp_code,
                    mbb_latest_rsp_json, mbb_rsp_json, mbb_rsp_timestamp
                ) 
                VALUES (
                    @txn_date, @transaction_id, @transaction_amount, @exit_timestamp,
                    @common_trx_no, @mbb_rsp_code,
                    @mbb_latest_rsp_json::jsonb, 
                    CASE 
                        WHEN @mbb_rsp_json IS NULL THEN NULL::jsonb 
                        ELSE jsonb_build_array(@mbb_rsp_json::jsonb) 
                    END, 
                    @mbb_rsp_timestamp
                )
                ON CONFLICT (txn_date, transaction_id)
                DO UPDATE SET
                    transaction_code = EXCLUDED.transaction_code,
                    transaction_amount = EXCLUDED.transaction_amount,
                    exit_timestamp = EXCLUDED.exit_timestamp,
                    common_trx_no = EXCLUDED.common_trx_no,
                    mbb_rsp_code = EXCLUDED.mbb_rsp_code,
                    mbb_latest_rsp_json = EXCLUDED.mbb_latest_rsp_json,
                    mbb_rsp_json = CASE 
                        WHEN @mbb_rsp_json IS NULL THEN alpr_txn.mbb_rsp_json
                        ELSE COALESCE(alpr_txn.mbb_rsp_json, '[]'::jsonb) || EXCLUDED.mbb_rsp_json
                    END,
                    mbb_rsp_timestamp = EXCLUDED.mbb_rsp_timestamp;
            ";

            // Map payload to parameters
            var parameters = new
            {
                txn_date = payload.exitTimestamp.Date,
                transaction_id = payload.transactionId,
                transaction_amount = payload.transactionAmount,
                exit_timestamp = payload.exitTimestamp.ToLocalTime(),
                common_trx_no = payload.commonTrxNo,
                mbb_rsp_code = payload.responseCode,
                mbb_latest_rsp_json = JsonSerializer.Serialize(payload, RmqJsonOptions),
                mbb_rsp_json = JsonSerializer.Serialize(payload, RmqJsonOptions),
                mbb_rsp_timestamp = payload.datetime.LocalDateTime
            };

            var rowsInserted = await _postgresService.ExecuteAsync(sql, parameters, cancellationToken);

            if (rowsInserted > 0)
            {
                _logger.LogInformation("Successfully persisted transaction. transactionId={transactionId}", payload.transactionId);
                return new RabbitmqHandlerResult
                {
                    Action = MessageAction.Ack
                };
            }
            else
            {
                _logger.LogWarning("Failed to persist transaction (0 rows affected). transactionId={transactionId}", payload.transactionId);
                return new RabbitmqHandlerResult
                {
                    Action = MessageAction.Nack
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist transaction. Requeue message. transactionId={transactionId}", payload.transactionId);
            return new RabbitmqHandlerResult
            {
                Action = MessageAction.Nack
            };
        }
    }

}
