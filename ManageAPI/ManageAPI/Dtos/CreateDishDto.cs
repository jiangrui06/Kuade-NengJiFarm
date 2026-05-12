using System.ComponentModel.DataAnnotations;

namespace ManageAPI.Dtos;

/// <summary>
/// 创建菜品
/// </summary>
public class CreateDishDto
{
    [Required(ErrorMessage = "菜品名称不能为空")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "菜品价格必须大于0")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "库存不能为负")]
    public int Stock { get; set; }

    [Required(ErrorMessage = "状态不能为空")]
    public string Status { get; set; } = "已上架";

    public string Image { get; set; } = string.Empty;

    public List<CarouselMediaDto>? CarouselMedia { get; set; }

    public List<string>? SpecImages { get; set; }

    public string? Description { get; set; }
}

/// <summary>
/// 编辑菜品
/// </summary>
public class UpdateDishDto
{
    [Required(ErrorMessage = "菜品ID不能为空")]
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public int? Stock { get; set; }
    public string? Status { get; set; }
    public string? Image { get; set; }
    public List<CarouselMediaDto>? CarouselMedia { get; set; }
    public List<string>? SpecImages { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 删除菜品请求
/// </summary>
public class DeleteDishRequest
{
    [Required(ErrorMessage = "菜品ID不能为空")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// 批量删除菜品请求
/// </summary>
public class DeleteBatchDishRequest
{
    [Required(ErrorMessage = "菜品ID不能为空")]
    public string[] Ids { get; set; } = [];
}
