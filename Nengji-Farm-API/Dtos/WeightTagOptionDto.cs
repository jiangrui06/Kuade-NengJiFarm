namespace WebAPI.Dtos;

/// <summary>
/// 重量标签选项 - 用于下拉框展示
/// </summary>
public class WeightTagOptionDto
{
    /// <summary>
    /// 显示文本，如 "142斤"
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 净含量数值
    /// </summary>
    public decimal? NetWeight { get; set; }

    /// <summary>
    /// 重量单位
    /// </summary>
    public string? WeightUnit { get; set; }
}
