using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("order")]
public class OrderEntity
{
    [Key]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("order_number")]
    [MaxLength(45)]
    public string OrderNumber { get; set; } = string.Empty;

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("actual_payment")]
    public decimal ActualPayment { get; set; }

    [Column("order_type")]
    public int OrderType { get; set; }

    [Column("total_amount")]
    public decimal TotalOrderAmount { get; set; }

    [Column("order_status")]
    public int OrderStatus { get; set; }

    [Column("payment_status")]
    public int PaymentStatus { get; set; }

    [Column("delivery_methods")]
    public int DeliveryMethods { get; set; }

    [Column("shipping_address")]
    [MaxLength(45)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Column("address_id")]
    public int AddressId { get; set; }

    [Column("contact_person")]
    [MaxLength(45)]
    public string ContactPerson { get; set; } = string.Empty;

    [Column("contact_number")]
    [MaxLength(45)]
    public string ContactNumber { get; set; } = string.Empty;

    [Column("order_create_time")]
    public DateTime OrderCreationTime { get; set; }

    [Column("payment_time")]
    public DateTime PaymentTime { get; set; }

    [Column("payment_methods")]
    public int PaymentMethods { get; set; }

    [Column("order_form_id")]
    public int OrderFormId { get; set; }

    [Column("snapshot_receiver_name")]
    [MaxLength(100)]
    public string? SnapshotReceiverName { get; set; }

    [Column("snapshot_receiver_phone")]
    [MaxLength(50)]
    public string? SnapshotReceiverPhone { get; set; }

    [Column("snapshot_delivery_address")]
    [MaxLength(255)]
    public string? SnapshotDeliveryAddress { get; set; }

    [Column("snapshot_user_nickname")]
    [MaxLength(100)]
    public string? SnapshotUserNickname { get; set; }
}
