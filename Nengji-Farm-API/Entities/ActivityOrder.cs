using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("activity_orders")]
public class ActivityOrder
{
    [Key]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("order_no")]
    [MaxLength(64)]
    public string OrderNo { get; set; } = string.Empty;

    [Column("wx_pay_no")]
    [MaxLength(500)]
    public string? WxPayNo { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("total_quantity")]
    public int TotalQuantity { get; set; }

    [Column("order_status_id")]
    public int OrderStatusId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("create_time")]
    public DateTime CreateTime { get; set; }
}
