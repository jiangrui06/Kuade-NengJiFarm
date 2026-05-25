using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("activity_material")]
public class ActivityMaterial
{
    [Key]
    [Column("activity_material_id")]
    public long Id { get; set; }

    [Column("activity_id")]
    public long ActivityId { get; set; }

    [Column("material_type")]
    public int? MaterialType { get; set; }

    [Column("material_url")]
    [MaxLength(500)]
    public string? MaterialUrl { get; set; }

    [Column("video_url")]
    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
