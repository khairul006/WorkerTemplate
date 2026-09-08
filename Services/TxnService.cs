using Jose;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using WorkerTemplate.Configs;
using WorkerTemplate.Converters;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Models;
using WorkerTemplate.Providers;
using WorkerTemplate.Utils;

namespace WorkerTemplate.Services;

public class TxnService : ITxnService
{
    private readonly ILogger<TxnService> _logger;
    private readonly IRabbitMQService _rabbitmqService;
    private readonly IRedisService _redisService;
    private readonly IPostgresService _postgresService;
    private readonly SecuritySettings _securitySettings;
    private readonly ApplicationSettings _applicationSettings;
    private readonly RabbitMQSettings _queueSettings;
    private readonly HttpClient _httpClient;

    private static readonly HashSet<string> AllowedTransactionTypes =
        [
            "C","TP","B","SU","BR","BU"
        ];

    private static readonly HashSet<string> OpenSystemTollType =
        [
            "B", "BR", "BU"
        ];

    private static readonly HashSet<string> JKSBExitPlazaIds =
        [
            "138", "139"
        ];

    public TxnService(
        IRabbitMQService rabbitmqService,
        IRedisService redisService,
        IPostgresService postgresService,
        HttpClient httpClient,
        IOptions<SecuritySettings> securityOptions,
        IOptions<ApplicationSettings> applicationOptions,
        IOptions<RabbitMQSettings> queueOptions,
        ILogger<TxnService> logger
    )
    {
        _rabbitmqService = rabbitmqService;
        _redisService = redisService;
        _postgresService = postgresService;
        _httpClient = httpClient;
        _securitySettings = securityOptions.Value;
        _applicationSettings = applicationOptions.Value;
        _queueSettings = queueOptions.Value;
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

    // Define retry policies for specific MBB API response codes
    public static readonly Dictionary<string, RetryPolicy> RetryPolicies = new()
    {
        // Retry after 5,10,30 seconds with exponential back-off.Maximum 3 retries.
        ["MP20002"] = new RetryPolicy
        {
            DelaysMs = new[] { 5000, 10000, 30000 }
        },
        // Retry once after 30 seconds.
        ["MP10002"] = new RetryPolicy
        {
            DelaysMs = new[] { 30000 }
        }
    };


    // Processing transaction message from RabbitMQ
    public async Task<RabbitmqHandlerResult> ProcessLPPMessageAsync(TxnLPPMsg payload, int retryCount)
    {
        try
        {
            // Guard clause. Ignore unallowed transaction types early
            if (!AllowedTransactionTypes.Contains(payload.body.transactionType))
            {
                _logger.LogInformation("Non-paying transaction message. TransactionType={transactionType}. TransactionId={transactionId}",
                    payload.body.transactionType, payload.body.transactionId);

                // Publish to persistor queue to save into database
                await PublishTxnResponseAsync(payload, null, null);

                // ack and drop
                return new RabbitmqHandlerResult
                {
                    Action = MessageAction.Ack
                };
            }

            // Build the outbound MBB payload object
            var txnPayload = await BuildMbbPayloadAsync(payload);

            var serializedPayload = JsonSerializer.Serialize(txnPayload, RmqJsonOptions);
            _logger.LogInformation("MBB Charge - PAYLOAD - {serializedPayload}", serializedPayload);

            var encryptedPayload = SecurityUtil.SignThenEncrypt(
                serializedPayload,
                _securitySettings.Mbb.SigningKey,
                _securitySettings.Mbb.EncryptionKey
            );

            var mbbRequest = new TxnLPPMBBRequest
            {
                transactionReference = txnPayload.transactionReference,
                payload = encryptedPayload
            };

            // Call POST request to MBB API
            var response = await SubmitTollCharge(mbbRequest);

            // publish txn/response to Persistor Queue
            await PublishTxnResponseAsync(payload, txnPayload, response);

            // Handle specific MBB API response code for retry logic
            if (RetryPolicies.TryGetValue(response.responseCode, out var policy))
            {
                _logger.LogWarning("MBB API POST return error code. TransactionId={transactionId} Code={responseCode} Desc={responseDesc}", payload.body.transactionId, response.responseCode, response.responseDesc);
                //Console.WriteLine($"Policy: {JsonSerializer.Serialize(policy)}");

                if (retryCount >= policy.DelaysMs.Length)
                {
                    return new RabbitmqHandlerResult
                    {
                        Action = MessageAction.Dead
                    };
                }

                return new RabbitmqHandlerResult
                {
                    Action = MessageAction.Retry,
                    RetryDelayMs = policy.DelaysMs[retryCount]
                };
            }

            // If successful, return true to acknowledge the message
            _logger.LogInformation("Successfully processed transaction. transactionId={transactionId}", payload.body.transactionId);
            return new RabbitmqHandlerResult
            {
                Action = MessageAction.Ack
            };

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process transaction. Requeue message. transactionId={transactionId}", payload.body.transactionId);
            return new RabbitmqHandlerResult
            {
                Action = MessageAction.Nack
            };
        }
    }

    /// <summary>
    /// Handles all fare calculation, apportionment lookup, and metadata assembly.
    /// </summary>
    private async Task<TxnLPPMBBPayload> BuildMbbPayloadAsync(TxnLPPMsg payload)
    {
        try
        {
            // Parse transaction amount once safely
            if (!decimal.TryParse(payload.body.transactionAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount))
            {
                _logger.LogWarning("Invalid transaction amount format: {Amount}", payload.body.transactionAmount);
            }

            decimal moneyValue;
            decimal? moneyValue2 = null;
            decimal? moneyValueTotal = parsedAmount;
            string supplierLabel;
            string? partnerLabel = null;

            // Handle JKSB vs Non-JKSB Fare Apportionment
            if (JKSBExitPlazaIds.Contains(payload.body.exitPlazaId))
            {
                _logger.LogInformation("Calling Fare API. PlazaId={PlazaId} TransactionId={TransactionId}",
                    payload.body.exitPlazaId, payload.body.transactionId);

                // Build POST request body for Fare API
                var fareRequest = new PostFareRequest
                {
                    serialNum = payload.header.serialNum,
                    entryPlazaId = payload.body.entryPlazaId ?? payload.additionalInfo.farePlaza,
                    exitPlazaId = payload.body.exitPlazaId,
                    exitSPId = payload.body.exitSPId,
                    exitTimestamp = payload.body.exitTimestamp.ToString("o", CultureInfo.InvariantCulture),
                    vehicleClass = payload.body.exitClass.PadLeft(2, '0'),
                    groupId = payload.additionalInfo.fareGroupId,
                    flexiId = payload.additionalInfo.fareFlexiId
                };

                var fareResponse = await GetFareApportionment(fareRequest);
                _logger.LogInformation("Fare API response: {Response}", JsonSerializer.Serialize(fareResponse));

                // Group apportionment fares in a single lookup pass
                var apportionmentLookup = fareResponse.data?.apportionment?.ToLookup(x => x.spid, x => x.fare);

                moneyValue = apportionmentLookup?["48"].Sum() ?? 0m;
                moneyValue2 = apportionmentLookup?["04"].Sum() ?? 0m;
                supplierLabel = "JKSB";
                partnerLabel = "PLUS";
            }
            else
            {
                moneyValue = parsedAmount;
                moneyValueTotal = null;
                supplierLabel = _applicationSettings.SupplierLabel.Trim().ToUpperInvariant();
            }

            // Determine product ID and promotion code
            var productId = OpenSystemTollType.Contains(payload.body.transactionType)
                ? "JG_ANPR_OS"
                : "JG_ANPR_CS";

            var promotionCodes = (parsedAmount == 0m && !(payload.body.transactionType == "SU" && payload.body.exitPlazaId == payload.body.entryPlazaId))
                ? "ZERO_FARE"
                : payload.additionalInfo.farePlaza == "997"
                    ? "JPP"
                    : null;

            // Cache pre-trimmed variables
            var customerAccId = payload.body.accId.Trim();
            var mediumId = payload.body.mediaID.Trim();
            var exitClassPadded = payload.body.exitClass.PadLeft(2, '0');

            var (moneyAmount, decimalPlaces) = MoneyUtil.ToMinorUnits(payload.body.transactionAmount);
            var accountType = await GetJustgoAccountType(mediumId, payload.body.exitSPId);

            return new TxnLPPMBBPayload
            {
                merchantAccountId = _applicationSettings.MerchantId,
                transactionReference = payload.body.transactionId,
                customerReference = customerAccId,
                vehicleReference = mediumId,
                transactionTimestamp = payload.body.exitTimestamp.ToUniversalTime(),
                amount = moneyAmount,
                decimalPlaces = decimalPlaces,
                currencyCode = "MYR",
                metadata = new TxnLPPMBBPayload.Metadata
                {
                    moneyValue = moneyValue,
                    moneyValue2 = moneyValue2,
                    moneyValueTotal = moneyValueTotal,
                    transactionCode = payload.body.transactionId,
                    transactionTimestamp = payload.body.exitTimestamp,
                    customerId = customerAccId,
                    mediumId = mediumId,
                    salesChannelId = exitClassPadded,
                    ttype = payload.body.transactionType,
                    ttypeDescription = TransactionUtil.GetTtypeDescription(payload.body.transactionType),
                    productId = productId,
                    promotionCodes = promotionCodes,
                    entrySpid = payload.body.entrySPId,
                    entrySpName = TransactionUtil.GetSpName(payload.body.entrySPId),
                    entryPlaza = payload.body.entryPlazaId,
                    entryPlazaName = null,
                    entryLane = payload.body.entryLaneId,
                    entryDatetime = payload.body.entryTimestamp,
                    exitSpid = payload.body.exitSPId,
                    exitSpName = TransactionUtil.GetSpName(payload.body.exitSPId),
                    exitPlaza = payload.body.exitPlazaId,
                    exitPlazaName = null,
                    exitLane = payload.body.exitLaneId,
                    exitDatetime = payload.body.exitTimestamp,
                    supplierLabel = supplierLabel,
                    partnerLabel = partnerLabel,
                    accountType = accountType,
                    siLabel = "TERAS"
                }
            };

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build MBB payload");
            throw;
        }
    }


    private async Task<string?> GetJustgoAccountType(string plateNumber, string exitSpid)
    {
        try
        {
            // for JPP (exitSpid=02) use RedisKeyLPPJPP, otherwise use RedisKeyLPP
            var redisKeyLPP = exitSpid == "02" ? _applicationSettings.RedisKeyLPPJPP : _applicationSettings.RedisKeyLPP;
            var paramValue = await _redisService.HashGetAsync(redisKeyLPP, plateNumber);

            if (paramValue != null)
            {
                var parts = paramValue.Split(';');
                var accountType = parts.Length > 9 ? parts[9] : null;

                _logger.LogInformation(
                    "GetJustgoAccountType - REDIS - PlateNumber={PlateNumber}, AccountType={AccountType}",
                    plateNumber, accountType);

                return accountType;
            }

            const string sql = """
                SELECT account_type
                FROM vehicle_parameter
                WHERE plate_number = @PlateNumber
             """;

            var accountTypeFromDb = (await _postgresService.QueryAsync<string>(
                sql,
                new { PlateNumber = plateNumber }))
                .FirstOrDefault();

            _logger.LogInformation(
                "GetJustgoAccountType - POSTGRES - PlateNumber={PlateNumber}, AccountType={AccountType}",
                plateNumber, accountTypeFromDb);

            return accountTypeFromDb;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account type for plate number {plateNumber}", plateNumber);
            throw;
        }
    }


    private async Task PublishTxnResponseAsync(TxnLPPMsg payload, TxnLPPMBBPayload? request, TxnLPPMBBResponse? response)
    {
        try
        {
            var txnAndResponse = new TxnMBBResponsePst
            {
                txnPayload = payload,
                mbbRequest = request,
                mbbResponse = response
            };

            // Serualized with timestamp format include miliseconds
            var serializedMsg = JsonSerializer.Serialize(txnAndResponse, RmqJsonOptions);

            await _rabbitmqService.PublishAsync(
                _queueSettings.Default.Exchanges["Persistor"],
                serializedMsg);
        }
        catch (Exception ex)
        {
            // log it properly
            _logger.LogError(ex, "Failed to publish txn/response to RabbitMQ. TransactionId: {transactionId}",
                payload.body.transactionId);
            throw;
        }
    }


    public async Task<HttpResponse<PostFareData>> GetFareApportionment(PostFareRequest requestBody, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = $"{_applicationSettings.FareServiceApi?.Trim()}/api/fare";

            using var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Fare API error {(int)response.StatusCode}: {responseBody}");
            }

            var result = JsonSerializer.Deserialize<HttpResponse<PostFareData>>(responseBody);
            return result ?? throw new InvalidOperationException($"Fare API [{endpoint}] returned null or empty response");
        }
        catch (HttpRequestException ex)
        {
            // Log and rethrow or handle
            throw new HttpRequestException($"Get fare apportionment failed: {ex.Message}", ex);
        }
    }


    public async Task<TxnLPPMBBResponse> SubmitTollCharge(TxnLPPMBBRequest requestBody, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            _logger.LogInformation("MBB Charge - REQUEST - {requestJson}", json);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = $"{_applicationSettings.MGateApi?.Trim()}/mpgw/api/v2/{_applicationSettings.MerchantId}/payments/concessionaire/charge";

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"MBB Charge API error {(int)response.StatusCode}: {responseBody}");
            }

            var result = JsonSerializer.Deserialize<TxnLPPMBBResponse>(responseBody);
            if (result != null)
            {
                _logger.LogInformation("MBB Charge - RESPONSE - {response}", responseBody);
                return result;
            }
            else
            {
                throw new InvalidOperationException($"MBB Charge API [{endpoint}] returned null or empty response");
            }
        }
        catch (HttpRequestException ex)
        {
            // Log and rethrow or handle
            throw new HttpRequestException($"Submit toll payment charge failed: {ex.Message}", ex);
        }
    }
}