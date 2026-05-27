namespace WebAPI.Dtos;

/// <summary>
/// 产品列表项 - 对应 /api/product/list
/// </summary>
public class ProductListItemDto
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
    /// 列表页缩略图
    /// </summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>
    /// 上架/更新时间, 格式: "yyyy-MM-dd HH:mm"
    /// </summary>
    public string UploadTime { get; set; } = string.Empty;

    /// <summary>
    /// 重量标签文本，如 "142斤"、"5kg"
    /// </summary>
    public string? WeightText { get; set; }

    /// <summary>
    /// 产品类型: 实物/虚拟
    /// </summary>
    public string ProductType { get; set; } = "实物";
}
