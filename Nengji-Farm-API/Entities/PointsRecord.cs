using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("points_record")]
public class PointsRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("type")]
    [MaxLength(10)]
    public string Type { get; set; } = string.Empty;

    [Column("points")]
    public int Points { get; set; }

    [Column("description")]
    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    [Column("order_no")]
    [MaxLength(64)]
    public string? OrderNo { get; set; }

    [Column("create_time")]
    public DateTime CreateTime { get; set; }
}
