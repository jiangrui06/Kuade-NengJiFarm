using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

/// <summary>
/// 产品订单退款请求
/// </summary>
public class ProductOrderRefundRequest
{
    [JsonPropertyName("orderNo")]
    public string? OrderNo { get; set; }

    [JsonPropertyName("orderId")]
    public long OrderId { get; set; }

    [JsonPropertyName("refundReason")]
    public string? RefundReason { get; set; }
}

/// <summary>
/// 产品订单退款响应
/// </summary>
public class ProductOrderRefundResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string RefundAmount { get; set; } = string.Empty;
    public string RefundTime { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
}
