namespace WebAPI.Dtos;

/// <summary>
/// 创建产品DTO
/// </summary>
public class CreateProductDto
{
    /// <summary>
    /// 产品名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 产品价格
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 库存数量
    /// </summary>
    public int Stock { get; set; }

    /// <summary>
    /// 上架状态
    /// </summary>
    public string Status { get; set; } = "已下架";

    /// <summary>
    /// 封面图URL
    /// </summary>
    public string CoverImage { get; set; } = string.Empty;

    /// <summary>
    /// 轮播图/视频
    /// </summary>
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];

    /// <summary>
    /// 单位ID（关联unit表）
    /// </summary>
    public int? UnitId { get; set; }

    /// <summary>
    /// 产品类型：实物/虚拟
    /// </summary>
    public string? ProductType { get; set; }

    /// <summary>
    /// 净含量
    /// </summary>
    public decimal? NetWeight { get; set; }

    /// <summary>
    /// 单位
    /// </summary>
    public string WeightUnit { get; set; } = string.Empty;

    /// <summary>
    /// 保存条件
    /// </summary>
    public string StorageCondition { get; set; } = string.Empty;

    /// <summary>
    /// 规格图片列表
    /// </summary>
    public List<string> SpecImages { get; set; } = [];

    /// <summary>
    /// 产品描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

}
