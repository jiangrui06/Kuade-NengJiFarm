namespace WebAPI.Dtos;

/// <summary>
/// ��Ʒ����
/// </summary>
public class DishDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int Sold { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
    public List<SpecImageItemDto> SpecImages { get; set; } = [];
    public string? Description { get; set; }
    public string? DishType { get; set; }
    public string UploadTime { get; set; } = string.Empty;
}