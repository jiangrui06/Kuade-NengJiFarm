using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage;

[Table("carousel")]
public class Carousel
{
    [Key]
    [Column("carousel_id")]
    public long CarouselId { get; set; }

    [Column("title")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("image_url")]
    [MaxLength(255)]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("link_url")]
    [MaxLength(255)]
    public string? LinkUrl { get; set; }

    [Column("position")]
    [MaxLength(50)]
    public string Position { get; set; } = "home";

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("status")]
    public int Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
