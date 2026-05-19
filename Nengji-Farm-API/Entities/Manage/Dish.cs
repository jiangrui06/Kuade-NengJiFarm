using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage;

[Table("dish")]
public class Dish
{
    [Key]
    [Column("dish_id")]
    public int DishId { get; set; }

    [Column("dish_name")]
    [MaxLength(100)]
    public string DishName { get; set; } = string.Empty;

    [Column("dish_description")]
    [MaxLength(255)]
    public string DishDescription { get; set; } = string.Empty;

    [Column("dish_price")]
    public decimal DishPrice { get; set; }

    [Column("dish_category_id")]
    public int DishCategoryId { get; set; }

    [Column("image_url")]
    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("attribute_name")]
    [MaxLength(100)]
    public string AttributeName { get; set; } = string.Empty;

    [Column("dish_status_id")]
    public int Status { get; set; }

    [Column("limited_edition")]
    public int LimitedEdition { get; set; }

    [Column("dish_sold")]
    public int DishSold { get; set; }

    [Column("dish_remaining_quantity")]
    public int DishRemainingQuantity { get; set; }

    [Column("user_purchase_limit")]
    public int UserPurchaseLimit { get; set; }

    [Column("isdelete_id")]
    public byte IsdeleteId { get; set; }

}
