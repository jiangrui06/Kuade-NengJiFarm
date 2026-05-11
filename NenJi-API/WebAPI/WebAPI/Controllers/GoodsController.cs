using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/goods")]
public class GoodsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInventoryStatsService _inventoryStatsService;

    public GoodsController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var normalizedType = (type ?? "goods").Trim().ToLowerInvariant();

        if (normalizedType == "food")
        {
            var query = _dbContext.Dishes
                .AsNoTracking()
                .Where(x => x.Status == 1);

            if (categoryId.HasValue && categoryId > 0)
            {
                query = query.Where(x => x.DishCategoryId == categoryId.Value);
            }

            var dishes = await query
                .OrderByDescending(x => x.DishSold)
                .ThenByDescending(x => x.DishId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var dishIds = dishes.Select(x => x.DishId).ToList();
            var dishStats = await _inventoryStatsService.GetDishStatsAsync(dishIds, cancellationToken);

            var list = dishes.Select(x =>
            {
                var stat = dishStats.GetValueOrDefault(x.DishId);
                return new
                {
                    id = x.DishId.ToString(),
                    name = x.DishName,
                    price = x.DishPrice,
                    originalPrice = x.DishPrice,
                    image = NormalizeMediaUrl(x.ImageUrl),
                    stock = stat?.Stock ?? x.DishRemainingQuantity,
                    sold = stat?.Sold ?? x.DishSold,
                    description = x.DishDescription ?? string.Empty,
                    categoryId = x.DishCategoryId.ToString(),
                    category = x.DishCategoryId.ToString(),
                    tags = string.IsNullOrWhiteSpace(x.AttributeName) ? [] : new[] { x.AttributeName }
                };
            }).ToList();

            return Ok(ApiResult.Success(list));
        }

        var commoditiesQuery = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => (x.ProductStatus ?? 0) == 1);

        if (categoryId.HasValue && categoryId > 0)
        {
            commoditiesQuery = commoditiesQuery.Where(x => x.CategoryId == categoryId.Value);
        }

        var commodities = await commoditiesQuery
            .OrderByDescending(x => x.CommodityId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var commodityIds = commodities.Select(x => x.CommodityId).ToList();
        var commodityStats = await _inventoryStatsService.GetCommodityStatsAsync(commodityIds, cancellationToken);

        var goodsList = commodities.Select(x =>
        {
            var stat = commodityStats.GetValueOrDefault(x.CommodityId);
            return (object)new
            {
                id = x.CommodityId.ToString(),
                name = x.ProductName,
                price = x.UnitPrice ?? 0m,
                originalPrice = x.OriginalPrice ?? (x.UnitPrice ?? 0m),
                image = NormalizeMediaUrl(x.ImageUrl),
                stock = stat?.Stock ?? (x.InStock ?? 0),
                sold = stat?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                description = x.SpecDescription ?? string.Empty,
                categoryId = x.CategoryId.ToString(),
                category = x.CategoryId.ToString(),
                tags = Array.Empty<string>()
            };
        }).ToList<object>();

        return Ok(ApiResult.Success(goodsList));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] string? type, CancellationToken cancellationToken)
    {
        var normalizedType = (type ?? "goods").Trim().ToLowerInvariant();

        if (normalizedType == "food")
        {
            var categories = await _dbContext.DishCategories
                .AsNoTracking()
                .Where(x => (x.DishCategoryStatusId ?? 1) == 1)
                .OrderBy(x => x.DishSortOrder)
                .Select(x => new
                {
                    id = x.DishCategoryId,
                    name = x.DishCategoryName
                })
                .ToListAsync(cancellationToken);
            return Ok(ApiResult.Success(categories));
        }

        var commodityCategories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => (x.CategoryStatusId ?? 1) == 1)
            .OrderBy(x => x.SortOrder ?? 0)
            .Select(x => new
            {
                id = x.Id,
                name = x.CategoryName
            })
            .ToListAsync(cancellationToken);
        return Ok(ApiResult.Success(commodityCategories));
    }

    [HttpGet("{id:int}")]
    public Task<IActionResult> DetailByRoute(
        int id,
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        return BuildDetailResponseAsync(id, type, cancellationToken);
    }

    [HttpGet("detail")]
    public Task<IActionResult> Detail(
        [FromQuery(Name = "goodsId")] int? goodsId,
        [FromQuery(Name = "goods_id")] int? goodsIdAlias,
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        return BuildDetailResponseAsync(goodsId ?? goodsIdAlias ?? 0, type, cancellationToken);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);
        keyword = (keyword ?? string.Empty).Trim();

        var query = _dbContext.Commodities.AsNoTracking().Where(x => (x.ProductStatus ?? 0) == 1);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.ProductName.Contains(keyword) || (x.SpecDescription ?? string.Empty).Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var commodities = await query
            .OrderByDescending(x => x.ProductName.Contains(keyword))
            .ThenBy(x => x.UnitPrice ?? 0m)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var ids = commodities.Select(x => x.CommodityId).ToList();
        var stats = await _inventoryStatsService.GetCommodityStatsAsync(ids, cancellationToken);

        var goodsList = commodities.Select(x =>
        {
            var stat = stats.GetValueOrDefault(x.CommodityId);
            return new
            {
                id = x.CommodityId.ToString(),
                name = x.ProductName,
                price = x.UnitPrice ?? 0m,
                originalPrice = x.OriginalPrice ?? (x.UnitPrice ?? 0m),
                image = NormalizeMediaUrl(x.ImageUrl),
                stock = stat?.Stock ?? (x.InStock ?? 0),
                sold = stat?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                description = x.SpecDescription ?? string.Empty,
                categoryId = x.CategoryId.ToString()
            };
        }).ToList();

        var categoriesData = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => (x.CategoryStatusId ?? 1) == 1)
            .OrderBy(x => x.SortOrder ?? 0)
            .Select(x => new
            {
                id = x.Id.ToString(),
                name = x.CategoryName
            })
            .ToListAsync(cancellationToken);

        var categories = new List<object> { new { id = "all", name = "全部商品" } };
        categories.AddRange(categoriesData);

        return Ok(ApiResult.Success(new { goodsList, items = goodsList, goods = goodsList, categories, total, page, pageSize }));
    }

    private async Task<IActionResult> BuildDetailResponseAsync(int goodsId, string? type = null, CancellationToken cancellationToken = default)
    {
        if (goodsId <= 0)
        {
            return Ok(ApiResult.Fail("goodsId is invalid", 400));
        }

        if (string.Equals(type, "food", StringComparison.OrdinalIgnoreCase))
        {
            return await BuildDishDetailResponseAsync(goodsId, cancellationToken);
        }

        var commodity = await _dbContext.Commodities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CommodityId == goodsId && (x.ProductStatus ?? 0) == 1, cancellationToken);
        if (commodity is null)
        {
            return await BuildDishDetailResponseAsync(goodsId, cancellationToken);
        }

        var detailRows = await _dbContext.CommodityImages
            .AsNoTracking()
            .Where(x => x.CommodityId == goodsId)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var images = detailRows
            .Select(x => NormalizeMediaUrl(x.Url))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var mainImage = NormalizeMediaUrl(commodity.ImageUrl);
        if (!string.IsNullOrWhiteSpace(mainImage) && images.All(x => !x.Equals(mainImage, StringComparison.OrdinalIgnoreCase)))
        {
            images.Insert(0, mainImage);
        }

        var tags = await (
            from relation in _dbContext.CommodityTagRelations.AsNoTracking()
            join tag in _dbContext.Tags.AsNoTracking() on relation.TagId equals tag.TagId
            where relation.CommodityId == goodsId
            select tag.TagName
        ).Distinct().ToListAsync(cancellationToken);
        var stats = (await _inventoryStatsService.GetCommodityStatsAsync([goodsId], cancellationToken)).GetValueOrDefault(goodsId);
        var price = commodity.UnitPrice ?? 0m;
        var stock = stats?.Stock ?? (commodity.InStock ?? 0);
        var sold = stats?.Sold ?? Math.Max(0, commodity.Quantity ?? 0);
        var detailImage = images.FirstOrDefault() ?? mainImage;

        return Ok(ApiResult.Success(new
        {
            id = commodity.CommodityId.ToString(),
            name = commodity.ProductName,
            price,
            originalPrice = commodity.OriginalPrice ?? price,
            image = mainImage,
            mainImage,
            main_image = mainImage,
            detailImage,
            detail_image = detailImage,
            detailImages = images,
            detail_images = images,
            description = commodity.SpecDescription ?? string.Empty,
            desc = commodity.SpecDescription ?? string.Empty,
            weight = commodity.WeightText ?? string.Empty,
            storage = commodity.StorageCondition ?? string.Empty,
            videoUrl = string.Empty,
            sold,
            stock,
            tags,
            swiperList = images.Select((image, index) => new { id = index + 1, image }).ToList()
        }));
    }

    private async Task<IActionResult> BuildDishDetailResponseAsync(int dishId, CancellationToken cancellationToken)
    {
        var dish = await _dbContext.Dishes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DishId == dishId && x.Status == 1, cancellationToken);
        if (dish is null)
        {
            return Ok(ApiResult.Fail("goods not found", 404));
        }

        var stats = (await _inventoryStatsService.GetDishStatsAsync([dishId], cancellationToken)).GetValueOrDefault(dishId);
        var image = NormalizeMediaUrl(dish.ImageUrl);
        var tags = string.IsNullOrWhiteSpace(dish.AttributeName)
            ? Array.Empty<string>()
            : new[] { dish.AttributeName };

        return Ok(ApiResult.Success(new
        {
            id = dish.DishId.ToString(),
            name = dish.DishName,
            price = dish.DishPrice,
            originalPrice = dish.DishPrice,
            image,
            mainImage = image,
            main_image = image,
            detailImage = image,
            detail_image = image,
            detailImages = string.IsNullOrWhiteSpace(image) ? Array.Empty<string>() : new[] { image },
            detail_images = string.IsNullOrWhiteSpace(image) ? Array.Empty<string>() : new[] { image },
            description = dish.DishDescription ?? string.Empty,
            desc = dish.DishDescription ?? string.Empty,
            weight = string.Empty,
            storage = string.Empty,
            videoUrl = string.Empty,
            sold = stats?.Sold ?? dish.DishSold,
            stock = stats?.Stock ?? dish.DishRemainingQuantity,
            categoryId = dish.DishCategoryId.ToString(),
            category = dish.DishCategoryId.ToString(),
            tags,
            swiperList = string.IsNullOrWhiteSpace(image) ? Array.Empty<object>() : [new { id = 1, image }]
        }));
    }

    private static string NormalizeMediaUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.StartsWith("/api/file/", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.StartsWith("api/file/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/{value}";
        }

        var name = value.TrimStart('/');
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv"
            ? $"/api/file/video/{name}"
            : $"/api/file/image/{name}";
    }
}
