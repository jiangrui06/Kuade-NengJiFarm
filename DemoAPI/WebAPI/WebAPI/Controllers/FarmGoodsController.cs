using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/farm-goods")]
public class FarmGoodsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInventoryStatsService _inventoryStatsService;

    private static readonly string[] CategoryColors =
    [
        "#4E8B3A",
        "#FF8A3D",
        "#2F7D8C",
        "#C66B3D",
        "#D94F70"
    ];

    public FarmGoodsController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
    }

    [HttpGet]
    public Task<IActionResult> GetGoodsPage(
        [FromQuery] string category = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        return BuildPagedGoodsResponseAsync(category, page, pageSize, includeCategories: true, includeSwiper: true, cancellationToken);
    }

    [HttpGet("index")]
    public async Task<IActionResult> GetFarmGoodsIndex(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await LoadCategoriesAsync(cancellationToken);
            var goods = await LoadGoodsCardsAsync(
                _dbContext.Commodities
                    .AsNoTracking()
                    .Where(x => (x.ProductStatus ?? 0) == 1)
                    .OrderByDescending(x => x.CommodityId)
                    .Take(12),
                cancellationToken);
            var swiperList = await LoadGoodsSwiperAsync(cancellationToken);

            var data = new FarmGoodsIndexResponse
            {
                SwiperList = swiperList,
                Categories = categories,
                TodayGoods = goods.Take(6).ToList(),
                HotGoods = goods.Skip(6).Take(6).ToList()
            };

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取农场优选数据失败：{ex.Message}"));
        }
    }

    [HttpGet("category")]
    public Task<IActionResult> GetCategoryGoods(
        [FromQuery] string? categoryId,
        [FromQuery] string? category,
        [FromQuery] string? id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var categoryValue = categoryId ?? category ?? id ?? "all";
        return BuildPagedGoodsResponseAsync(categoryValue, page, pageSize, includeCategories: false, includeSwiper: false, cancellationToken);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await LoadCategoriesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                categories,
                list = categories
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取分类失败：{ex.Message}"));
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchGoods(
        [FromQuery] string keyword = "",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            keyword = (keyword ?? string.Empty).Trim();
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var query = _dbContext.Commodities
                .AsNoTracking()
                .Where(x => (x.ProductStatus ?? 0) == 1);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.ProductName.Contains(keyword) ||
                    (x.SpecDescription ?? string.Empty).Contains(keyword));
            }

            query = query.OrderByDescending(x => x.CommodityId);

            var total = await query.CountAsync(cancellationToken);
            var goodsList = await LoadGoodsCardsAsync(
                query.Skip((page - 1) * pageSize).Take(pageSize),
                cancellationToken);

            return Ok(ApiResult.Success(new
            {
                keyword,
                items = goodsList,
                goodsList,
                list = goodsList,
                total,
                page,
                pageSize,
                hasMore = page * pageSize < total
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"搜索商品失败：{ex.Message}"));
        }
    }

    private async Task<IActionResult> BuildPagedGoodsResponseAsync(
        string? category,
        int page,
        int pageSize,
        bool includeCategories,
        bool includeSwiper,
        CancellationToken cancellationToken)
    {
        try
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 6 : pageSize;

            var categories = await LoadCategoriesAsync(cancellationToken);
            var normalizedCategory = NormalizeCategory(category, categories);
            var query = BuildCategoryQuery(normalizedCategory);
            var total = await query.CountAsync(cancellationToken);
            var items = await LoadGoodsCardsAsync(
                query.Skip((page - 1) * pageSize).Take(pageSize),
                cancellationToken);

            var currentCategory = categories.FirstOrDefault(x => x.Id == normalizedCategory);
            var swiperList = includeSwiper
                ? await LoadGoodsSwiperAsync(cancellationToken)
                : [];

            return Ok(ApiResult.Success(new
            {
                swiperList = swiperList.Select((item, index) => new
                {
                    id = item.Id == 0 ? index + 1 : item.Id,
                    image = item.Image
                }),
                categories = includeCategories ? categories : [],
                category = normalizedCategory,
                currentCategory = normalizedCategory,
                categoryId = normalizedCategory,
                categoryName = currentCategory?.Name ?? string.Empty,
                items,
                goodsList = items,
                list = items,
                page,
                pageSize,
                total,
                hasMore = page * pageSize < total
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取农场优选商品失败：{ex.Message}"));
        }
    }

    private async Task<List<CategoryItem>> LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => (x.CategoryStatus ?? 0) == 1)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var items = new List<CategoryItem>
        {
            new()
            {
                Id = "all",
                Name = "全部商品",
                Icon = "全",
                Color = CategoryColors[0]
            }
        };

        items.AddRange(categories.Select((item, index) => new CategoryItem
        {
            Id = item.Id.ToString(),
            Name = item.CategoryName,
            Icon = string.IsNullOrWhiteSpace(item.CategoryDescription) ? string.Empty : item.CategoryDescription![..1],
            Color = CategoryColors[(index + 1) % CategoryColors.Length]
        }));

        return items;
    }

    private static string NormalizeCategory(string? category, IReadOnlyCollection<CategoryItem> categories)
    {
        var value = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim();
        if (categories.Any(x => x.Id.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            return categories.First(x => x.Id.Equals(value, StringComparison.OrdinalIgnoreCase)).Id;
        }

        if (categories.Any(x => x.Name.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            return categories.First(x => x.Name.Equals(value, StringComparison.OrdinalIgnoreCase)).Id;
        }

        var aliasCategoryId = ResolveAliasCategoryId(value, categories);
        if (!string.IsNullOrWhiteSpace(aliasCategoryId))
        {
            return aliasCategoryId;
        }

        // "粮油"分类必须严格匹配，避免误回退到 all 导致混入其他商品。
        if (IsGrainsAlias(value))
        {
            return "__empty__";
        }

        return "all";
    }

    private static string? ResolveAliasCategoryId(string category, IReadOnlyCollection<CategoryItem> categories)
    {
        if (string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            return "all";
        }

        var normalized = category.Trim().ToLowerInvariant();
        var keywords = normalized switch
        {
            "vegetables" or "vegetable" or "veg" or "蔬菜" => new[] { "蔬", "菜", "瓜", "豆", "菌" },
            "fruits" or "fruit" or "水果" => new[] { "果", "橙", "桃", "梨", "莓", "葡萄", "苹果" },
            "meat" or "meats" or "肉类" or "鲜肉" => new[] { "肉", "鸡", "鸭", "鱼", "虾", "牛", "猪", "羊", "蛋" },
            "grains" or "grain" or "cereal" or "staple" or "oil" or "liangyou" or "粮油" => new[] { "粮", "米", "面", "油", "谷", "杂粮" ,"花生油"},
            _ => Array.Empty<string>()
        };

        if (keywords.Length == 0)
        {
            return null;
        }

        return categories
            .Where(x => !string.Equals(x.Id, "all", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(x => keywords.Any(keyword => x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            ?.Id;
    }

    private static bool IsGrainsAlias(string category)
    {
        return category.Trim().ToLowerInvariant() is "grains" or "grain" or "cereal" or "staple" or "oil" or "liangyou" or "粮油";
    }

    private IQueryable<Commodity> BuildCategoryQuery(string category)
    {
        var query = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => (x.ProductStatus ?? 0) == 1);

        if (string.Equals(category, "__empty__", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(_ => false);
        }

        if (category != "all" && int.TryParse(category, out var categoryId))
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        return query.OrderByDescending(x => x.CommodityId);
    }

    private async Task<List<GoodsCardItem>> LoadGoodsCardsAsync(
        IQueryable<Commodity> query,
        CancellationToken cancellationToken)
    {
        var commodities = await query.ToListAsync(cancellationToken);
        var commodityIds = commodities.Select(x => x.CommodityId).Distinct().ToList();
        var tags = await LoadCommodityTagsAsync(commodityIds, cancellationToken);
        var commodityStats = await _inventoryStatsService.GetCommodityStatsAsync(commodityIds, cancellationToken);
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => commodityIds.Count > 0)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return commodities.Select(x => new GoodsCardItem
        {
            Id = x.CommodityId,
            Name = x.ProductName,
            Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
            Price = x.UnitPrice ?? ResolveCommodityPrice(x.ProductName),
            OriginalPrice = x.OriginalPrice ?? ((x.UnitPrice ?? ResolveCommodityPrice(x.ProductName)) + 3m),
            Sold = commodityStats.GetValueOrDefault(x.CommodityId)?.Sold ?? Math.Max(0, x.Quantity ?? 0),
            Stock = commodityStats.GetValueOrDefault(x.CommodityId)?.Stock ?? (x.InStock ?? 0),
            CategoryId = x.CategoryId,
            CategoryName = categories.TryGetValue(x.CategoryId, out var category) ? category.CategoryName : string.Empty,
            Tags = tags.TryGetValue(x.CommodityId, out var itemTags) ? itemTags : []
        }).ToList();
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

    private async Task<List<SwiperItem>> LoadGoodsSwiperAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Carousels
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.Position == "goods")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => new
            {
                x.CarouselId,
                x.ImageUrl
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new SwiperItem
        {
            Id = (int)x.CarouselId,
            Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty
        }).ToList();
    }

    private static decimal ResolveCommodityPrice(string? productName)
    {
        return productName switch
        {
            "有机生菜" => 0.01m,
            "甜朝玉米" => 8.8m,
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

    private sealed class FarmGoodsIndexResponse
    {
        public List<SwiperItem> SwiperList { get; set; } = [];
        public List<CategoryItem> Categories { get; set; } = [];
        public List<GoodsCardItem> TodayGoods { get; set; } = [];
        public List<GoodsCardItem> HotGoods { get; set; } = [];
    }

    private sealed class SwiperItem
    {
        public int Id { get; set; }
        public string Image { get; set; } = string.Empty;
    }

    private sealed class CategoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    private sealed class GoodsCardItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public int Sold { get; set; }
        public int Stock { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = [];
    }
}
