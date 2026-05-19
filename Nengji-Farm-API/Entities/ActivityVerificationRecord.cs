using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("activity_verification_record")]
public class ActivityVerificationRecord
{
    [Key]
    [Column("record_id")]
    public long RecordId { get; set; }

    [Column("activity_order_details_id")]
    public long ActivityOrderDetailsId { get; set; }

    [Column("verification_time")]
    public DateTime VerificationTime { get; set; }
}
