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
}

public class VerifyActivityOrderRequest
{
    public long ActivityOrderDetailsId { get; set; }
}

public class ActivityOrderRefundRequest
{
    public long OrderId { get; set; }
    public string? RefundReason { get; set; }
}

public class ActivityOrderRefundResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string RefundAmount { get; set; } = string.Empty;
    public string RefundTime { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
}
