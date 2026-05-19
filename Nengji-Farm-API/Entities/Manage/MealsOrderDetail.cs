using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage;

[Table("meals_order_details")]
public class MealsOrderDetail
{
    [Key]
    [Column("meals_order_details_id")]
    public int MealsOrderDetailsId { get; set; }

    [Column("order_food_id")]
    public int OrderFoodId { get; set; }

    [Column("dish_id")]
    public int DishId { get; set; }


    [Column("dish_name")]
    [MaxLength(255)]
    public string DishName { get; set; } = string.Empty;

    [Column("meal_unit_price")]
    public decimal MealUnitPrice { get; set; }

    [Column("meal_order_quantity")]
    public int MealOrderQuantity { get; set; }

    [Column("meal_subtotal_amount")]
    public decimal MealSubtotalAmount { get; set; }

    [Column("taste")]
    [MaxLength(255)]
    public string Taste { get; set; } = string.Empty;

    [Column("meal_status")]
    public int MealStatus { get; set; }
}
