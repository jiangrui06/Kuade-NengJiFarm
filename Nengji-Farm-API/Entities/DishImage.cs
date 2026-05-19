using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("dish_image")]
public class DishImage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("dish_id")]
    public int DishId { get; set; }

    [Column("image_url")]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("material_type")]
    public int MaterialType { get; set; }
}
