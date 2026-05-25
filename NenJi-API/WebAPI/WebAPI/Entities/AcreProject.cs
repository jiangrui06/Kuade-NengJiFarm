using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("acre_project")]
public class AcreProject
{
    [Key]
    [Column("acre_project_id")]
    public long AcreProjectId { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    [Column("image_url")]
    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("status")]
    public int Status { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
