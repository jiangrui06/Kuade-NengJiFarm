using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity_image")]
public class CommodityImage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("commodity_id")]
    public int? CommodityId { get; set; }

    [Column("url")]
    [MaxLength(255)]
    public string? Url { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }

    [Column("image_type")]
    public int? ImageType { get; set; }
}
