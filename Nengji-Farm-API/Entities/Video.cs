using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("videos")]
public class Video
{
    [Key]
    [Column("video_id")]
    public long VideoId { get; set; }

    [NotMapped]
    public string Title { get; set; } = string.Empty;

    [NotMapped]
    public string CoverUrl { get; set; } = string.Empty;

    [Column("video_url")]
    [MaxLength(255)]
    public string VideoUrl { get; set; } = string.Empty;

    [NotMapped]
    public int? CommodityId { get; set; }

    [NotMapped]
    public int? DishId { get; set; }

    [NotMapped]
    public long? AcreProjectId { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [NotMapped]
    public int Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
