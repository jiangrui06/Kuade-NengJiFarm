using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("activity_order_detail")]
public class ActivityOrderDetail
{
    [Key]
    [Column("activity_order_details_id")]
    public long ActivityOrderDetailsId { get; set; }

    [Column("activity_order_id")]
    public long ActivityOrderId { get; set; }

    [Column("activity_id")]
    public long ActivityId { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("subtotal_amount")]
    public decimal SubtotalAmount { get; set; }

    [Column("activity_qrcode")]
    [MaxLength(255)]
    public string? ActivityQrcode { get; set; }
}
