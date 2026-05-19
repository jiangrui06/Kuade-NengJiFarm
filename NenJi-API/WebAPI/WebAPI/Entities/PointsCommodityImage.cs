using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

/// <summary>
/// 积分商品图片（映射 points_commodity_image 表）
/// </summary>
[Table("points_commodity_image")]
public class PointsCommodityImage
{
    [Key]
    [Column("material_id")]
    public int MaterialId { get; set; }

    [Column("points_commodity_id")]
    public int PointsCommodityId { get; set; }

    [Column("image_url")]
    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("create_time")]
    public DateTime CreateTime { get; set; }

    [Column("type")]
    [MaxLength(255)]
    public string? Type { get; set; }

    [Column("material_type")]
    public int MaterialType { get; set; }
}
