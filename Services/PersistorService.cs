using System.Text.Encodings.Web;
using System.Text.Json;
using WorkerTemplate.Converters;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;
using WorkerTemplate.Providers;

namespace WorkerTemplate.Services
{
    public class PersistorService : IPersistorService
    {
        private readonly ILogger<PersistorService> _logger;
        private readonly IPostgresService _postgresService;
        private readonly ElasticSearchService _elasticSearchService;

        public PersistorService(
            IPostgresService postgresService,
            ElasticSearchService elasticSearchService,
            ILogger<PersistorService> logger
        )
        {
            _postgresService = postgresService;
            _elasticSearchService = elasticSearchService;
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
            TxnMBBResponsePst payload, 
            int retryCount,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Log to elasticsearch
                await LogToElasticAsync(payload, cancellationToken);

                string sql = @"
                    INSERT INTO alpr_txn(                        
                        txn_date, oper_date, transaction_id, transaction_code, transaction_type,
                        transaction_amount, media_id, media_token_id, lpr_id, acc_id,
                        acc_type, entry_timestamp, entry_spid, entry_plazaid, entry_laneid,
                        entry_class, exit_timestamp, exit_spid, exit_plazaid, exit_laneid,
                        exit_class, parameter_timestamp, operation_mode, boj_datetime, tc_badgeno,
                        job_type, job_no, common_trxno, trx_no, anpr_plateno,
                        journey_exceptioncode, fare_type, fare_groupid, fare_flexiid, fare_plaza,
                        fare_amount, detection_timestamp, completion_timestamp, txn_json,
                        -- Maybank request additional fields
                        money_amount, decimal_places, money_value, money_value_2, money_value_total, 
                        ttype_description, entry_sp_name, entry_plaza_name, exit_sp_name, exit_plaza_name,
                        product_id, promotion_codes, supplier_label, partner_label, account_type,
                        sys_timestamp, mbb_request_json,
                        -- Maybank response,
                        mbb_correlation_id, mbb_merchant_account_id, mbb_rsp_code, mbb_rsp_desc,
                        mbb_latest_rsp_json, mbb_rsp_json, mbb_rsp_timestamp
                    ) 
                    VALUES (
                        @txn_date, @oper_date, @transaction_id, @transaction_code, @transaction_type,
                        @transaction_amount, @media_id, @media_token_id, @lpr_id, @acc_id,
                        @acc_type, @entry_timestamp, @entry_spid, @entry_plazaid, @entry_laneid,
                        @entry_class, @exit_timestamp, @exit_spid, @exit_plazaid, @exit_laneid,
                        @exit_class, @parameter_timestamp, @operation_mode, @boj_datetime, @tc_badgeno,
                        @job_type, @job_no, @common_trxno, @trx_no, @anpr_plateno,
                        @journey_exceptioncode, @fare_type, @fare_groupid, @fare_flexiid, @fare_plaza,
                        @fare_amount, @detection_timestamp, @completion_timestamp, @txn_json::jsonb,

                        @money_amount, @decimal_places, @money_value, @money_value_2, @money_value_total, 
                        @ttype_description, @entry_sp_name, @entry_plaza_name, @exit_sp_name, @exit_plaza_name,
                        @product_id, @promotion_codes, @supplier_label, @partner_label, @account_type,
                        @sys_timestamp, @mbb_request_json::jsonb,

                        @mbb_correlation_id, @mbb_merchant_account_id, @mbb_rsp_code, @mbb_rsp_desc,
                        @mbb_latest_rsp_json::jsonb, 
                        CASE 
                            WHEN @mbb_rsp_json IS NULL THEN NULL::jsonb 
                            ELSE jsonb_build_array(@mbb_rsp_json::jsonb) 
                        END, 
                        @mbb_rsp_timestamp
                    )
                    ON CONFLICT (txn_date, transaction_id)
                    DO UPDATE SET
                        oper_date = EXCLUDED.oper_date,
                        transaction_code = EXCLUDED.transaction_code,
                        transaction_type = EXCLUDED.transaction_type,
                        transaction_amount = EXCLUDED.transaction_amount,
                        media_id = EXCLUDED.media_id,
                        media_token_id = EXCLUDED.media_token_id,
                        lpr_id = EXCLUDED.lpr_id,
                        acc_id = EXCLUDED.acc_id,
                        acc_type = EXCLUDED.acc_type,
                        entry_timestamp = EXCLUDED.entry_timestamp,
                        entry_spid = EXCLUDED.entry_spid,
                        entry_plazaid = EXCLUDED.entry_plazaid,
                        entry_laneid = EXCLUDED.entry_laneid,
                        entry_class = EXCLUDED.entry_class,
                        exit_timestamp = EXCLUDED.exit_timestamp,
                        exit_spid = EXCLUDED.exit_spid,
                        exit_plazaid = EXCLUDED.exit_plazaid,
                        exit_laneid = EXCLUDED.exit_laneid,
                        exit_class = EXCLUDED.exit_class,
                        parameter_timestamp = EXCLUDED.parameter_timestamp,
                        operation_mode = EXCLUDED.operation_mode,
                        boj_datetime = EXCLUDED.boj_datetime,
                        tc_badgeno = EXCLUDED.tc_badgeno,
                        job_type = EXCLUDED.job_type,
                        job_no = EXCLUDED.job_no,
                        common_trxno = EXCLUDED.common_trxno,
                        trx_no = EXCLUDED.trx_no,
                        anpr_plateno = EXCLUDED.anpr_plateno,
                        journey_exceptioncode = EXCLUDED.journey_exceptioncode,
                        fare_type = EXCLUDED.fare_type, 
                        fare_groupid = EXCLUDED.fare_groupid,
                        fare_flexiid = EXCLUDED.fare_flexiid,
                        fare_plaza = EXCLUDED.fare_plaza,
                        fare_amount = EXCLUDED.fare_amount,
                        detection_timestamp = EXCLUDED.detection_timestamp,
                        completion_timestamp = EXCLUDED.completion_timestamp,
                        txn_json = EXCLUDED.txn_json,
                        money_amount = EXCLUDED.money_amount,
                        decimal_places = EXCLUDED.decimal_places,
                        money_value = EXCLUDED.money_value,
                        money_value_2 = EXCLUDED.money_value_2,
                        money_value_total = EXCLUDED.money_value_total,
                        ttype_description = EXCLUDED.ttype_description,
                        entry_sp_name = EXCLUDED.entry_sp_name,
                        entry_plaza_name = EXCLUDED.entry_plaza_name,
                        exit_sp_name = EXCLUDED.exit_sp_name,
                        exit_plaza_name = EXCLUDED.exit_plaza_name,
                        product_id = EXCLUDED.product_id, 
                        promotion_codes = EXCLUDED.promotion_codes,
                        supplier_label = EXCLUDED.supplier_label,
                        partner_label = EXCLUDED.partner_label,
                        account_type = EXCLUDED.account_type,
                        sys_timestamp = EXCLUDED.sys_timestamp,
                        mbb_request_json = EXCLUDED.mbb_request_json,

                        mbb_correlation_id = EXCLUDED.mbb_correlation_id,
                        mbb_merchant_account_id = EXCLUDED.mbb_merchant_account_id,
                        mbb_rsp_code = EXCLUDED.mbb_rsp_code,
                        mbb_rsp_desc = EXCLUDED.mbb_rsp_desc,
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
                    txn_date = payload.txnPayload.body.exitTimestamp.Date,
                    oper_date = payload.txnPayload.additionalInfo.operationalDate.Date,
                    transaction_id = payload.txnPayload.body.transactionId,
                    transaction_code = payload.txnPayload.body.transactionCode,
                    transaction_type = payload.txnPayload.body.transactionType,
                    transaction_amount = decimal.TryParse(payload.txnPayload.body.transactionAmount?.ToString(), out var transactionAmount) ? transactionAmount : 0,
                    media_id = payload.txnPayload.body.mediaID,
                    media_token_id = payload.txnPayload.body.mediaTokenId,
                    lpr_id = payload.txnPayload.body.LPRId,
                    acc_id = payload.txnPayload.body.accId.Trim(),
                    acc_type = payload.txnPayload.body.accType,
                    entry_timestamp = payload.txnPayload.body.entryTimestamp.DateTime,
                    entry_spid = payload.txnPayload.body.entrySPId,
                    entry_plazaid = payload.txnPayload.body.entryPlazaId,
                    entry_laneid = payload.txnPayload.body.entryLaneId,
                    entry_class = payload.txnPayload.body.entryClass,
                    exit_timestamp = payload.txnPayload.body.exitTimestamp.DateTime,
                    exit_spid = payload.txnPayload.body.exitSPId,
                    exit_plazaid = payload.txnPayload.body.exitPlazaId,
                    exit_laneid = payload.txnPayload.body.exitLaneId,
                    exit_class = payload.txnPayload.body.exitClass,
                    parameter_timestamp = payload.txnPayload.body.parameterTimestamp.DateTime,
                    operation_mode = payload.txnPayload.additionalInfo.operationMode,
                    boj_datetime =  payload.txnPayload.additionalInfo.bojDateTime.DateTime,
                    tc_badgeno = payload.txnPayload.additionalInfo.tcBadgeNo,
                    job_type = payload.txnPayload.additionalInfo.jobType,
                    job_no = payload.txnPayload.additionalInfo.jobNo,
                    common_trxno = payload.txnPayload.additionalInfo.commonTrxNo,
                    trx_no = payload.txnPayload.additionalInfo.trxNo,
                    anpr_plateno = payload.txnPayload.additionalInfo.ANPRPlateNo,
                    journey_exceptioncode = payload.txnPayload.additionalInfo.journeyExceptionCode,
                    fare_type = payload.txnPayload.additionalInfo.fareType,
                    fare_groupid = payload.txnPayload.additionalInfo.fareGroupId,
                    fare_flexiid = payload.txnPayload.additionalInfo.fareFlexiId,
                    fare_plaza = payload.txnPayload.additionalInfo.farePlaza,
                    fare_amount = decimal.TryParse(payload.txnPayload.additionalInfo.fareAmount?.ToString(), out var fareAmount) ? fareAmount : 0,
                    detection_timestamp = payload.txnPayload.additionalInfo.detectionTimestamp.DateTime,
                    completion_timestamp = payload.txnPayload.additionalInfo.completionTimestamp.DateTime,
                    sys_timestamp = DateTimeOffset.Now.DateTime,
                    txn_json = JsonSerializer.Serialize(payload.txnPayload, RmqJsonOptions),

                    money_amount = payload.mbbRequest?.amount,
                    decimal_places = payload.mbbRequest?.decimalPlaces,
                    money_value = payload.mbbRequest?.metadata.moneyValue,
                    money_value_2 = payload.mbbRequest?.metadata.moneyValue2,
                    money_value_total = payload.mbbRequest?.metadata.moneyValueTotal,
                    ttype_description = payload.mbbRequest?.metadata.ttypeDescription,
                    entry_sp_name = payload.mbbRequest?.metadata.entrySpName,
                    entry_plaza_name = payload.mbbRequest?.metadata.entryPlazaName,
                    exit_sp_name = payload.mbbRequest?.metadata.exitSpName,
                    exit_plaza_name = payload.mbbRequest?.metadata.exitPlazaName,
                    product_id = payload.mbbRequest?.metadata.productId,
                    promotion_codes = payload.mbbRequest?.metadata.promotionCodes,
                    supplier_label = payload.mbbRequest?.metadata.supplierLabel,
                    partner_label = payload.mbbRequest?.metadata.partnerLabel,
                    account_type = payload.mbbRequest?.metadata.accountType,
                    mbb_request_json = payload.mbbRequest is null ? null : JsonSerializer.Serialize(payload.mbbRequest, RmqJsonOptions),

                    mbb_correlation_id = payload.mbbResponse?.correlationId,
                    mbb_merchant_account_id = payload.mbbResponse?.merchantAccountId,
                    mbb_rsp_code = payload.mbbResponse?.responseCode,
                    mbb_rsp_desc = payload.mbbResponse?.responseDesc,
                    mbb_latest_rsp_json = payload.mbbResponse is null ? null : JsonSerializer.Serialize(payload.mbbResponse, RmqJsonOptions),
                    mbb_rsp_json = payload.mbbResponse is null ? null : JsonSerializer.Serialize(payload.mbbResponse, RmqJsonOptions),
                    mbb_rsp_timestamp = payload.mbbResponse?.datetime.LocalDateTime
                };

                var rowsInserted = await _postgresService.ExecuteAsync(sql, parameters, cancellationToken);

                if (rowsInserted > 0)
                {
                    _logger.LogInformation("Successfully persisted ALPR transaction. transactionId={transactionId}", payload.txnPayload.body.transactionId);
                    return new RabbitmqHandlerResult
                    {
                        Action = MessageAction.Ack
                    };
                }
                else
                {
                    _logger.LogWarning("Failed to persist ALPR transaction (0 rows affected). transactionId={transactionId}", payload.txnPayload.body.transactionId);
                    return new RabbitmqHandlerResult
                    {
                        Action = MessageAction.Nack
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist ALPR transaction. Requeue message. transactionId={transactionId}", payload.txnPayload.body.transactionId);
                return new RabbitmqHandlerResult
                {
                    Action = MessageAction.Nack
                };
            }
        }

        private async Task LogToElasticAsync(TxnMBBResponsePst payload, CancellationToken cancellationToken)
        {
            try
            {
                var transactionId = payload.txnPayload.body.transactionId;
                var timestamp = DateTimeOffset.UtcNow;

                // Build the base upsert dictionary (used only when the document is created for the first time)
                var upsert = new Dictionary<string, object?>
                {
                    ["@timestamp"] = timestamp,
                    ["sdp"] = payload.txnPayload.sdp ?? (object)new { },
                    ["header"] = payload.txnPayload?.header,
                    ["body"] = payload.txnPayload?.body,
                    ["additionalInfo"] = payload.txnPayload?.additionalInfo
                };

                // Conditionally include "mbb" only when mbbRequest exists
                if (payload.mbbRequest is not null)
                {
                    upsert["mbb"] = new Dictionary<string, object?>
                    {
                        ["latestResponse"] = payload.mbbResponse,
                        ["requests"] = new[] { payload.mbbRequest },
                        ["responses"] = payload.mbbResponse is not null
                            ? new[] { payload.mbbResponse }
                            : Array.Empty<TxnLPPMBBResponse>()
                    };
                }

                var updatePayload = new
                {
                    script = new
                    {
                        source = """
                            // 1. Always refresh the top-level fields on every update (not just on insert)
                            ctx._source['@timestamp'] = params.timestamp;
                            if (params.sdp != null) {
                                ctx._source.sdp = params.sdp;
                            }
                            if (params.header != null) {
                                ctx._source.header = params.header;
                            }
                            if (params.body != null) {
                                ctx._source.body = params.body;
                            }
                            if (params.additionalInfo != null) {
                                ctx._source.additionalInfo = params.additionalInfo;
                            }

                            // 2. Initialize mbb object if it doesn't exist
                            if (ctx._source.mbb == null) {
                                ctx._source.mbb = [:];
                            }
                            // 3. Initialize requests list if it doesn't exist, then append
                            if (ctx._source.mbb.requests == null) {
                                ctx._source.mbb.requests = new ArrayList();
                            }
                            if (params.new_request != null) {
                                ctx._source.mbb.requests.add(params.new_request);
                            }
                            // 4. Initialize responses list if it doesn't exist, then append
                            if (ctx._source.mbb.responses == null) {
                                ctx._source.mbb.responses = new ArrayList();
                            }
                            if (params.new_response != null) {
                                ctx._source.mbb.responses.add(params.new_response);
                                ctx._source.mbb.latestResponse = params.new_response;
                            }
                        """,
                        lang = "painless",
                        @params = new
                        {
                            timestamp = timestamp,
                            sdp = payload.txnPayload?.sdp,
                            header = payload.txnPayload?.header,
                            body = payload.txnPayload?.body,
                            additionalInfo = payload.txnPayload?.additionalInfo,
                            new_request = payload.mbbRequest,
                            new_response = payload.mbbResponse
                        }
                    },
                    upsert = upsert
                };

                var (data, status) = await _elasticSearchService.UpdateDocumentAsync(
                    id: transactionId,
                    updateBody: updatePayload
                );

                _logger.LogInformation("Logged to Elasticsearch. transactionId={transactionId}, status={status}", transactionId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log to Elasticsearch. transactionId={transactionId}", payload?.txnPayload?.body?.transactionId);
                // continue to next. Don't let one failure stop the rest
            }
        }

    }

}
