using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("sys_config")]
public class SysConfig
{
    [Key]
    [Column("config_id")]
    public int ConfigId { get; set; }

    [Column("config_key")]
    [MaxLength(100)]
    public string ConfigKey { get; set; } = string.Empty;

    [Column("config_value")]
    public string ConfigValue { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }
}
