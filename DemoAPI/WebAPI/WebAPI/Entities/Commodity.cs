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

    [NotMapped]
    public decimal UnitPrice { get; set; }

    [Column("image_data")]
    public byte[]? ImageData { get; set; }
}
