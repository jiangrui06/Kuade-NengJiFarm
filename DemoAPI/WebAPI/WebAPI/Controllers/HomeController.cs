using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.IO;

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

    private static readonly string[] FunctionColors =
    [
        "#4E8B3A",
        "#FF8A3D",
        "#2F7D8C",
        "#C66B3D"
    ];

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
                .Where(x => x.Status == 1 && x.Position == "home")
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CarouselId)
                .Select(x => new
                {
                    x.CarouselId,
                    x.ImageUrl,
                    x.LinkUrl,
                    x.Title
                })
                .ToListAsync(cancellationToken);

            var homeSwiperList = homeSwiperRows.Select(x => new SwiperItem
            {
                Id = (int)x.CarouselId,
                Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
                LinkUrl = x.LinkUrl ?? string.Empty,
                Title = x.Title
            }).ToList();

            var acreProjectRows = await _dbContext.AcreProjects
                .AsNoTracking()
                .Where(x => x.Status == 1)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.AcreProjectId)
                .Take(6)
                .Select(x => new
                {
                    x.AcreProjectId,
                    x.Name,
                    x.Description,
                    x.Price,
                    x.ImageUrl
                })
                .ToListAsync(cancellationToken);

            var acreProjects = acreProjectRows.Select(x => new AcreProjectItem
            {
                Id = (int)x.AcreProjectId,
                Name = x.Name,
                Description = x.Description,
                Price = x.Price,
                Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty
            }).ToList();

            var homeVideos = await LoadHomeVideosAsync(cancellationToken);

            var allFarmGoods = await LoadCommodityCardsAsync(
                _dbContext.Commodities
                    .AsNoTracking()
                    .Where(x => (x.ProductStatus ?? 0) == 1)
                    .OrderByDescending(x => x.CommodityId),
                cancellationToken);

            allFarmGoods = EnsureBlackPorkImage(allFarmGoods);

            var hotDishRows = await _dbContext.Dishes
                .AsNoTracking()
                .Where(x => x.Status == 1)
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
                FunctionButtons = page == 1 ? BuildFunctionButtons() : [],
                AcreProjects = page == 1 ? acreProjects : [],
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
        var commodityIds = commodities.Select(x => x.CommodityId).Distinct().ToList();
        var tags = await LoadCommodityTagsAsync(commodityIds, cancellationToken);
        var commodityStats = await _inventoryStatsService.GetCommodityStatsAsync(commodityIds, cancellationToken);

        return commodities.Select(x => new FarmGoodsItem
        {
            Id = x.CommodityId,
            Name = x.ProductName,
            Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
            Price = ResolveCommodityPrice(x.ProductName),
            OriginalPrice = ResolveCommodityPrice(x.ProductName) + 3m,
            Tags = tags.TryGetValue(x.CommodityId, out var itemTags) ? itemTags : [],
            Sold = commodityStats.GetValueOrDefault(x.CommodityId)?.Sold ?? Math.Max(0, x.Quantity ?? 0),
            Stock = commodityStats.GetValueOrDefault(x.CommodityId)?.Stock ?? (x.InStock ?? 0)
        }).ToList();
    }

    private async Task<List<HomeVideoItem>> LoadHomeVideosAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Videos
            .AsNoTracking()
            .Where(x => x.Status == 1 && !string.IsNullOrWhiteSpace(x.VideoUrl))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.VideoId)
            .Select(x => new
            {
                x.VideoId,
                x.CoverUrl,
                x.VideoUrl
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new HomeVideoItem
            {
                Id = (int)x.VideoId,
                CoverImage = NormalizeMediaUrl(x.CoverUrl) ?? string.Empty,
                VideoUrl = NormalizeMediaUrl(x.VideoUrl) ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.VideoUrl))
            .ToList();
    }

    private static List<FarmGoodsItem> EnsureBlackPorkImage(List<FarmGoodsItem> goods)
    {
        var fallbackImage = goods.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Image))?.Image ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fallbackImage))
        {
            return goods;
        }

        foreach (var item in goods.Where(x =>
                     (x.Name.Contains("黑猪", StringComparison.OrdinalIgnoreCase)
                      || x.Name.Contains("猪肉", StringComparison.OrdinalIgnoreCase))
                     && string.IsNullOrWhiteSpace(x.Image)))
        {
            item.Image = fallbackImage;
        }

        return goods;
    }

    private static List<FunctionButton> BuildFunctionButtons()
    {
        return
        [
            new() { Id = 1, Name = "认购一亩田", Color = FunctionColors[0], Path = "/pages/acre/acre" },
            new() { Id = 2, Name = "农场优选", Color = FunctionColors[1], Path = "/pages/farm-goods/farm-goods" },
            new() { Id = 3, Name = "点餐", Color = FunctionColors[2], Path = "/pages/order/order" },
            new() { Id = 4, Name = "活动中心", Color = FunctionColors[3], Path = "/pages/activity/activity" }
        ];
    }

    private static decimal ResolveCommodityPrice(string? productName)
    {
        return productName switch
        {
            "有机生菜" => 12.8m,
            "黄金甜玉米" => 8.8m,
            "农家西红柿" => 9.9m,
            "红富士苹果" => 19.9m,
            "香甜橙子" => 15.9m,
            "散养土鸡蛋" => 16.8m,
            "黑猪梅花肉" => 38m,
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
            return [];
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

    private string? NormalizeMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();

        // 如果已经是完整的 URL，直接处理可能的重复前缀并返回
        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var duplicateHttps = trimmed.IndexOf("https://", 8, StringComparison.OrdinalIgnoreCase);
            if (duplicateHttps > 0) trimmed = trimmed[..duplicateHttps];

            var duplicateHttp = trimmed.IndexOf("http://", 7, StringComparison.OrdinalIgnoreCase);
            if (duplicateHttp > 0) trimmed = trimmed[..duplicateHttp];

            return trimmed.Trim();
        }

        // 处理本地文件名，拼接完整的 API 访问路径
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var ext = Path.GetExtension(trimmed).ToLowerInvariant();

        // 视频文件
        if (ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".mkv" || ext == ".wmv")
        {
            return $"{baseUrl}/api/file/video/{trimmed}";
        }

        // 默认作为图片处理
        return $"{baseUrl}/api/file/image/{trimmed}";
    }

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

    public sealed class AcreProjectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Image { get; set; } = string.Empty;
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
        public List<AcreProjectItem> AcreProjects { get; set; } = [];
        public List<HomeVideoItem> Videos { get; set; } = [];
        public List<FarmGoodsItem> FarmGoods { get; set; } = [];
        public List<HotDishItem> HotDishes { get; set; } = [];
        public bool HasMore { get; set; }
    }
}
