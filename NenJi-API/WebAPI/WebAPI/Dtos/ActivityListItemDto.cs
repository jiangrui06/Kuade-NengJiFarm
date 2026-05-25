namespace WebAPI.Dtos;

// ========== 活动列表项 DTO（本次新增 StartDate / EndDate / LimitPerOrder） ==========
public class ActivityListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "已下架";
    public string Image { get; set; } = string.Empty;
    public int? People { get; set; }
    public int Duration { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }     // 新增
    public DateTime? EndDate { get; set; }       // 新增
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
    public string CreateTime { get; set; } = string.Empty;
}
