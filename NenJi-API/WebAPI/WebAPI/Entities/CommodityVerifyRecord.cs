using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity_verify_record")]
public class CommodityVerifyRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("staff_id")]
    public int StaffId { get; set; }

    [Column("verify_time")]
    public DateTime VerifyTime { get; set; }
}
