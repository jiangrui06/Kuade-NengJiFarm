using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("unit")]
public class Unit
{
    [Key]
    [Column("unit_id")]
    public int UnitId { get; set; }

    [Column("unit_name")]
    [MaxLength(20)]
    public string UnitName { get; set; } = string.Empty;

    [Column("unit_code")]
    [MaxLength(10)]
    public string UnitCode { get; set; } = string.Empty;

    [Column("is_enabled")]
    public sbyte IsEnabled { get; set; }
}
