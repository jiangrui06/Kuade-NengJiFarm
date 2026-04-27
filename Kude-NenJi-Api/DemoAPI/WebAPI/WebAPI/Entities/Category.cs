using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("category")]
public class Category
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("category_name")]
    [MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [Column("category_description")]
    [MaxLength(255)]
    public string? CategoryDescription { get; set; }

    [Column("category_status")]
    public int? CategoryStatus { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }
}
