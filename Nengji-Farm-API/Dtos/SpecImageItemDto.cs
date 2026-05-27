namespace WebAPI.Dtos;

/// <summary>
/// 规格图片项（含URL和排序序号）
/// </summary>
public class SpecImageItemDto
{
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
