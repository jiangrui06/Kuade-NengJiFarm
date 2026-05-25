using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity_material")]
public class CommodityImage
{
    [Key]
    [Column("material_id")]
    public long Id { get; set; }

    [Column("commodity_id")]
    public int? CommodityId { get; set; }

    [Column("material_url")]
    [MaxLength(500)]
    public string? Url { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }

    [Column("material_type")]
    public int MaterialType { get; set; }
}
