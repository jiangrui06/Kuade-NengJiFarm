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
                .Where(x => x.IsDelete == 0 && x.Status == 1);

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
            .Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1);

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

        var unitMap = await _dbContext.Units.AsNoTracking().Where(u => u.IsEnabled == 1).ToDictionaryAsync(u => u.UnitId, u => u.UnitName, cancellationToken);

        var goodsList = commodities.Select(x =>
        {
            var stat = commodityStats.GetValueOrDefault(x.CommodityId);
            var unitName = x.UnitId.HasValue ? unitMap.GetValueOrDefault(x.UnitId.Value) : null;
            var spec = BuildSpec(x.WeightText, unitName);
            var description = ExtractDescription(x.SpecDescription, spec);
            var (netWeight, weightUnit) = ParseWeightText(x.WeightText);
            return (object)new
            {
                id = x.CommodityId.ToString(),
                name = x.ProductName,
                price = x.UnitPrice ?? 0m,
                originalPrice = x.OriginalPrice ?? (x.UnitPrice ?? 0m),
                image = NormalizeMediaUrl(x.ImageUrl),
                stock = stat?.Stock ?? (x.InStock ?? 0),
                sold = stat?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                type = x.CategoryId == 5 ? "acre" : "normal",
                spec,
                description,
                weight = x.WeightText ?? string.Empty,
                netWeight,
                weightUnit,
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

        var query = _dbContext.Commodities.AsNoTracking().Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1);
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

        var unitMap = await _dbContext.Units.AsNoTracking().Where(u => u.IsEnabled == 1).ToDictionaryAsync(u => u.UnitId, u => u.UnitName, cancellationToken);

        var goodsList = commodities.Select(x =>
        {
            var stat = stats.GetValueOrDefault(x.CommodityId);
            var unitName = x.UnitId.HasValue ? unitMap.GetValueOrDefault(x.UnitId.Value) : null;
            var spec = BuildSpec(x.WeightText, unitName);
            var description = ExtractDescription(x.SpecDescription, spec);
            var (netWeight, weightUnit) = ParseWeightText(x.WeightText);
            return new
            {
                id = x.CommodityId.ToString(),
                name = x.ProductName,
                price = x.UnitPrice ?? 0m,
                originalPrice = x.OriginalPrice ?? (x.UnitPrice ?? 0m),
                image = NormalizeMediaUrl(x.ImageUrl),
                stock = stat?.Stock ?? (x.InStock ?? 0),
                sold = stat?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                type = x.CategoryId == 5 ? "acre" : "normal",
                spec,
                description,
                weight = x.WeightText ?? string.Empty,
                netWeight,
                weightUnit,
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
            .FirstOrDefaultAsync(x => x.IsDelete == 0 && x.CommodityId == goodsId && (x.ProductStatus ?? 0) == 1, cancellationToken);
        if (commodity is null)
        {
            return await BuildDishDetailResponseAsync(goodsId, cancellationToken);
        }

        var materialImages = await _dbContext.CommodityImages
            .AsNoTracking()
            .Where(x => x.CommodityId == goodsId)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        // 按 material_type 分类：0=轮播图, 1=详情图, 2=主页图片
        var carouselImages = materialImages.Where(x => x.MaterialType == 0)
            .Select(x => NormalizeMediaUrl(x.Url))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var detailImgList = materialImages.Where(x => x.MaterialType == 1)
            .Select(x => NormalizeMediaUrl(x.Url))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var mainMaterialImg = materialImages.Where(x => x.MaterialType == 2)
            .Select(x => NormalizeMediaUrl(x.Url))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var mainImage = mainMaterialImg ?? NormalizeMediaUrl(commodity.ImageUrl) ?? string.Empty;

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
        var detailImage = detailImgList.FirstOrDefault() ?? mainImage;

        // 从 unit 表获取单位名称
        var unitName = commodity.UnitId.HasValue
            ? await _dbContext.Units.AsNoTracking()
                .Where(u => u.UnitId == commodity.UnitId.Value && u.IsEnabled == 1)
                .Select(u => u.UnitName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var spec = BuildSpec(commodity.WeightText, unitName);
        var description = ExtractDescription(commodity.SpecDescription, spec);
        var (netWeight, weightUnit) = ParseWeightText(commodity.WeightText);

            // 从轮播图中提取第一个视频 URL
            var firstVideoUrl = carouselImages.FirstOrDefault(u => MediaHelper.IsVideoUrl(u)) ?? string.Empty;

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
                detailImages = detailImgList.Count > 0 ? detailImgList : new List<string> { mainImage },
                detail_images = detailImgList.Count > 0 ? detailImgList : new List<string> { mainImage },
                spec,
                description,
                desc = description,
                weight = commodity.WeightText ?? string.Empty,
                netWeight,
                weightUnit,
                storage = commodity.StorageCondition ?? string.Empty,
                type = commodity.CategoryId == 5 ? "acre" : "normal",
                videoUrl = firstVideoUrl,
                sold,
                stock,
                tags,
                swiperList = carouselImages.Count > 0
                    ? carouselImages.Select((image, index) => (object)new
                    {
                        id = index + 1,
                        image,
                        type = MediaHelper.IsVideoUrl(image) ? "video" : "image",
                        thumb = MediaHelper.IsVideoUrl(image) ? MediaHelper.GetVideoThumbUrl(image) : null
                    }).ToList()
                    : new List<object>()
            }));
    }

    private async Task<IActionResult> BuildDishDetailResponseAsync(int dishId, CancellationToken cancellationToken)
    {
        var dish = await _dbContext.Dishes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsDelete == 0 && x.DishId == dishId && x.Status == 1, cancellationToken);
        if (dish is null)
        {
            return Ok(ApiResult.Fail("goods not found", 404));
        }

        var stats = (await _inventoryStatsService.GetDishStatsAsync([dishId], cancellationToken)).GetValueOrDefault(dishId);
        var tags = string.IsNullOrWhiteSpace(dish.AttributeName)
            ? Array.Empty<string>()
            : new[] { dish.AttributeName };

        // 从 dish_image 表按 material_type 分类读取图片
        var dishMaterialImages = await _dbContext.DishImages
            .AsNoTracking()
            .Where(x => x.DishId == dishId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var dishCarouselImages = dishMaterialImages.Where(x => x.MaterialType == 0)
            .Select(x => NormalizeMediaUrl(x.ImageUrl))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dishDetailImages = dishMaterialImages.Where(x => x.MaterialType == 1)
            .Select(x => NormalizeMediaUrl(x.ImageUrl))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dishMainImg = dishMaterialImages.Where(x => x.MaterialType == 2)
            .Select(x => NormalizeMediaUrl(x.ImageUrl))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var image = dishMainImg ?? NormalizeMediaUrl(dish.ImageUrl) ?? string.Empty;

        // 轮播图降级：无轮播图时用详情图
        var swiperImages = dishCarouselImages.Count > 0 ? dishCarouselImages : dishDetailImages;
        // 详情图降级：无详情图时用主图
        var detailList = dishDetailImages.Count > 0 ? dishDetailImages : (dishCarouselImages.Count > 0 ? dishCarouselImages : new List<string> { image });

        // 从轮播图中提取第一个视频 URL
        var dishFirstVideo = dishCarouselImages.FirstOrDefault(u => MediaHelper.IsVideoUrl(u)) ?? string.Empty;

        return Ok(ApiResult.Success(new
        {
            id = dish.DishId.ToString(),
            name = dish.DishName,
            price = dish.DishPrice,
            originalPrice = dish.DishPrice,
            image,
            mainImage = image,
            main_image = image,
            detailImage = dishCarouselImages.FirstOrDefault() ?? image,
            detail_image = dishCarouselImages.FirstOrDefault() ?? image,
            detailImages = dishCarouselImages,
            detail_images = dishCarouselImages,
            description = dish.DishDescription ?? string.Empty,
            desc = dish.DishDescription ?? string.Empty,
            weight = string.Empty,
            storage = string.Empty,
            videoUrl = dishFirstVideo,
            sold = stats?.Sold ?? dish.DishSold,
            stock = stats?.Stock ?? dish.DishRemainingQuantity,
            categoryId = dish.DishCategoryId.ToString(),
            category = dish.DishCategoryId.ToString(),
            tags,
            swiperList = dishCarouselImages.Select((img, index) => (object)new
            {
                id = index + 1,
                image = img,
                type = MediaHelper.IsVideoUrl(img) ? "video" : "image",
                thumb = MediaHelper.IsVideoUrl(img) ? MediaHelper.GetVideoThumbUrl(img) : null
            }).ToList<object>()
        }));
    }

    private static string NormalizeMediaUrl(string? raw) => MediaUrlHelper.Normalize(raw);

    /// <summary>
    /// 从 weight_text 提取数值部分和单位后缀
    /// "500g" → (500m, "g")， "2.5斤" → (2.5m, "斤")
    /// </summary>
    internal static (decimal? netWeight, string weightUnit) ParseWeightText(string? weightText)
    {
        if (string.IsNullOrWhiteSpace(weightText))
            return (null, string.Empty);

        var match = System.Text.RegularExpressions.Regex.Match(weightText.Trim(), @"^([\d.]+)\s*(.*)$");
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, out var num))
            return (null, weightText.Trim());

        return (num, match.Groups[2].Value.Trim());
    }

    /// <summary>
    /// 构建规格文本：weightText 已含标签（如 "11g/份"）则直接返回，否则拼上单位
    /// </summary>
    internal static string BuildSpec(string? weightText, string? unitName)
    {
        if (string.IsNullOrWhiteSpace(weightText))
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(unitName) && !weightText.Contains('/'))
            return $"{weightText}/{unitName}";
        return weightText;
    }

    /// <summary>
    /// 从 spec_description 中提取纯描述文本（去掉规格前缀）
    /// </summary>
    internal static string ExtractDescription(string? specDescription, string? spec)
    {
        var desc = specDescription ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(spec) && !string.IsNullOrWhiteSpace(desc))
        {
            var prefix = $"{spec}，";
            if (desc.StartsWith(prefix))
            {
                desc = desc[prefix.Length..];
            }
        }
        return desc;
    }
}
