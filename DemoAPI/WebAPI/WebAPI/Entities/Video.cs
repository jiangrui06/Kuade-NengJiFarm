using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("video")]
public class Video
{
    [Key]
    [Column("video_id")]
    public long VideoId { get; set; }

    [Column("title")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("cover_url")]
    [MaxLength(255)]
    public string CoverUrl { get; set; } = string.Empty;

    [Column("video_url")]
    [MaxLength(500)]
    public string VideoUrl { get; set; } = string.Empty;

    [Column("commodity_id")]
    public int? CommodityId { get; set; }

    [Column("dish_id")]
    public int? DishId { get; set; }

    [Column("acre_project_id")]
    public long? AcreProjectId { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("status")]
    public int Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
