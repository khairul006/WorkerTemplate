using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using WorkerTemplate.Configs;
using WorkerTemplate.Converters;
using WorkerTemplate.Models;
using WorkerTemplate.Providers;
using WorkerTemplate.Utils;

namespace WorkerTemplate.Services
{
    public class TxnService
    {
        private readonly ILogger<TxnService> _logger;
        private readonly RabbitMQService _rabbitmqService;
        private readonly PostgresService _postgresService;
        private readonly HashSettings _hashSettings;
        private readonly MerchantSettings _merchantSettings;

        public TxnService(
            RabbitMQService rabbitmqService,
            PostgresService postgresService,
            IOptions<HashSettings> hashOptions,
            IOptions<MerchantSettings> merchantOptions,
            ILogger<TxnService> logger
        )
        {
            _rabbitmqService = rabbitmqService;
            _postgresService = postgresService;
            _hashSettings = hashOptions.Value;
            _merchantSettings = merchantOptions.Value;
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

        // Template for publishing transaction message to RabbitMQ
        public async Task<bool> PublishTxnAsync(TxnMsg payload)
        {
            try
            {
                string sql = @"
                    SELECT token FROM token_table
                    WHERE id = @id
                    LIMIT 1;
                ";
                var parameters = new Dictionary<string, object?>
                {
                    ["id"] = payload.body.transactionId,
                };
                var result = await _postgresService.ExecuteAsync(sql, parameters);

                string? tokenId = null;
                if (result.Rows.Rows.Count > 0)
                {
                    tokenId = result.Rows.Rows[0]["token_id"]?.ToString();
                }

                var txnBody = new TxnPubMsg.Body
                {
                    transactionId = payload.body.transactionId,
                    entryTimestamp = payload.body.entryTimestamp,
                    vehicleClass = payload.body.vehicleClass?.PadLeft(2, '0'),
                    exitTimestamp = payload.body.exitTimestamp,
                };

                string signature = string.Empty;

                if (_hashSettings.EnableHash)
                {
                    if (string.IsNullOrWhiteSpace(_hashSettings.SecretKey))
                        throw new InvalidOperationException(
                            "EnableHash is true but SecretKey is not configured.");

                    // Serialize to compact JSON string
                    // Default options = no whitespace, no indentation.
                    string compactJson = JsonSerializer.Serialize(
                        txnBody,
                        RmqJsonOptions
                    );

                    signature = HashUtil.GenerateSignature(compactJson, _hashSettings.SecretKey);
                }

                var txnPubObj = new TxnPubMsg
                {
                    header = new TxnPubMsg.Header
                    {
                        timestamp = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)), // current process timestamp in +08:00
                    },
                    body = txnBody,
                    signature = signature
                };

                bool success = await _rabbitmqService.PublishAsync(
                    message: JsonSerializer.Serialize(
                        txnPubObj,
                        RmqJsonOptions
                    )
                );

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process transaction: {transactionId}", payload.body.transactionId);
                return false;
            }
        }

        // Template for subscribing transaction response message from RabbitMQ
        public async Task<bool> SubscribeTxnRspAsync(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                throw new ArgumentException("Raw JSON cannot be empty", nameof(rawJson));

            var payload = JsonSerializer.Deserialize<TxnRspMsg>(rawJson);
            if (payload == null)
            {
                _logger.LogWarning("Failed to deserialize payload: {rawJson}", rawJson);
                return false;
            }

            try
            {
                // Extract body as JSON string
                using JsonDocument doc = JsonDocument.Parse(rawJson);
                string bodyJson = doc.RootElement.GetProperty("body").GetRawText();

                if (_hashSettings.EnableHash)
                {
                    if (string.IsNullOrWhiteSpace(_hashSettings.SecretKey))
                        throw new InvalidOperationException(
                            "EnableHash is true but SecretKey is not configured.");

                    if (string.IsNullOrWhiteSpace(payload.signature))
                        throw new ArgumentException(
                            "Signature not provided in the message.",
                            nameof(payload.signature));

                    // get signature from msg
                    string signature = payload.signature;
                    //Console.WriteLine("Signature in msg: " + signature);

                    // Verify signature
                    bool isValid = HashUtil.VerifySignature(bodyJson, _hashSettings.SecretKey, signature);
                    //Console.WriteLine("Signature valid? " + isValid);

                    if (!isValid)
                    {
                        // Log and drop the message if signature verification fails
                        _logger.LogWarning("Signature verification failed for Response {transactionId}. Message dropped.", payload?.body?.transactionId);
                        return true;
                    }
                }

                var txnRspSubObj = payload;

                // publish rawJson as what being received
                bool success = await _rabbitmqService.PublishAsync(
                    message: rawJson
                );
                _logger.LogInformation("Subscribed Response {transactionId} json success: {success}", payload?.body?.transactionId, success);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Response {transactionId}.", payload?.body?.transactionId);
                return false;
            }
        }

        // Template for inserting transaction message into Postgres
        public async Task<bool> ProcessTxnMessageAsync(TxnMsg payload)
        {
            try
            {
                string sql = @"
                    INSERT INTO public.txn(
                        txn_date,
                        txn_id,
                        vehicle_class,
                        entry_timestamp,
                        exit_timestamp
                    ) 
                    VALUES (
                        @txn_date,
                        @txn_id,
                        @vehicle_class,
                        @entry_timestamp,
                        @exit_timestamp
                    )
                    ON CONFLICT (txn_id)
                    DO UPDATE SET
                        txn_date = EXCLUDED.txn_date,
                        txn_id = EXCLUDED.txn_id,
                        vehicle_class = EXCLUDED.vehicle_class,
                        entry_timestamp = EXCLUDED.entry_timestamp,
                        exit_timestamp = EXCLUDED.exit_timestamp;
                ";

                // Map payload to parameters
                var parameters = new Dictionary<string, object?>
                {
                    ["txn_date"] = PostgresUtil.Date("txn_date", payload.body.exitTimestamp),
                    ["txn_id"] = payload.body.transactionId,
                    ["entry_timestamp"] = PostgresUtil.TimestampTz("entry_timestamp", payload.body.entryTimestamp),
                    ["txn_date_time"] = PostgresUtil.TimestampTz("txn_date_time", payload.body.exitTimestamp),
                    ["vehicle_class"] = payload.body.vehicleClass,
                    ["txn_json"] = PostgresUtil.Jsonb("txn_json", payload)
                };

                var result = await _postgresService.ExecuteAsync(sql, parameters);

                if (result.RowsAffected > 0)
                {
                    _logger.LogInformation("Transaction {transactionId} inserted successfully.", payload.body.transactionId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Transaction insert {transactionId} returned 0 rows affected.", payload.body.transactionId);
                    return false;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to insert transaction.");
                return false;
            }
        }

    }
}
