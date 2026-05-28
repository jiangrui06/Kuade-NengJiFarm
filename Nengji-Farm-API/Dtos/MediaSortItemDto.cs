namespace WebAPI.Dtos;

/// <summary>
/// 媒体排序项（含 URL 和排序序号）
/// </summary>
public class MediaSortItemDto
{
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
