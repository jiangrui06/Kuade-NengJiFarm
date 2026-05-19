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

    [Column("cart_item_type")]
    public int CartItemType { get; set; } = 1;

    [Column("dish_id")]
    public int? DishId { get; set; }

    [Column("commodity_id")]
    public int? CommodityId { get; set; }

    [NotMapped]
    public DateTime JoinTime { get; set; }
}
