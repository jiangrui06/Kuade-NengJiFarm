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

    private static readonly string[] CategoryColors = ["#4CAF50", "#FF9800", "#2F7D8C", "#C66B3D"];

    public FarmGoodsController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetGoodsPage(
        [FromQuery] string category = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var categories = await LoadCategoriesAsync(cancellationToken);
        var normalizedCategory = NormalizeCategory(category, categories);

        var query = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1);

        if (normalizedCategory != "all" && int.TryParse(normalizedCategory, out var categoryId))
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        query = query.OrderByDescending(x => x.CommodityId);
        var total = await query.CountAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(x => x.id, StringComparer.OrdinalIgnoreCase);
        var items = await LoadGoodsCardsAsync(
            query.Skip((page - 1) * pageSize).Take(pageSize),
            categoriesById,
            cancellationToken);

        return Ok(ApiResult.Success(new
        {
            list = items,
            page,
            pageSize,
            total
        }));
    }

    [HttpGet("index")]
    public async Task<IActionResult> GetFarmGoodsIndex(CancellationToken cancellationToken)
    {
        var categories = await LoadCategoriesAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(x => x.id, StringComparer.OrdinalIgnoreCase);
        var query = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1)
            .OrderByDescending(x => x.CommodityId)
            .Take(12);
        var goods = await LoadGoodsCardsAsync(query, categoriesById, cancellationToken);
        var swiperList = await LoadGoodsSwiperAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            swiperList,
            categories,
            todayGoods = goods.Take(6).ToList(),
            hotGoods = goods.Skip(6).Take(6).ToList()
        }));
    }

    [HttpGet("category")]
    public Task<IActionResult> GetCategoryGoods(
        [FromQuery] string? categoryId,
        [FromQuery] string? category,
        [FromQuery] string? id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return BuildPagedGoodsResponseAsync(categoryId ?? category ?? id ?? "all", page, pageSize, includeCategories: false, cancellationToken);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await LoadCategoriesAsync(cancellationToken);
        return Ok(ApiResult.Success(categories));
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchGoods(
        [FromQuery] string keyword = "",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);
        keyword = (keyword ?? string.Empty).Trim();

        var query = _dbContext.Commodities.AsNoTracking().Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.ProductName.Contains(keyword) || (x.SpecDescription ?? string.Empty).Contains(keyword));
        }

        query = query.OrderByDescending(x => x.CommodityId);
        var total = await query.CountAsync(cancellationToken);
        var categories = await LoadCategoriesAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(x => x.id, StringComparer.OrdinalIgnoreCase);
        var items = await LoadGoodsCardsAsync(query.Skip((page - 1) * pageSize).Take(pageSize), categoriesById, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            keyword,
            items,
            goodsList = items,
            list = items,
            total,
            page,
            pageSize,
            hasMore = page * pageSize < total
        }));
    }

    private async Task<IActionResult> BuildPagedGoodsResponseAsync(string? category, int page, int pageSize, bool includeCategories, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var categories = await LoadCategoriesAsync(cancellationToken);
        var normalizedCategory = NormalizeCategory(category, categories);

        var query = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1);

        if (normalizedCategory != "all" && int.TryParse(normalizedCategory, out var categoryId))
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        query = query.OrderByDescending(x => x.CommodityId);
        var total = await query.CountAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(x => x.id, StringComparer.OrdinalIgnoreCase);
        var items = await LoadGoodsCardsAsync(query.Skip((page - 1) * pageSize).Take(pageSize), categoriesById, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            category = normalizedCategory,
            categories = includeCategories ? categories : [],
            items,
            goodsList = items,
            list = items,
            page,
            pageSize,
            total,
            hasMore = page * pageSize < total
        }));
    }

    private async Task<List<FarmGoodsCategoryDto>> LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => (x.CategoryStatusId ?? 0) == 1)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var categoryIds = rows.Select(x => x.Id).ToList();
        var counts = categoryIds.Count == 0
            ? []
            : await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1 && categoryIds.Contains(x.CategoryId))
                .GroupBy(x => x.CategoryId)
                .Select(x => new { CategoryId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        var result = rows.Select((x, index) => new FarmGoodsCategoryDto
        {
            id = x.Id.ToString(),
            name = x.CategoryName,
            color = CategoryColors[index % CategoryColors.Length],
            icon = string.IsNullOrWhiteSpace(x.CategoryName) ? string.Empty : x.CategoryName[..1],
            count = counts.GetValueOrDefault(x.Id)
        }).ToList();

        return result;
    }

    private async Task<List<object>> LoadGoodsCardsAsync(
        IQueryable<Commodity> query,
        IReadOnlyDictionary<string, FarmGoodsCategoryDto> categoriesById,
        CancellationToken cancellationToken)
    {
        var commodities = await query.ToListAsync(cancellationToken);
        var ids = commodities.Select(x => x.CommodityId).ToList();
        var stats = await _inventoryStatsService.GetCommodityStatsAsync(ids, cancellationToken);
        var tags = await LoadCommodityTagsAsync(ids, cancellationToken);

        // 加载 unit 表
        var unitMap = await _dbContext.Units
            .AsNoTracking()
            .Where(u => u.IsEnabled == 1)
            .ToDictionaryAsync(u => u.UnitId, u => u.UnitName, cancellationToken);

        return commodities.Select(x =>
        {
            var price = x.UnitPrice ?? 0m;
            var stat = stats.GetValueOrDefault(x.CommodityId);
            var categoryId = x.CategoryId.ToString();
            categoriesById.TryGetValue(categoryId, out var category);
            var stock = stat?.Stock ?? (x.InStock ?? 0);
            var unitName = x.UnitId.HasValue ? unitMap.GetValueOrDefault(x.UnitId.Value) : null;
            var spec = GoodsController.BuildSpec(x.WeightText, unitName);
            var description = GoodsController.ExtractDescription(x.SpecDescription, spec);

            return (object)new Dictionary<string, object?>
            {
                ["id"] = x.CommodityId.ToString(),
                ["type"] = x.CategoryId == 5 ? "acre" : "normal",
                ["name"] = x.ProductName,
                ["price"] = price,
                ["originalPrice"] = x.OriginalPrice ?? price,
                ["image"] = NormalizeMediaUrl(x.ImageUrl),
                ["stock"] = stock,
                ["tags"] = (object)(tags.GetValueOrDefault(x.CommodityId) ?? new List<string>()),
                ["status"] = stock > 0 ? "available" : "soldOut",
                ["categoryId"] = categoryId,
                ["category"] = category?.name ?? categoryId,
                ["spec"] = spec,
                ["description"] = description,
                ["unit"] = unitName ?? string.Empty,
                ["sold"] = stat?.Sold ?? Math.Max(0, x.Quantity ?? 0)
            };
        }).ToList();
    }

    private static string NormalizeCategory(string? category, IReadOnlyCollection<FarmGoodsCategoryDto> categories)
    {
        var value = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim();
        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return "all";
        }

        foreach (var item in categories)
        {
            string id = item.id;
            string name = item.name;
            if (id.Equals(value, StringComparison.OrdinalIgnoreCase) || name.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return value;
    }

    private async Task<Dictionary<int, List<string>>> LoadCommodityTagsAsync(IReadOnlyCollection<int> commodityIds, CancellationToken cancellationToken)
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

        return rows.GroupBy(x => x.CommodityId).ToDictionary(g => g.Key, g => g.Select(x => x.TagName).Distinct().ToList());
    }

    private async Task<List<object>> LoadGoodsSwiperAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Carousels
            .AsNoTracking()
            .Where(x => x.Position == "goods")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .ToListAsync(cancellationToken);

        return rows.Select(x => (object)new { id = x.CarouselId, image = NormalizeMediaUrl(x.ImageUrl) }).ToList();
    }

    private static string NormalizeMediaUrl(string? raw) => MediaUrlHelper.Normalize(raw);

    private sealed class FarmGoodsCategoryDto
    {
        public string id { get; init; } = string.Empty;

        public string name { get; init; } = string.Empty;

        public string icon { get; init; } = string.Empty;

        public string color { get; init; } = string.Empty;

        public int count { get; init; }
    }
}
