using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

/// <summary>
/// 菜品订单退款请求（后台管理员操作）
/// </summary>
public class DishOrderRefundRequest
{
    [JsonPropertyName("orderNo")]
    public string? OrderNo { get; set; }

    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }

    [JsonPropertyName("refundReason")]
    public string? RefundReason { get; set; }
}

/// <summary>
/// 菜品订单退款响应
/// </summary>
public class DishOrderRefundResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string RefundAmount { get; set; } = string.Empty;
    public string RefundTime { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
}

/// <summary>
/// 菜品订单驳回退款请求
/// </summary>
public class DishOrderRefundRejectRequest
{
    [JsonPropertyName("orderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [JsonPropertyName("refundId")]
    public string RefundId { get; set; } = string.Empty;

    [JsonPropertyName("adminReply")]
    public string? AdminReply { get; set; }

    [JsonPropertyName("processNote")]
    public string? ProcessNote { get; set; }
}
