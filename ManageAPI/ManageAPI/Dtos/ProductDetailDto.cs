namespace ManageAPI.Dtos;

/// <summary>
/// 产品详情 - 对应 /api/product/detail
/// </summary>
public class ProductDetailDto
{
    /// <summary>
    /// 产品ID (格式: CommodityId字符串)
    /// </summary>
    public string Id { get; set; } = string.Empty;

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
    /// 状态: "已上架" 或 "已下架"
    /// </summary>
    public string Status { get; set; } = "已下架";

    /// <summary>
    /// 产品主图
    /// </summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>
    /// 产品封面图 (与image同值)
    /// </summary>
    public string CoverImage { get; set; } = string.Empty;

    /// <summary>
    /// 轮播图/视频素材 (最多5个)
    /// </summary>
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];

    /// <summary>
    /// 净含量数值
    /// </summary>
    public decimal? NetWeight { get; set; }

    /// <summary>
    /// 单位: kg, g, 个
    /// </summary>
    public string? WeightUnit { get; set; }

    /// <summary>
    /// 保存条件: 常温保存, 冷藏保存
    /// </summary>
    public string? StorageCondition { get; set; }

    /// <summary>
    /// 规格图片列表 (最多5个)
    /// </summary>
    public List<string> SpecImages { get; set; } = [];

    /// <summary>
    /// 产品描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 产品类型：实物/虚拟
    /// </summary>
    public string? ProductType { get; set; }

    /// <summary>
    /// 上架/更新时间, 格式: "yyyy-MM-dd HH:mm"
    /// </summary>
    public string UploadTime { get; set; } = string.Empty;
}
