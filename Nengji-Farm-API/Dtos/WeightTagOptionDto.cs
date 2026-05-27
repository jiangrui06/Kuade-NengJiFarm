namespace WebAPI.Dtos;

/// <summary>
/// 重量标签选项 - 用于下拉框展示，从 unit 表获取
/// </summary>
public class WeightTagOptionDto
{
    /// <summary>
    /// 单位名称，如 "斤"、"个"、"箱"
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 重量单位
    /// </summary>
    public string? WeightUnit { get; set; }
}
