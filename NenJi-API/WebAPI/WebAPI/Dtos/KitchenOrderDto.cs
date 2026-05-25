namespace WebAPI.Dtos;


public class KitchenOrderListItemDto
{
    /// <summary>
    /// 订单号（前端用 id）
    /// </summary>
    public long Id { get; set; }

    public string No { get; set; } = string.Empty;

    /// <summary>
    /// 下单时间（ISO 8601格式，前端自行格式化）
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// 桌台号
    /// </summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    //public string? Remark { get; set; }

    /// <summary>
    /// 菜品列表
    /// </summary>
    public List<KitchenOrderItemDto> Items { get; set; } = new List<KitchenOrderItemDto>();

    /// <summary>
    /// 订单总金额
    /// </summary>
    public decimal Total { get; set; }
}



public class KitchenOrderDetailDto
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    //public long DishOrderDetailsId { get; set; }
    public long TableNumber { get; set; }
    public DateTime CreateTime { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Remark { get; set; }

    public List<KitchenOrderItemDto> DishList { get; set; } = new();
}

/// <summary>
/// 后厨菜品信息
/// </summary>
public class KitchenOrderItemDto
{
    /// <summary>
    /// 菜品名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 购买数量
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 出餐状态（0=未出, 1=已出）
    /// </summary>
    public int Status { get; set; }

    public long DishOrderDetailsId { get; set; }

    /// <summary>
    /// 菜品单价
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 是否已取消（可选字段）
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? Cancelled { get; set; }
}

public class MarkDishFinishDto
{
    public long DishOrderDetailsId { get; set; }
}

public class MarkDishFinishResponseDto
{
    public bool AllFinished { get; set; }
    public int FinishDish { get; set; }
    public int TotalDish { get; set; }
}

public class KitchenStatisticsDto
{
    public decimal TodayTotalAmount { get; set; }
    public int TodayTotalOrder { get; set; }
    public int TodayFinishedOrder { get; set; }
    public int TodayPendingDish { get; set; }
    public int TodayFinishedDish { get; set; }
}