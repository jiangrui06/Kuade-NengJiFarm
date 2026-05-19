using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("dish_category")]
public class DishCategory
{
    [Key]
    [Column("dish_category_id")]
    public int DishCategoryId { get; set; }

    [Column("dish_category_name")]
    [MaxLength(50)]
    public string DishCategoryName { get; set; } = string.Empty;

    [Column("dish_sort_order")]
    public int DishSortOrder { get; set; }

    [Column("dish_category_status_id")]
    public int? DishCategoryStatusId { get; set; }
}
