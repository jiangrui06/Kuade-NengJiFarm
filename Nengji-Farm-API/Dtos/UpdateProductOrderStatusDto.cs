using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class UpdateProductOrderStatusDto
{
    [JsonPropertyName("orderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("logisticsType")]
    public string? LogisticsType { get; set; }

    [JsonPropertyName("logisticsNo")]
    public string? LogisticsNo { get; set; }

    [JsonPropertyName("refundReason")]
    public string? RefundReason { get; set; }

    [JsonPropertyName("refundImages")]
    public List<string>? RefundImages { get; set; }

    [JsonPropertyName("refundProofImages")]
    public List<string>? RefundProofImages { get; set; }

    /// <summary>合并 refundImages 和 refundProofImages</summary>
    [JsonIgnore]
    public List<string>? EffectiveRefundImages => RefundImages ?? RefundProofImages;

    [JsonPropertyName("refundId")]
    public string? RefundId { get; set; }

    [JsonPropertyName("adminReply")]
    public string? AdminReply { get; set; }

    [JsonPropertyName("processNote")]
    public string? ProcessNote { get; set; }
}
