using System.Text.Json.Serialization;
using WorkerTemplate.Converters;

namespace WorkerTemplate.Models;

public class TxnLPPMBBPayload
{
    public required string merchantAccountId { get; set; }
    public required string transactionReference { get; set; }
    public required string customerReference { get; set; }
    public required string vehicleReference { get; set; }

    [JsonConverter(typeof(IsoUtcDateTimeOffsetConverter))]
    public DateTimeOffset transactionTimestamp { get; set; }
    public int amount { get; set; }

    [JsonPropertyName("decimal")]   //  API sees "decimal", C# uses decimalPlaces. decimal is reserved keyword in C#, so we can't use it as a property name.
    public int decimalPlaces { get; set; }
    public required string currencyCode { get; set; }
    public required Metadata metadata { get; set; }


    public class Metadata
    {
        [JsonPropertyName("money_value")]
        public decimal moneyValue { get; set; }

        [JsonPropertyName("money_value_2")]
        //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? moneyValue2 { get; set; }

        [JsonPropertyName("money_value_total")]
        //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? moneyValueTotal { get; set; }

        [JsonPropertyName("transaction_code")]
        public required string transactionCode { get; set; }

        [JsonPropertyName("transaction_timestamp")]
        public DateTimeOffset? transactionTimestamp { get; set; }

        [JsonPropertyName("customer_id")]
        public required string customerId { get; set; }

        [JsonPropertyName("medium_id")]
        public required string mediumId { get; set; }

        [JsonPropertyName("sales_channel_id")]
        public string? salesChannelId { get; set; }

        [JsonPropertyName("ttype")]
        public required string ttype { get; set; }

        [JsonPropertyName("ttype_description")]
        public string? ttypeDescription { get; set; }

        [JsonPropertyName("product_id")]
        public required string productId { get; set; }

        [JsonPropertyName("promotion_codes")]
        public string? promotionCodes { get; set; }

        [JsonPropertyName("entry_spid")]
        public string? entrySpid { get; set; }

        [JsonPropertyName("entry_sp_name")]
        public string? entrySpName { get; set; }

        [JsonPropertyName("entry_plaza")]
        public string? entryPlaza { get; set; }

        [JsonPropertyName("entry_plaza_name")]
        public string? entryPlazaName { get; set; }

        [JsonPropertyName("entry_lane")]
        public string? entryLane { get; set; }

        [JsonPropertyName("entry_datetime")]
        public DateTimeOffset? entryDatetime { get; set; }

        [JsonPropertyName("exit_spid")]
        public string? exitSpid { get; set; }

        [JsonPropertyName("exit_sp_name")]
        public string? exitSpName { get; set; }

        [JsonPropertyName("exit_plaza")]
        public string? exitPlaza { get; set; }

        [JsonPropertyName("exit_plaza_name")]
        public string? exitPlazaName { get; set; }

        [JsonPropertyName("exit_lane")]
        public string? exitLane { get; set; }

        [JsonPropertyName("exit_datetime")]
        public DateTimeOffset? exitDatetime { get; set; }

        [JsonPropertyName("supplier_label")]
        public required string supplierLabel { get; set; }

        [JsonPropertyName("partner_label")]
        public string? partnerLabel { get; set; }

        [JsonPropertyName("account_type")]
        public string? accountType { get; set; }

        [JsonPropertyName("si_label")]
        public required string siLabel { get; set; }

    }
}