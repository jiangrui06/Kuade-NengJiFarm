using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/home")]
public class HomeController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInventoryStatsService _inventoryStatsService;

    public HomeController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
    }

    [HttpGet]
    public Task<IActionResult> GetHomePage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        return BuildHomeResponseAsync(page, pageSize, cancellationToken);
    }

    [HttpGet("index")]
    public Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        return BuildHomeResponseAsync(page, pageSize, cancellationToken);
    }

    [HttpGet("video")]
    public async Task<IActionResult> GetHomeVideo(CancellationToken cancellationToken)
    {
        var items = await LoadHomeVideosAsync(cancellationToken);
        return Ok(ApiResult.Success(new { items }));
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchHome(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 50);
            keyword = (keyword ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Ok(ApiResult.Success(new HomeSearchResponse
                {
                    Keyword = keyword,
                    Page = page,
                    PageSize = pageSize
                }));
            }

            var searchTypeNames = await LoadSearchTypeNamesAsync(cancellationToken);

            var farmGoodsRows = await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1
                    && x.ProductName.Contains(keyword))
                .OrderByDescending(x => x.ProductName.Contains(keyword))
                .ThenByDescending(x => x.CommodityId)
                .Select(x => new
                {
                    x.CommodityId,
                    x.ProductName,
                    x.ImageUrl,
                    x.UnitPrice,
                    x.OriginalPrice,
                    x.SpecDescription,
                    x.WeightText,
                    x.UnitId,
                    x.InStock
                })
                .ToListAsync(cancellationToken);

            var dishRows = await _dbContext.Dishes
                .AsNoTracking()
                .Where(x => x.IsDelete == 0 && x.Status == 1
                    && (x.DishName.Contains(keyword) || (x.DishDescription ?? string.Empty).Contains(keyword)))
                .OrderByDescending(x => x.DishName.Contains(keyword))
                .ThenByDescending(x => x.DishSold)
                .ThenByDescending(x => x.DishId)
                .Select(x => new
                {
                    x.DishId,
                    x.DishName,
                    x.ImageUrl,
                    x.DishPrice,
                    x.DishDescription,
                    x.DishRemainingQuantity
                })
                .ToListAsync(cancellationToken);

            var activityRows = await _dbContext.Activities
                .AsNoTracking()
                .Where(x => x.IsDelete == 0 && x.StatusId == 1
                    && (x.Title.Contains(keyword)
                        || x.Description.Contains(keyword)
                        || x.Location.Contains(keyword)
                        || (x.Content ?? string.Empty).Contains(keyword)))
                .OrderByDescending(x => x.Title.Contains(keyword))
                .ThenBy(x => x.SortOrder)
                .ThenByDescending(x => x.ActivityId)
                .Select(x => new
                {
                    x.ActivityId,
                    x.Title,
                    x.ImageUrl,
                    x.Price,
                    x.Description,
                    x.StartDate
                })
                .ToListAsync(cancellationToken);

            // 加载 unit 表
            var unitMap = await _dbContext.Units
                .AsNoTracking()
                .Where(u => u.IsEnabled == 1)
                .ToDictionaryAsync(u => u.UnitId, u => u.UnitName, cancellationToken);

            var items = farmGoodsRows.Select(x =>
            {
                var price = x.UnitPrice ?? 0m;
                var unitName = x.UnitId.HasValue ? unitMap.GetValueOrDefault(x.UnitId.Value) : null;
                var spec = GoodsController.BuildSpec(x.WeightText, unitName);
                var description = GoodsController.ExtractDescription(x.SpecDescription, spec);
                return new HomeSearchItem
                {
                    Id = x.CommodityId.ToString(),
                    Type = "goods",
                    TypeName = searchTypeNames.GetValueOrDefault("goods", "农场优选"),
                    Name = x.ProductName ?? string.Empty,
                    Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
                    Price = price,
                    OriginalPrice = x.OriginalPrice ?? price,
                    Spec = spec,
                    Description = description,
                    Stock = x.InStock ?? 0,
                    DetailPath = $"/user-pages/goods-detail/goods-detail?id={x.CommodityId}"
                };
            })
            .Concat(dishRows.Select(x => new HomeSearchItem
            {
                Id = x.DishId.ToString(),
                Type = "dish",
                TypeName = searchTypeNames.GetValueOrDefault("dish", "热销菜品"),
                Name = x.DishName,
                Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
                Price = x.DishPrice,
                OriginalPrice = x.DishPrice,
                Description = x.DishDescription ?? string.Empty,
                Stock = x.DishRemainingQuantity,
                DetailPath = $"/user-pages/order-foods-detail/order-foods-detail?id={x.DishId}"
            }))
            .Concat(activityRows.Select(x => new HomeSearchItem
            {
                Id = x.ActivityId.ToString(),
                Type = "activity",
                TypeName = searchTypeNames.GetValueOrDefault("activity", "活动"),
                Name = x.Title,
                Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
                Price = x.Price,
                OriginalPrice = x.Price,
                Description = x.Description ?? string.Empty,
                Date = x.StartDate.ToString("yyyy-MM-dd HH:mm"),
                DetailPath = $"/user-pages/activity-detail/activity-detail?id={x.ActivityId}"
            }))
            .ToList();

            var total = items.Count;
            var pagedItems = items
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(ApiResult.Success(new HomeSearchResponse
            {
                Keyword = keyword,
                Items = pagedItems,
                List = pagedItems,
                Total = total,
                Page = page,
                PageSize = pageSize,
                HasMore = page * pageSize < total
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"搜索失败：{ex.Message}"));
        }
    }

    private async Task<IActionResult> BuildHomeResponseAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 6 : pageSize;

            var homeSwiperRows = await _dbContext.Carousels
                .AsNoTracking()
                .Where(x => x.Position == "home")
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CarouselId)
                .Select(x => new
                {
                    x.CarouselId,
                    x.ImageUrl,
                    x.LinkUrl
                })
                .ToListAsync(cancellationToken);

            var homeSwiperList = homeSwiperRows.Select(x => new SwiperItem
            {
                Id = (int)x.CarouselId,
                Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
                LinkUrl = x.LinkUrl ?? string.Empty,
                Title = string.Empty
            }).ToList();

            var homeVideos = await LoadHomeVideosAsync(cancellationToken);

            var allFarmGoods = await LoadCommodityCardsAsync(
                _dbContext.Commodities
                    .AsNoTracking()
                    .Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1)
                    .OrderByDescending(x => x.CommodityId),
                cancellationToken);

            var hotDishRows = await _dbContext.Dishes
                .AsNoTracking()
                .Where(x => x.IsDelete == 0 && x.Status == 1)
                .OrderByDescending(x => x.DishSold)
                .ThenByDescending(x => x.DishId)
                .Select(x => new
                {
                    x.DishId,
                    x.DishName,
                    x.ImageUrl,
                    x.DishPrice,
                    x.AttributeName
                })
                .ToListAsync(cancellationToken);

            var dishStats = await _inventoryStatsService.GetDishStatsAsync(
                hotDishRows.Select(x => x.DishId),
                cancellationToken);

            var allHotDishes = hotDishRows.Select(x => new HotDishItem
            {
                Id = x.DishId,
                Name = x.DishName,
                Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
                Price = x.DishPrice,
                Sold = dishStats.GetValueOrDefault(x.DishId)?.Sold ?? 0,
                Stock = dishStats.GetValueOrDefault(x.DishId)?.Stock ?? 0,
                Tags = string.IsNullOrWhiteSpace(x.AttributeName)
                    ? new List<string>()
                    : new List<string> { x.AttributeName }
            })
            .OrderByDescending(x => x.Sold)
            .ThenByDescending(x => x.Id)
            .ToList();

            var farmGoods = allFarmGoods
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hotDishes = allHotDishes
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var data = new HomeIndexResponse
            {
                SwiperList = page == 1 ? homeSwiperList : [],
                FunctionButtons = page == 1 ? await LoadFunctionButtonsAsync(cancellationToken) : [],
                Videos = page == 1 ? homeVideos : [],
                FarmGoods = farmGoods,
                HotDishes = hotDishes,
                HasMore = page * pageSize < Math.Max(allFarmGoods.Count, allHotDishes.Count)
            };

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取首页数据失败：{ex.Message}"));
        }
    }

    private async Task<List<FarmGoodsItem>> LoadCommodityCardsAsync(
        IQueryable<Commodity> query,
        CancellationToken cancellationToken)
    {
        var commodities = await query.ToListAsync(cancellationToken);

        var commodityIds = commodities
            .Select(x => x.CommodityId)
            .Distinct()
            .ToList();

        var tags = await LoadCommodityTagsAsync(commodityIds, cancellationToken);

        var commodityStats = await _inventoryStatsService.GetCommodityStatsAsync(
            commodityIds,
            cancellationToken);

        return commodities.Select(x =>
        {
            var price = x.UnitPrice ?? 0m;

            return new FarmGoodsItem
            {
                Id = x.CommodityId,
                Name = x.ProductName ?? string.Empty,
                Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
                Price = price,
                OriginalPrice = x.OriginalPrice ?? price,
                Tags = tags.TryGetValue(x.CommodityId, out var itemTags) ? itemTags : [],
                Sold = commodityStats.GetValueOrDefault(x.CommodityId)?.Sold ?? 0,
                Stock = commodityStats.GetValueOrDefault(x.CommodityId)?.Stock ?? (x.Quantity ?? 0)
            };
        }).ToList();
    }

    private async Task<List<HomeVideoItem>> LoadHomeVideosAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Videos
            .AsNoTracking()
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.VideoUrl))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.VideoId)
            .Select(x => new
            {
                x.VideoId,
                x.VideoUrl
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new HomeVideoItem
            {
                Id = (int)x.VideoId,
                CoverImage = string.Empty,
                VideoUrl = NormalizeMediaUrl(x.VideoUrl) ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.VideoUrl))
            .ToList();
    }

    private async Task<List<FunctionButton>> LoadFunctionButtonsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await _dbContext.SysConfigs
                .AsNoTracking()
                .Where(x => x.ConfigKey == "home_function_buttons")
                .Select(x => x.ConfigValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var buttons = JsonSerializer.Deserialize<List<FunctionButton>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return buttons ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<Dictionary<string, string>> LoadSearchTypeNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await _dbContext.SysConfigs
                .AsNoTracking()
                .Where(x => x.ConfigKey == "search_type_names")
                .Select(x => x.ConfigValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string>();
            }

            var names = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return names ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private async Task<Dictionary<int, List<string>>> LoadCommodityTagsAsync(
        IReadOnlyCollection<int> commodityIds,
        CancellationToken cancellationToken)
    {
        if (commodityIds.Count == 0)
        {
            return [];
        }

        var rows = await (
            from relation in _dbContext.CommodityTagRelations.AsNoTracking()
            join tag in _dbContext.Tags.AsNoTracking()
                on relation.TagId equals tag.TagId
            where commodityIds.Contains(relation.CommodityId)
            select new
            {
                relation.CommodityId,
                tag.TagName
            }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.CommodityId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => x.TagName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList());
    }

    private static string? NormalizeMediaUrl(string? url) => MediaUrlHelper.Normalize(url) is { Length: > 0 } r ? r : null;

    public sealed class SwiperItem
    {
        public int Id { get; set; }
        public string Image { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public sealed class FunctionButton
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public sealed class HomeVideoItem
    {
        public int Id { get; set; }
        public string CoverImage { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
    }

    public sealed class FarmGoodsItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public List<string> Tags { get; set; } = [];
        public int Sold { get; set; }
        public int Stock { get; set; }
    }

    public sealed class HotDishItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Sold { get; set; }
        public int Stock { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    public sealed class HomeIndexResponse
    {
        public List<SwiperItem> SwiperList { get; set; } = [];
        public List<FunctionButton> FunctionButtons { get; set; } = [];
        public List<HomeVideoItem> Videos { get; set; } = [];
        public List<FarmGoodsItem> FarmGoods { get; set; } = [];
        public List<HotDishItem> HotDishes { get; set; } = [];
        public bool HasMore { get; set; }
    }

    public sealed class HomeSearchItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public string Spec { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Stock { get; set; }
        public string Date { get; set; } = string.Empty;
        public string DetailPath { get; set; } = string.Empty;
    }

    public sealed class HomeSearchResponse
    {
        public string Keyword { get; set; } = string.Empty;
        public List<HomeSearchItem> Items { get; set; } = [];
        public List<HomeSearchItem> List { get; set; } = [];
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }
}
