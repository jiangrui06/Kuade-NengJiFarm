using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage;

[Table("tracking_type")]
public class TrackingType
{
    [Key]
    [Column("tracking_type_id")]
    public long TrackingTypeId { get; set; }

    [Column("tracking_type_name")]
    [MaxLength(50)]
    public string TrackingTypeName { get; set; } = string.Empty;
}
