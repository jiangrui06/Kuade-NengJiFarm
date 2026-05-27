using System.ComponentModel.DataAnnotations;

namespace WebAPI.Dtos;

/// <summary>
/// 创建菜品
/// </summary>
public class CreateDishDto
{
    /// <summary>
    /// 菜品名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 菜品价格
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 库存
    /// </summary>
    public int Stock { get; set; }

    /// <summary>
    /// 上架状态
    /// </summary>
    public string Status { get; set; } = "已上架";

    /// <summary>
    /// 封面图URL
    /// </summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>
    /// 轮播图/视频
    /// </summary>
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];

    /// <summary>
    /// 规格图片列表
    /// </summary>
    public List<SpecImageItemDto> SpecImages { get; set; } = [];

    /// <summary>
    /// 菜品描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 菜品类型: 主食/菜品/汤品/甜品（映射到 dish_category_name）
    /// </summary>
    public string? DishType { get; set; }
}

/// <summary>
/// 编辑菜品 - 对应 /api/dish/edit
/// </summary>
public class UpdateDishDto : CreateDishDto
{
    /// <summary>
    /// 菜品ID (必填)
    /// </summary>
    [Required(ErrorMessage = "菜品ID不能为空")]
    public int Id { get; set; }
}

/// <summary>
/// 删除菜品请求
/// </summary>
public class DeleteDishRequest
{
    public int Id { get; set; }
}

/// <summary>
/// 批量删除菜品请求
/// </summary>
public class DeleteBatchDishRequest
{
    public int[]? Ids { get; set; }
}
