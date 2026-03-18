using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("order_details")]
public class OrderDetail
{
    [Key]
    [Column("order_detail_id")]
    public long OrderDetailsId { get; set; }

    [Column("order_food_id")]
    public long OrderId { get; set; }

    [Column("dish_id")]
    public int CommodityId { get; set; }

    [Column("dish_name")]
    [MaxLength(100)]
    public string DishName { get; set; } = string.Empty;

    [Column("meal_unit_price")]
    public decimal ActualUnitPrice { get; set; }

    [Column("meal_order_quantity")]
    public int PurchaseQuantity { get; set; }

    [Column("meal_subtotal_amount")]
    public decimal SubtotalAmount { get; set; }

    [Column("taste")]
    [MaxLength(100)]
    public string? Taste { get; set; }

    [Column("meal_status")]
    public int MealStatus { get; set; }
}
