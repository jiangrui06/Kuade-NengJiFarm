using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("activity")]
public class ActivityEntity
{
    [Key]
    [Column("activity_id")]
    public long ActivityId { get; set; }

    [Column("title")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("image_url")]
    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("video_url")]
    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    [Column("description")]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Column("location")]
    [MaxLength(255)]
    public string Location { get; set; } = string.Empty;

    [Column("people")]
    public int People { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    [Column("status_id")]
    public int StatusId { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("type_id")]
    public int TypeId { get; set; }
}
