using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity_order_detail")]
public class CommodityOrderDetail
{
    [Key]
    [Column("commodity_order_details_id")]
    public long CommodityOrderDetailsId { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("commodity_id")]
    public int CommodityId { get; set; }

    [Column("goods_name")]
    [MaxLength(100)]
    public string GoodsName { get; set; } = string.Empty;

    [Column("image_url")]
    [MaxLength(255)]
    public string? ImageUrl { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("subtotal_amount")]
    public decimal SubtotalAmount { get; set; }

    [Column("status_id")]
    public int? StatusId { get; set; }
}
