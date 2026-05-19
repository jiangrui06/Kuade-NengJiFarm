using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class ProductOrderDetailResponseDto
{
    [JsonPropertyName("orderInfo")]
    public ProductOrderInfoDto OrderInfo { get; set; } = new();

    [JsonPropertyName("orderItems")]
    public List<ProductOrderItemDto> OrderItems { get; set; } = new();

    [JsonPropertyName("logisticsRecords")]
    public List<LogisticsRecordDto> LogisticsRecords { get; set; } = new();

    [JsonPropertyName("buyerInfo")]
    public ProductOrderBuyerInfoDto BuyerInfo { get; set; } = new();

    [JsonPropertyName("fulfillmentInfo")]
    public FulfillmentInfoDto FulfillmentInfo { get; set; } = new();
}

public class ProductOrderInfoDto
{
    [JsonPropertyName("orderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [JsonPropertyName("orderType")]
    public string OrderType { get; set; } = string.Empty;

    [JsonPropertyName("createTime")]
    public string CreateTime { get; set; } = string.Empty;

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; set; } = string.Empty;

    [JsonPropertyName("paymentStatus")]
    public string PaymentStatus { get; set; } = string.Empty;

    [JsonPropertyName("deliveryMethod")]
    public string DeliveryMethod { get; set; } = string.Empty;

    [JsonPropertyName("logisticsType")]
    public string LogisticsType { get; set; } = string.Empty;

    [JsonPropertyName("deliveryNote")]
    public string DeliveryNote { get; set; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = "微信支付";
}

public class ProductOrderItemDto
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("spec")]
    public string Spec { get; set; } = string.Empty;

    [JsonPropertyName("netWeight")]
    public string NetWeight { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }
}

public class LogisticsRecordDto
{
    [JsonPropertyName("logisticsType")]
    public string LogisticsType { get; set; } = string.Empty;

    [JsonPropertyName("nodeName")]
    public string NodeName { get; set; } = string.Empty;

    [JsonPropertyName("handler")]
    public string Handler { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("updateTime")]
    public string UpdateTime { get; set; } = string.Empty;

    [JsonPropertyName("remark")]
    public string Remark { get; set; } = string.Empty;
}

public class ProductOrderBuyerInfoDto
{
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("customerWechat")]
    public string CustomerWechat { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("memberLevel")]
    public string MemberLevel { get; set; } = "普通会员";

    [JsonPropertyName("orderSource")]
    public string OrderSource { get; set; } = string.Empty;

    [JsonPropertyName("remark")]
    public string Remark { get; set; } = string.Empty;
}

public class FulfillmentInfoDto
{
    [JsonPropertyName("receiverName")]
    public string ReceiverName { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("schedule")]
    public string Schedule { get; set; } = string.Empty;

    [JsonPropertyName("trackingNo")]
    public string TrackingNo { get; set; } = string.Empty;

    [JsonPropertyName("remark")]
    public string Remark { get; set; } = string.Empty;
}
