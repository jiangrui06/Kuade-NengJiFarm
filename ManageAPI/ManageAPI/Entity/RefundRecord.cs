using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManageAPI.Entity;

[Table("refund_record")]
public class RefundRecord
{
    [Key]
    [Column("refund_id")]
    public long RefundId { get; set; }

    [Column("refund_no")]
    [MaxLength(64)]
    public string RefundNo { get; set; } = string.Empty;

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("order_no")]
    [MaxLength(64)]
    public string OrderNo { get; set; } = string.Empty;

    [Column("order_type")]
    [MaxLength(20)]
    public string OrderType { get; set; } = string.Empty;

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("reason")]
    [MaxLength(50)]
    public string Reason { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("images")]
    public string? Images { get; set; }

    [Column("refund_amount")]
    public decimal RefundAmount { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    [Column("process_time")]
    public DateTime? ProcessTime { get; set; }

    [Column("process_note")]
    [MaxLength(500)]
    public string? ProcessNote { get; set; }

    [Column("admin_reply")]
    [MaxLength(500)]
    public string? AdminReply { get; set; }

    [Column("create_time")]
    public DateTime CreateTime { get; set; }

    [Column("update_time")]
    public DateTime? UpdateTime { get; set; }
}
