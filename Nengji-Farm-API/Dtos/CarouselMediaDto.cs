namespace WebAPI.Dtos;

/// <summary>
/// 轮播媒体 (图片或视频)
/// </summary>
public class CarouselMediaDto
{
    /// <summary>
    /// 媒体类型: image 或 video
    /// </summary>
    public string Type { get; set; } = "image";

    /// <summary>
    /// 媒体URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 视频封面图URL (仅视频类型需要)
    /// </summary>
    public string? Thumb { get; set; }

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; set; }
}
