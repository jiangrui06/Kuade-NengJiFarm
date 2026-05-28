namespace WebAPI.Dtos;

// ========== 活动详情 DTO（本次新增 StartDate / EndDate / LimitPerOrder / Stock） ==========
public class ActivityManageDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StatusId { get; set; }
    public string Status { get; set; } = "已下架";
    public string? Image { get; set; }
    public string? ImageName { get; set; }
    public string? VideoUrl { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public int? People { get; set; }
    public string? Content { get; set; }
    public int Duration { get; set; }
    public DateTime? StartDate { get; set; }    // 新增
    public DateTime? EndDate { get; set; }      // 新增
    public int Stock { get; set; }              // 新增
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
    /// <summary>轮播图URL列表（备用，从 CarouselMedia 提取）</summary>
    public List<string> Images { get; set; } = [];
    /// <summary>视频URL（备用，与 VideoUrl 同值）</summary>
    public string? Video { get; set; }
    /// <summary>
    /// 规格图片列表，每项含 url + sortOrder
    /// </summary>
    public List<MediaSortItemDto> SpecImages { get; set; } = [];
    public string CreateTime { get; set; } = string.Empty;
}
