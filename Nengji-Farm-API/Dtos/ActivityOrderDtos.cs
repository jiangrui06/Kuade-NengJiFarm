using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class ActivityOrderListItemDto
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
    public int OrderStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    /// <summary>
    /// 支付状态（前端展示用）：待支付 / 已支付 / 已退款
    /// </summary>
    public string PaymentStatus => OrderStatusId switch
    {
        1 => "待支付",
        4 => "已退款",
        _ => "已支付"
    };

    /// <summary>
    /// 订单状态（前端展示用）：待付款 / 待核销 / 已核销 / 已退款
    /// </summary>
    public string OrderStatus => OrderStatusId switch
    {
        1 => "待付款",
        2 => "待核销",
        3 => "已核销",
        4 => "已退款",
        _ => "未知"
    };

    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? ActivityTitle { get; set; }
    public string CreateTime { get; set; } = string.Empty;
}

public class ActivityOrderItemDto
{
    public long ActivityOrderDetailsId { get; set; }
    public long ActivityId { get; set; }
    public string ActivityTitle { get; set; } = string.Empty;
    public string? ActivityImage { get; set; }
    public string? ActivityDescription { get; set; }
    public string? ActivityLocation { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal SubtotalAmount { get; set; }
    public string? ActivityQrcode { get; set; }
    public bool IsVerified { get; set; }
    public string? VerificationTime { get; set; }
}

public class ActivityOrderFullDetailDto
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string? WxPayNo { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
    public int OrderStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    /// <summary>
    /// 支付状态（前端展示用）：待支付 / 已支付 / 已退款
    /// </summary>
    public string PaymentStatus => OrderStatusId switch
    {
        1 => "待支付",
        4 => "已退款",
        _ => "已支付"
    };

    /// <summary>
    /// 订单状态（前端展示用）：待付款 / 待核销 / 已核销 / 已退款
    /// </summary>
    public string OrderStatus => OrderStatusId switch
    {
        1 => "待付款",
        2 => "待核销",
        3 => "已核销",
        4 => "已退款",
        _ => "未知"
    };

    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserPhone { get; set; }
    public string CreateTime { get; set; } = string.Empty;
    public List<ActivityOrderItemDto> Items { get; set; } = new();

    /// <summary>
    /// 退款原因（从 RefundRecord.Description 提取，非退款状态时为 null）
    /// </summary>
    public string? RefundReason { get; set; }

    /// <summary>
    /// 退款编号
    /// </summary>
    public string? RefundId { get; set; }

    /// <summary>
    /// 退款状态：pending / completed / rejected
    /// </summary>
    public string? RefundStatus { get; set; }
}

public class VerifyActivityOrderRequest
{
    /// <summary>活动订单明细 ID（后端管理端使用）</summary>
    public long ActivityOrderDetailsId { get; set; }

    /// <summary>主订单 ID（前端核销时传入）</summary>
    public long OrderId { get; set; }

    /// <summary>核销码（前端传入，暂未使用）</summary>
    public string? VerifyCode { get; set; }
}

public class ActivityOrderRefundRequest
{
    [JsonPropertyName("orderNo")]
    public string? OrderNo { get; set; }

    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }

    [JsonPropertyName("refundReason")]
    public string? RefundReason { get; set; }

    /// <summary>操作类型：reject（驳回）</summary>
    public string? Action { get; set; }

    /// <summary>退款编号（驳回时使用）</summary>
    public string? RefundId { get; set; }

    /// <summary>管理员回复（驳回原因）</summary>
    public string? AdminReply { get; set; }

    /// <summary>处理备注</summary>
    public string? ProcessNote { get; set; }
}

public class ActivityOrderRefundResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string RefundAmount { get; set; } = string.Empty;
    public string RefundTime { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
}

public class ActivityOrderRejectResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string Action { get; set; } = "rejected";
    public string? AdminReply { get; set; }
}
