namespace WebAPI.Dtos;

// ========== 创建活动 DTO（本次新增 StartDate / EndDate / LimitPerOrder） ==========
public class CreateActivityDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StatusId { get; set; } = 1;
    public string? Image { get; set; }
    public string? VideoUrl { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public int? People { get; set; }
    public string? Content { get; set; }
    public int Duration { get; set; }
    public DateTime? StartDate { get; set; }     // 新增
    public DateTime? EndDate { get; set; }       // 新增
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
}

// ========== 更新活动 DTO ==========
public class UpdateActivityDto : CreateActivityDto
{
    public long Id { get; set; }
}
