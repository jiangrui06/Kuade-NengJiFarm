namespace ManageAPI.Dtos;

public class ActivityDetailDto
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
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
    public string CreateTime { get; set; } = string.Empty;
}
