using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("activity_type")]
public class ActivityTypeEntity
{
    [Key]
    [Column("activity_type_id")]
    public int ActivityTypeId { get; set; }

    [Column("type_name")]
    [MaxLength(100)]
    public string TypeName { get; set; } = string.Empty;
}
