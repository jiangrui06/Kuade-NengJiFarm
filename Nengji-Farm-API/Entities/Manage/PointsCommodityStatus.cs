using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage;

[Table("points_commodity_status")]
public class PointsCommodityStatus
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("status_name")]
    [MaxLength(30)]
    public string StatusName { get; set; } = string.Empty;
}
