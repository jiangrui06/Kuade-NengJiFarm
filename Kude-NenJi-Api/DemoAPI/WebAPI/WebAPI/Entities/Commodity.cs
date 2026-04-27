using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity")]
public class Commodity
{
    [Key]
    [Column("commodity_id")]
    public int CommodityId { get; set; }

    [Column("spec_description")]
    [MaxLength(255)]
    public string? SpecDescription { get; set; }

    [Column("in_stock")]
    public int? InStock { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    [Column("product_status")]
    public int? ProductStatus { get; set; }

    [Column("product_name")]
    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("image_url")]
    [MaxLength(255)]
    public string? ImageUrl { get; set; }

    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }

    [Column("original_price")]
    public decimal? OriginalPrice { get; set; }

    [Column("weight_text")]
    [MaxLength(50)]
    public string? WeightText { get; set; }

    [Column("storage_condition")]
    [MaxLength(50)]
    public string? StorageCondition { get; set; }

    [Column("unit_name")]
    [MaxLength(20)]
    public string? UnitName { get; set; }

    [NotMapped]
    public byte[]? ImageData { get; set; }
}
