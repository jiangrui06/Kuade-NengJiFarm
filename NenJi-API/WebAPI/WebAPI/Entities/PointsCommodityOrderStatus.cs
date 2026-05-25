using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

/// <summary>
/// 积分兑换订单状态（映射 points_commodity_order_status 表）
/// </summary>
[Table("points_commodity_order_status")]
public class PointsCommodityOrderStatus
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("status_name")]
    [MaxLength(30)]
    public string StatusName { get; set; } = string.Empty;
}
