using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class DishOrderListResponseDto
{
    [JsonPropertyName("records")]
    public List<DishOrderListItemDto> Records { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("pageNum")]
    public int PageNum { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }
}

public class DishOrderListItemDto
{
    [JsonPropertyName("dishOrderId")]
    public long DishOrderId { get; set; }

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("orderPrimaryId")]
    public long OrderPrimaryId { get; set; }

    [JsonPropertyName("customerWechat")]
    public string CustomerWechat { get; set; } = string.Empty;

    [JsonPropertyName("contactPhone")]
    public string ContactPhone { get; set; } = string.Empty;

    [JsonPropertyName("tableNo")]
    public string TableNo { get; set; } = string.Empty;

    [JsonPropertyName("dishCount")]
    public int DishCount { get; set; }

    [JsonPropertyName("actualAmount")]
    public decimal ActualAmount { get; set; }

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = "微信支付";

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; set; } = string.Empty;

    [JsonPropertyName("orderTime")]
    public string OrderTime { get; set; } = string.Empty;
}
