namespace WebAPI.Dtos.Kitchen;

public class KitchenOrderListItemDto
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalDish { get; set; }
    public int FinishDish { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
}

public class KitchenOrderDetailDto
{
    public long OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public decimal TotalAmount { get; set; }
    public List<KitchenDishDetailDto> DishList { get; set; } = new();
}

public class KitchenDishDetailDto
{
    public long DishOrderDetailsId { get; set; }
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubtotalAmount { get; set; }
    public int? DishStatus { get; set; } // 1=Î´³ö²Í£¬2=ÒÑ³ö²Í
    public string DishStatusName { get; set; } = string.Empty;
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