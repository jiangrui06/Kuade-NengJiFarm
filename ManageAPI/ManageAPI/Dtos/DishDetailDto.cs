namespace ManageAPI.Dtos;

/// <summary>
/// 菜品详情
/// </summary>
public class DishDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public string UploadTime { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
    public List<string> SpecImages { get; set; } = [];
}
