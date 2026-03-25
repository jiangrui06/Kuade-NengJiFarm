using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/farm-goods")]
public class FarmGoodsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    private static readonly string[] CategoryColors =
    [
        "#4E8B3A",
        "#FF8A3D",
        "#2F7D8C",
        "#C66B3D",
        "#D94F70"
    ];

    public FarmGoodsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public Task<IActionResult> GetGoodsPage(
        [FromQuery] string category = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        return BuildPagedGoodsResponseAsync(category, page, pageSize, cancellationToken);
    }

    [HttpGet("index")]
    public async Task<IActionResult> GetFarmGoodsIndex(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await LoadDemoCategoriesAsync(cancellationToken);
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
    public async Task<IActionResult> GetCategoryGoods(
        [FromQuery] string categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!int.TryParse(categoryId, out var categoryIdValue))
            {
                return Ok(ApiResult.Fail("categoryId 参数不正确", 400));
            }

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var query = _dbContext.Commodities
                .AsNoTracking()
                .Where(x => (x.ProductStatus ?? 0) == 1 && x.CategoryId == categoryIdValue)
                .OrderByDescending(x => x.CommodityId);

            var total = await query.CountAsync(cancellationToken);
            var goodsList = await LoadGoodsCardsAsync(
                query.Skip((page - 1) * pageSize).Take(pageSize),
                cancellationToken);

            return Ok(ApiResult.Success(new GoodsPageResponse
            {
                GoodsList = goodsList,
                Total = total,
                Page = page,
                PageSize = pageSize
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取分类商品失败：{ex.Message}"));
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

            return Ok(ApiResult.Success(new GoodsPageResponse
            {
                GoodsList = goodsList,
                Total = total,
                Page = page,
                PageSize = pageSize
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"搜索商品失败：{ex.Message}"));
        }
    }

    private async Task<IActionResult> BuildPagedGoodsResponseAsync(
        string category,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 6 : pageSize;

            var categories = await LoadDemoCategoriesAsync(cancellationToken);
            var normalizedCategory = NormalizeCategory(category, categories);
            var query = BuildCategoryQuery(normalizedCategory);
            var total = await query.CountAsync(cancellationToken);
            var items = await LoadGoodsCardsAsync(
                query.Skip((page - 1) * pageSize).Take(pageSize),
                cancellationToken);

            var swiperList = page == 1
                ? await LoadGoodsSwiperAsync(cancellationToken)
                : [];

            var responseCategories = page == 1
                ? categories.Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    color = x.Color,
                    icon = x.Icon
                }).Cast<object>().ToArray()
                : Array.Empty<object>();

            return Ok(ApiResult.Success(new
            {
                swiperList = swiperList.Select((item, index) => new
                {
                    id = item.Id,
                    image = item.Image
                }),
                categories = responseCategories,
                category = normalizedCategory,
                items,
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

    private async Task<List<CategoryItem>> LoadDemoCategoriesAsync(CancellationToken cancellationToken)
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
            Icon = string.Empty,
            Color = CategoryColors[(index + 1) % CategoryColors.Length]
        }));

        return items;
    }

    private string NormalizeCategory(string? category, IReadOnlyCollection<CategoryItem> categories)
    {
        var value = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim();
        return categories.Any(x => x.Id == value) ? value : "all";
    }

    private IQueryable<Commodity> BuildCategoryQuery(string category)
    {
        var query = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => (x.ProductStatus ?? 0) == 1);

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
        var tags = await LoadCommodityTagsAsync(
            commodities.Select(x => x.CommodityId).Distinct().ToList(),
            cancellationToken);

        return commodities.Select(x => new GoodsCardItem
        {
            Id = x.CommodityId,
            Name = x.ProductName,
            Image = x.ImageUrl ?? string.Empty,
            Price = ResolveCommodityPrice(x.ProductName),
            OriginalPrice = ResolveCommodityPrice(x.ProductName) + 3m,
            Stock = x.InStock ?? 0,
            Tags = tags.TryGetValue(x.CommodityId, out var itemTags) ? itemTags : []
        }).ToList();
    }

    private async Task<List<SwiperItem>> LoadGoodsSwiperAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Carousels
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.Position == "goods")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => new SwiperItem
            {
                Id = (int)x.CarouselId,
                Image = x.ImageUrl
            })
            .ToListAsync(cancellationToken);
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

    private sealed class GoodsPageResponse
    {
        public List<GoodsCardItem> GoodsList { get; set; } = [];
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
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
        public int Stock { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}
