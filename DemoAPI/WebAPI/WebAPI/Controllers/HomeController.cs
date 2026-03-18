using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/home")]
public class HomeController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    private static readonly string[] FunctionColors =
    {
        "#4E8B3A",
        "#FF8A3D",
        "#2F7D8C",
        "#C66B3D"
    };

    public HomeController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var farmGoods = await LoadCommodityCardsAsync(
                _dbContext.Commodities
                    .AsNoTracking()
                    .Where(x => (x.ProductStatus ?? 0) == 1)
                    .OrderByDescending(x => x.CommodityId)
                    .Take(6),
                cancellationToken);

            var hotDishes = await _dbContext.Dishes
                .AsNoTracking()
                .Where(x => x.Status == 1)
                .OrderByDescending(x => x.DishSold)
                .ThenByDescending(x => x.DishId)
                .Take(6)
                .Select(x => new HotDishItem
                {
                    Id = x.DishId,
                    Name = x.DishName,
                    Image = x.ImageUrl,
                    Price = x.DishPrice,
                    Tags = string.IsNullOrWhiteSpace(x.AttributeName)
                        ? new List<string>()
                        : new List<string> { x.AttributeName }
                })
                .ToListAsync(cancellationToken);

            var data = new HomeIndexResponse
            {
                SwiperList = farmGoods.Take(3)
                    .Select((item, index) => new SwiperItem
                    {
                        Id = index + 1,
                        Image = item.Image
                    })
                    .ToList(),
                FunctionButtons =
                {
                    new FunctionButton { Id = 1, Name = "认购一亩田", Color = FunctionColors[0], Path = "/pages/acre/acre" },
                    new FunctionButton { Id = 2, Name = "农场优选", Color = FunctionColors[1], Path = "/pages/farm-goods/farm-goods" },
                    new FunctionButton { Id = 3, Name = "热销菜品", Color = FunctionColors[2], Path = "/pages/order/order" },
                    new FunctionButton { Id = 4, Name = "活动中心", Color = FunctionColors[3], Path = "/pages/activity/activity" }
                },
                FarmGoods = farmGoods,
                HotDishes = hotDishes
            };

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取首页数据失败：{ex.Message}"));
        }
    }

    private async Task<List<FarmGoodsItem>> LoadCommodityCardsAsync(
        IQueryable<WebAPI.Entities.Commodity> query,
        CancellationToken cancellationToken)
    {
        var commodities = await query.ToListAsync(cancellationToken);
        var commodityIds = commodities.Select(x => x.CommodityId).Distinct().ToList();
        var tags = await LoadCommodityTagsAsync(commodityIds, cancellationToken);

        return commodities.Select(x => new FarmGoodsItem
        {
            Id = x.CommodityId,
            Name = x.ProductName,
            Image = x.ImageUrl ?? string.Empty,
            Price = ResolveCommodityPrice(x.ProductName),
            OriginalPrice = ResolveCommodityPrice(x.ProductName) + 3m,
            Tags = tags.TryGetValue(x.CommodityId, out var itemTags) ? itemTags : new List<string>(),
            Stock = x.InStock ?? 0
        }).ToList();
    }

    private static decimal ResolveCommodityPrice(string? productName)
    {
        return productName switch
        {
            "有机生菜" => 12.8m,
            "甜糯玉米" => 8.8m,
            "农家西红柿" => 9.9m,
            "红富士苹果" => 19.9m,
            "香甜橙子" => 15.9m,
            "散养土鸡蛋" => 16.8m,
            "土猪肉" => 38m,
            "鲜牛奶" => 19.9m,
            "农家大米" => 49.9m,
            _ => 19.9m
        };
    }

    private async Task<Dictionary<int, List<string>>> LoadCommodityTagsAsync(
        IReadOnlyCollection<int> commodityIds,
        CancellationToken cancellationToken)
    {
        if (commodityIds.Count == 0)
        {
            return new Dictionary<int, List<string>>();
        }

        var rows = await (
            from relation in _dbContext.CommodityTagRelations.AsNoTracking()
            join tag in _dbContext.Tags.AsNoTracking() on relation.TagId equals tag.TagId
            where commodityIds.Contains(relation.CommodityId)
            select new { relation.CommodityId, tag.TagName }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.CommodityId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.TagName).Distinct().ToList());
    }

    private sealed class HomeIndexResponse
    {
        public List<SwiperItem> SwiperList { get; set; } = new();
        public List<FunctionButton> FunctionButtons { get; set; } = new();
        public List<FarmGoodsItem> FarmGoods { get; set; } = new();
        public List<HotDishItem> HotDishes { get; set; } = new();
    }

    private sealed class SwiperItem
    {
        public int Id { get; set; }
        public string Image { get; set; } = string.Empty;
    }

    private sealed class FunctionButton
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    private sealed class FarmGoodsItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public List<string> Tags { get; set; } = new();
        public int Stock { get; set; }
    }

    private sealed class HotDishItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
