namespace WebAPI.Dtos;

/// <summary>
/// 券品列表项
/// </summary>
public class CouponListItemDto
{
    /// <summary>
    /// 券品ID (ActivityId)
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 券品名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 券品类型：采摘券 / 研学活动券
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 售价
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 库存数量
    /// </summary>
    public int Stock { get; set; }

    /// <summary>
    /// 单次限购数量
    /// </summary>
    public int LimitPerOrder { get; set; }

    /// <summary>
    /// 有效期数值
    /// </summary>
    public int ValidityPeriod { get; set; }

    /// <summary>
    /// 有效期单位
    /// </summary>
    public string ValidityUnit { get; set; } = string.Empty;

    /// <summary>
    /// 有效期展示（如 30天）
    /// </summary>
    public string Validity { get; set; } = string.Empty;

    /// <summary>
    /// 退款规则
    /// </summary>
    public string RefundRule { get; set; } = string.Empty;

    /// <summary>
    /// 使用规则
    /// </summary>
    public string UsageRules { get; set; } = string.Empty;

    /// <summary>
    /// 封面图
    /// </summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>
    /// 轮播图/视频
    /// </summary>
    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];

    /// <summary>
    /// 已售数量
    /// </summary>
    public int SoldCount { get; set; }

    /// <summary>
    /// 已核销数量
    /// </summary>
    public int VerifiedCount { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public string CreateTime { get; set; } = string.Empty;
}