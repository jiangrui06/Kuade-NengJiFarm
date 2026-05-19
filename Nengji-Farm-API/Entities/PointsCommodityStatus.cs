using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

/// <summary>
/// 积分商品状态（映射 points_commodity_status 表）
/// </summary>
[Table("points_commodity_status")]
public class PointsCommodityStatus
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("status_name")]
    [MaxLength(30)]
    public string StatusName { get; set; } = string.Empty;
}
