namespace WebAPI.Dtos;

/// <summary>
/// 菜品列表项
/// </summary>
public class DishListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Status { get; set; } = "已下架";
    public string Image { get; set; } = string.Empty;
    public string UploadTime { get; set; } = string.Empty;
}