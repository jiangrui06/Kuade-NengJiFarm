using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("order_food")]
public class OrderFood
{
    [Key]
    [Column("order_food_id")]
    public int OrderFoodId { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("menu_number")]
    [MaxLength(50)]
    public string MenuNumber { get; set; } = string.Empty;

    [Column("table_number")]
    public int TableNumber { get; set; }

    [Column("number_of_diners")]
    public int NumberOfDiners { get; set; }

    [Column("remark")]
    [MaxLength(255)]
    public string Remark { get; set; } = string.Empty;

    [Column("creation_time")]
    public DateTime CreationTime { get; set; }

    [Column("meal_serving_time")]
    public DateTime MealServingTime { get; set; }

    [Column("order_status")]
    public int OrderStatus { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }
}
