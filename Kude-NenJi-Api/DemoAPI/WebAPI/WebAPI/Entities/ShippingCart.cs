using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("shipping_cart")]
public class ShippingCart
{
    [Key]
    [Column("shipping_cart_id")]
    public int ShippingCartId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("cart_quantity")]
    public int CartQuantity { get; set; }

    [Column("commodity_id")]
    public int CommodityId { get; set; }

    [Column("join_time")]
    public DateTime JoinTime { get; set; }
}
