using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

/// <summary>
/// 积分商品（映射 points_commodity 表）
/// </summary>
[Table("points_commodity")]
public class PointsCommodity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("points_price")]
    public int PointsPrice { get; set; }

    [Column("stock")]
    public int Stock { get; set; }

    [Column("image_url")]
    [MaxLength(255)]
    public string? ImageUrl { get; set; }

    [Column("status_id")]
    public int? StatusId { get; set; }

    [Column("is_delete")]
    public int IsDelete { get; set; }

    [Column("create_time")]
    public DateTime CreateTime { get; set; }
}
