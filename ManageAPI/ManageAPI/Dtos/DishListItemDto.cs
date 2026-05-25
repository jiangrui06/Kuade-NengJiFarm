namespace ManageAPI.Dtos;

/// <summary>
/// 菜品列表项
/// </summary>
public class DishListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string UploadTime { get; set; } = string.Empty;
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
    public List<string> SpecImages { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public string? DishType { get; set; }
}