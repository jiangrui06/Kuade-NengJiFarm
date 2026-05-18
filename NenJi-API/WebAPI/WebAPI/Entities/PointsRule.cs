using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("points_rule")]
public class PointsRule
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("rule_name")]
    [MaxLength(64)]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>每消费 unit_amount 元</summary>
    [Column("unit_amount")]
    public decimal UnitAmount { get; set; }

    /// <summary>获得 unit_points 积分</summary>
    [Column("unit_points")]
    public int UnitPoints { get; set; }

    [Column("description")]
    [MaxLength(255)]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
