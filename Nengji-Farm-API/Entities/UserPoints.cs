using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("user_points")]
public class UserPoints
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("total_points")]
    public int TotalPoints { get; set; }

    [Column("earned_points")]
    public int EarnedPoints { get; set; }

    [Column("spent_points")]
    public int SpentPoints { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
