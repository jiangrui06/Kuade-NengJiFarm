using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("acre_project_image")]
public class AcreProjectImage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("acre_project_id")]
    public long AcreProjectId { get; set; }

    [Column("image_url")]
    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("sort_order")]
    public int SortOrder { get; set; }
}
