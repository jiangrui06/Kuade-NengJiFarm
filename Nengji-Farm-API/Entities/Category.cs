using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity_category")]
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

    [Column("category_status_id")]
    public int? CategoryStatusId { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }
}
