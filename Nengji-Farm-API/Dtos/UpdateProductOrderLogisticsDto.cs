using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class UpdateProductOrderLogisticsDto
{
    [JsonPropertyName("orderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [JsonPropertyName("logisticsType")]
    public string LogisticsType { get; set; } = string.Empty;

    [JsonPropertyName("logisticsNo")]
    public string? LogisticsNo { get; set; }
}
