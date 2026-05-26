using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage;

[Table("points_commodity_order")]
public class PointsExchange
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("order_no")]
    [MaxLength(64)]
    public string OrderNo { get; set; } = string.Empty;

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("commodity_id")]
    public int CommodityId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("points_spent")]
    public int PointsSpent { get; set; }

    [Column("status_id")]
    public int StatusId { get; set; }

    [Column("verify_code")]
    [MaxLength(64)]
    public string? VerifyCode { get; set; }

    [Column("verify_time")]
    public DateTime? VerifyTime { get; set; }

    [Column("create_time")]
    public DateTime CreateTime { get; set; }
}
