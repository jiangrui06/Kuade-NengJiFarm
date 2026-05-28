using System.ComponentModel.DataAnnotations;

namespace WebAPI.Dtos;

public class ActivitySummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string Image { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Duration { get; set; }
}

public class ActivityListDto
{
    public List<ActivityCategoryDto>? Categories { get; set; }
    public Dictionary<string, List<ActivitySummaryDto>> Activities { get; set; } = [];
}

public class ActivityCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class OrderMenuDataDto
{
    public List<MenuCategoryDto> Categories { get; set; } = [];
    public Dictionary<string, List<MenuGoodsDto>> GoodsList { get; set; } = [];
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

public class OrderGoodsDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Image { get; set; } = string.Empty;
    public string? DetailImage { get; set; }
    public string? Description { get; set; }
    public string? Weight { get; set; }
    public string? Storage { get; set; }
    public int Sold { get; set; }
    public int Stock { get; set; }
}

public class OrderCartDto
{
    public List<OrderCartItemDto> Items { get; set; } = [];
    public int CartCount { get; set; }
    public decimal TotalPrice { get; set; }
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

public class OrderCartAddRequest
{
    public int GoodsId { get; set; }
    public int Count { get; set; }
}

public class OrderCartUpdateRequest
{
    public int GoodsId { get; set; }
    public int Quantity { get; set; }
}

public class SubmitMealOrderRequest
{
}

public class SubmitMealOrderResponse
{
    public string OrderNo { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalPrice { get; set; }
}

public class CarouselMediaItem
{
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class ActivityDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Image { get; set; }
    public List<string> Images { get; set; } = [];
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? People { get; set; }
    public string? Content { get; set; }
    public int Participants { get; set; }
    public int RemainingSlots { get; set; }
    public string? Video { get; set; }

    /// <summary>
    /// 所有视频地址数组
    /// </summary>
    public List<string> Videos { get; set; } = [];

    /// <summary>
    /// 轮播媒体数组（支持图视交叉排序）
    /// </summary>
    public List<CarouselMediaItem> CarouselMedia { get; set; } = [];

    /// <summary>
    /// 规格图片列表
    /// </summary>
    public List<string> SpecImages { get; set; } = [];
}

