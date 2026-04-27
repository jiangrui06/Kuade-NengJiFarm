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

    [Column("price_text")]
    [MaxLength(100)]
    public int PriceText { get; set; }

    [Column("date_text")]
    [MaxLength(100)]
    public string DateText { get; set; } = string.Empty;

    [Column("image_url")]
    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("participants")]
    public int Participants { get; set; }

    [Column("remaining_slots")]
    public int RemainingSlots { get; set; }

    [Column("status")]
    public int Status { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
