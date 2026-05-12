using System.Text.Json.Serialization;

namespace ManageAPI.Dtos;

public class UpdateProductOrderStatusDto
{
    [JsonPropertyName("orderNo")]
    public string OrderNo { get; set; } = string.Empty;

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
}
