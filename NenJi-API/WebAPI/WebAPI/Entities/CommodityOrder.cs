using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity_orders")]
public class CommodityOrder
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

    [Column("address_id")]
    public long? AddressId { get; set; }

    [Column("tracking_number")]
    [MaxLength(64)]
    public string? TrackingNumber { get; set; }

    [Column("tracking_type_id")]
    public long? TrackingTypeId { get; set; }

    [Column("receiver_phone")]
    [MaxLength(20)]
    public string? ReceiverPhone { get; set; }

    [Column("delivery_method")]
    [MaxLength(20)]
    public string DeliveryMethod { get; set; } = "express";

    [Column("verify_code")]
    [MaxLength(64)]
    public string? VerifyCode { get; set; }

    [Column("verified_time")]
    public DateTime? VerifiedTime { get; set; }
}
