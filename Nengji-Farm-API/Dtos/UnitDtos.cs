using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class AddUnitRequestDto
{
    [JsonPropertyName("unitName")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("unitCode")]
    public string UnitCode { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public int IsEnabled { get; set; } = 1;
}

public class UpdateUnitRequestDto
{
    [JsonPropertyName("unitId")]
    public int UnitId { get; set; }

    [JsonPropertyName("unitName")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("unitCode")]
    public string UnitCode { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public int IsEnabled { get; set; } = 1;
}

public class DeleteUnitRequestDto
{
    [JsonPropertyName("unitId")]
    public int UnitId { get; set; }
}
