namespace WebAPI.Dtos;

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
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
    public string CreateTime { get; set; } = string.Empty;
}
