namespace WebAPI.Dtos;

/// <summary>
/// 重量标签选项 - 用于下拉框展示，从 unit 表获取
/// </summary>
public class WeightTagOptionDto
{
    /// <summary>
    /// 显示文本，如 "g/份"
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 重量单位，如 "g"、"kg"、"斤"
    /// </summary>
    public string? WeightUnit { get; set; }

    /// <summary>
    /// 重量标签，如 "份"、"人"
    /// </summary>
    public string? WeightTag { get; set; }
}
