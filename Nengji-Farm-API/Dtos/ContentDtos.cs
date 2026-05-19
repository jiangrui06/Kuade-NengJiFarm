using System.ComponentModel.DataAnnotations;

namespace WebAPI.Dtos;

public class ActivitySummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Duration { get; set; }
}

public class ActivityListDto
{
    public List<ActivityCategoryDto> Categories { get; set; } = [];
    public Dictionary<string, List<ActivitySummaryDto>> Activities { get; set; } = [];
}

public class ActivityCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ActivityDetailDto : ActivitySummaryDto
{
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string People { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Participants { get; set; }
    public int RemainingSlots { get; set; }
    public List<string> Images { get; set; } = [];
    public string Video { get; set; } = string.Empty;
}

public class MenuCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class MenuGoodsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Sold { get; set; }
    public int Stock { get; set; }
}

public class OrderMenuDataDto
{
    public List<MenuCategoryDto> Categories { get; set; } = [];
    public Dictionary<string, List<MenuGoodsDto>> GoodsList { get; set; } = [];
}

public class OrderGoodsDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Image { get; set; } = string.Empty;
    public string DetailImage { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
    public int Sold { get; set; }
    public int Stock { get; set; }
}

public class OrderCartItemDto
{
    public int GoodsId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int Stock { get; set; }
}

public class OrderCartDto
{
    public List<OrderCartItemDto> Items { get; set; } = [];
    public int CartCount { get; set; }
    public decimal TotalPrice { get; set; }
}

public class OrderCartAddRequest
{
    [Required]
    public int GoodsId { get; set; }

    [Range(1, int.MaxValue)]
    public int Count { get; set; } = 1;
}

public class OrderCartUpdateRequest
{
    [Required]
    public int GoodsId { get; set; }

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}

public class SubmitMealOrderRequest
{
    public string? Remark { get; set; }
}

public class SubmitMealOrderResponse
{
    public string OrderNo { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalPrice { get; set; }
}
