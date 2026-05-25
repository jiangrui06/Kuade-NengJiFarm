using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class ProductOrderListResponseDto
{
    [JsonPropertyName("records")]
    public List<ProductOrderListItemDto> Records { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("pageNum")]
    public int PageNum { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }
}

public class ProductOrderListItemDto
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("orderCategory")]
    public string? OrderCategory { get; set; }

    [JsonPropertyName("orderSource")]
    public string OrderSource { get; set; } = string.Empty;

    [JsonPropertyName("customerWechat")]
    public string CustomerWechat { get; set; } = string.Empty;

    [JsonPropertyName("contactPhone")]
    public string ContactPhone { get; set; } = string.Empty;

    [JsonPropertyName("receiverName")]
    public string ReceiverName { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("fieldName")]
    public string? FieldName { get; set; }

    [JsonPropertyName("plotNo")]
    public string? PlotNo { get; set; }

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("productSummary")]
    public string ProductSummary { get; set; } = string.Empty;

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [JsonPropertyName("actualAmount")]
    public decimal ActualAmount { get; set; }

    [JsonPropertyName("deliveryMethod")]
    public string DeliveryMethod { get; set; } = string.Empty;

    [JsonPropertyName("logisticsType")]
    public string LogisticsType { get; set; } = string.Empty;

    [JsonPropertyName("logisticsNo")]
    public string LogisticsNo { get; set; } = string.Empty;

    [JsonPropertyName("deliveryNote")]
    public string DeliveryNote { get; set; } = string.Empty;

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = "微信支付";

    [JsonPropertyName("paymentStatus")]
    public string PaymentStatus { get; set; } = string.Empty;

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; set; } = string.Empty;

    [JsonPropertyName("orderTime")]
    public string OrderTime { get; set; } = string.Empty;

    [JsonPropertyName("signTime")]
    public string? SignTime { get; set; }

    [JsonPropertyName("completeTime")]
    public string? CompleteTime { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("refundReason")]
    public string? RefundReason { get; set; }

    [JsonPropertyName("refundApplyTime")]
    public string? RefundApplyTime { get; set; }

    [JsonPropertyName("refundProofImages")]
    public List<string>? RefundProofImages { get; set; }
}
