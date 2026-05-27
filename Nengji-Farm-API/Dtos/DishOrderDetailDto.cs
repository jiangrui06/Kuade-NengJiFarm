using System.Text.Json.Serialization;

namespace WebAPI.Dtos;

public class DishOrderDetailResponseDto
{
    [JsonPropertyName("orderInfo")]
    public DishOrderInfoDto OrderInfo { get; set; } = new();

    [JsonPropertyName("orderItems")]
    public List<DishOrderItemDto> OrderItems { get; set; } = new();

    [JsonPropertyName("buyerInfo")]
    public DishOrderBuyerInfoDto BuyerInfo { get; set; } = new();
}

public class DishOrderInfoDto
{
    [JsonPropertyName("orderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [JsonPropertyName("orderPrimaryId")]
    public long OrderPrimaryId { get; set; }

    [JsonPropertyName("orderType")]
    public string OrderType { get; set; } = "现场菜品点餐";

    [JsonPropertyName("createTime")]
    public string CreateTime { get; set; } = string.Empty;

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; set; } = string.Empty;

    [JsonPropertyName("tableNo")]
    public string TableNo { get; set; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = "微信支付";
}

public class DishOrderItemDto
{
    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("remark")]
    public string Remark { get; set; } = string.Empty;

    [JsonPropertyName("cookingNote")]
    public string CookingNote { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }
}

public class DishOrderBuyerInfoDto
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
    public string OrderSource { get; set; } = "微信扫码点餐";

    [JsonPropertyName("dinerCount")]
    public int DinerCount { get; set; }

    [JsonPropertyName("seatArea")]
    public string SeatArea { get; set; } = string.Empty;

    [JsonPropertyName("remark")]
    public string Remark { get; set; } = string.Empty;
}
