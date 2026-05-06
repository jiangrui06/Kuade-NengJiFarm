namespace WebAPI.Dtos.Kitchen;

/// <summary>
/// 后厨菜品详情DTO（用于订单详情页面）
/// </summary>
public class KitchenDishDetailDto
{
    /// <summary>
    /// 订单菜品明细ID
    /// </summary>
    public long DishOrderDetailsId { get; set; }

    /// <summary>
    /// 菜品ID
    /// </summary>
    public int DishId { get; set; }

    /// <summary>
    /// 菜品名称
    /// </summary>
    public string DishName { get; set; } = string.Empty;

    /// <summary>
    /// 购买数量
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 菜品单价
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 小计金额
    /// </summary>
    public decimal SubtotalAmount { get; set; }

    /// <summary>
    /// 出餐状态ID（0或1=未出, 2=已出）
    /// </summary>
    public int DishStatus { get; set; }

    /// <summary>
    /// 出餐状态名称
    /// </summary>
    public string DishStatusName { get; set; } = string.Empty;
}