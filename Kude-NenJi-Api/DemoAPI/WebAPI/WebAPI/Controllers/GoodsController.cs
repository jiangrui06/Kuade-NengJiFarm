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

    [HttpGet("{id:int}")]
    public Task<IActionResult> DetailByRoute(int id, CancellationToken cancellationToken)
    {
        return BuildDetailResponseAsync(id, cancellationToken);
    }

    /// <summary>
    /// 根据关键词搜索商品
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="page">页码 (默认1)</param>
    /// <param name="pageSize">每页条数 (默认20)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Ok(ApiResult.Fail("关键词不能为空", 400));
            }

            page = Math.Max(1, page);
            pageSize = Math.Max(1, pageSize);

            // 1. 构建基础查询：已上架商品
            var query = _dbContext.Commodities
                .AsNoTracking()
                .Where(x => (x.CommodityStatusId ?? 0) == 1);

            // 2. 关键词模糊匹配 (名称 或 描述)
            query = query.Where(x =>
                (x.ProductName != null && x.ProductName.Contains(keyword)) ||
                (x.SpecDescription != null && x.SpecDescription.Contains(keyword)));

            // 3. 排序规则：优先匹配名称，其次按价格升序
            var total = await query.CountAsync(cancellationToken);
            var commodities = await query
                .OrderByDescending(x => x.ProductName != null && x.ProductName.Contains(keyword))
                .ThenBy(x => x.UnitPrice ?? 0m)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // 4. 批量加载搜索结果的标签
            var commodityIds = commodities.Select(x => x.CommodityId).ToList();
            var tagRelations = await (
                from relation in _dbContext.CommodityTagRelations.AsNoTracking()
                join tag in _dbContext.Tags.AsNoTracking() on relation.TagId equals tag.TagId
                where commodityIds.Contains(relation.CommodityId)
                select new { relation.CommodityId, tag.TagName }
            ).ToListAsync(cancellationToken);

            var tagMap = tagRelations
                .GroupBy(x => x.CommodityId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TagName).ToList());
            var commodityStats = await _inventoryStatsService.GetCommodityStatsAsync(commodityIds, cancellationToken);

            // 5. 组装响应数据
            var goodsList = commodities.Select(x =>
            {
                var stats = commodityStats.GetValueOrDefault(x.CommodityId);
                var stock = stats?.Stock ?? (x.InStock ?? 0);
                var status = (x.CommodityStatusId ?? 0) == 1 && stock > 0 ? 1 : 0;
                var imageUrl = NormalizeImageUrl(x.ImageUrl) ?? string.Empty;
                return new
                {
                    id = x.CommodityId,
                    name = x.ProductName,
                    price = x.UnitPrice ?? 0m,
                    //originalPrice = x.OriginalPrice ?? (x.UnitPrice ?? 0m),
                    image = imageUrl,
                    mainImage = imageUrl,
                    main_image = imageUrl,
                    tags = tagMap.TryGetValue(x.CommodityId, out var tags) ? tags : new List<string>(),
                    sold = stats?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                    sales = stats?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                    stock = stock,
                    status = status,
                    description = x.SpecDescription ?? string.Empty,
                    desc = x.SpecDescription ?? string.Empty
                };
            }).ToList();

            return Ok(ApiResult.Success(new
            {
                goods = goodsList,
                total,
                page,
                pageSize
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"搜索失败：{ex.Message}", 500));
        }
    }

    [HttpGet("detail")]
    public Task<IActionResult> Detail(
        [FromQuery(Name = "goodsId")] int? goodsId,
        [FromQuery(Name = "goods_id")] int? goodsIdAlias,
        CancellationToken cancellationToken)
    {
        return BuildDetailResponseAsync(goodsId ?? goodsIdAlias ?? 0, cancellationToken);
    }

    private async Task<IActionResult> BuildDetailResponseAsync(int goodsId, CancellationToken cancellationToken)
    {
        try
        {
            if (goodsId <= 0)
            {
                return Ok(ApiResult.Fail("goodsId 参数不正确", 400));
            }

            var commodity = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.CommodityId == goodsId && (x.CommodityStatusId ?? 0) == 1,
                    cancellationToken);

            if (commodity is null)
            {
                return Ok(ApiResult.Fail("商品不存在", 404));
            }

            var category = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == commodity.CategoryId, cancellationToken);

            var tags = await (
                from relation in _dbContext.CommodityTagRelations.AsNoTracking()
                join tag in _dbContext.Tags.AsNoTracking() on relation.TagId equals tag.TagId
                where relation.CommodityId == goodsId
                select tag.TagName
            ).Distinct().ToListAsync(cancellationToken);

            var detailImageRows = await _dbContext.CommodityImages
                .AsNoTracking()
                .Where(x => x.CommodityId == goodsId)
                .OrderBy(x => x.SortOrder ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Url,
                    x.ImageType
                })
                .ToListAsync(cancellationToken);

            var normalizedDetailImages = detailImageRows
                .Select(x => NormalizeImageUrl(x.Url))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            var primaryImage = ResolvePrimaryImageUrl(commodity.ImageUrl, normalizedDetailImages);
            if (normalizedDetailImages.Count == 0 && !string.IsNullOrWhiteSpace(primaryImage))
            {
                normalizedDetailImages.Add(primaryImage);
            }

            var longDetailImages = detailImageRows
                .Where(x => x.ImageType == 2)
                .Select(x => NormalizeImageUrl(x.Url))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .Take(4)
                .ToList();

            if (longDetailImages.Count == 0)
            {
                longDetailImages = normalizedDetailImages.Take(4).ToList();
            }

            if (longDetailImages.Count == 0 && !string.IsNullOrWhiteSpace(primaryImage))
            {
                longDetailImages.Add(primaryImage);
            }

            var detailImage = ResolveDetailImageUrl(primaryImage, longDetailImages);
            var price = commodity.UnitPrice ?? 0m;
            //var originalPrice = commodity.OriginalPrice ?? price;
            var description = commodity.SpecDescription ?? string.Empty;
            var stock = commodity.InStock ?? 0;
            var status = (commodity.CommodityStatusId ?? 0) == 1 && stock > 0 ? 1 : 0;
            var unit = commodity.UnitId;
            var weight = commodity.WeightText ?? string.Empty;
            var storage = commodity.StorageCondition ?? string.Empty;
            var commodityStats = (await _inventoryStatsService.GetCommodityStatsAsync([commodity.CommodityId], cancellationToken))
                .GetValueOrDefault(commodity.CommodityId);
            stock = commodityStats?.Stock ?? stock;
            status = (commodity.CommodityStatusId ?? 0) == 1 && stock > 0 ? 1 : 0;
            var sold = commodityStats?.Sold ?? Math.Max(0, commodity.Quantity ?? 0);

            return Ok(ApiResult.Success(new
            {
                id = commodity.CommodityId,
                name = commodity.ProductName,
                price,
                //originalPrice,
                sold,
                sales = sold,
                stock,
                mainImage = primaryImage,
                main_image = primaryImage,
                desc = description,
                detailImages = longDetailImages,
                detail_images = longDetailImages,
                unit,
                status,
                image = primaryImage,
                detailImage = detailImage,
                detail_image = detailImage,
                description,
                weight,
                storage,
                tags,
                categoryId = commodity.CategoryId,
                category_id = commodity.CategoryId,
                categoryName = category?.CategoryName ?? string.Empty,
                category_name = category?.CategoryName ?? string.Empty,
                canBuy = status == 1,
                salesStatus = status == 1 ? "on_sale" : "sold_out",
                bottomImages = longDetailImages
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取商品详情失败：{ex.Message}"));
        }
    }

    private string ResolveDetailImageUrl(string primaryImage, IReadOnlyCollection<string?> detailImages)
    {
        var detailImage = detailImages
            .Select(x => NormalizeImageUrl(x))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return string.IsNullOrWhiteSpace(detailImage) ? primaryImage : detailImage;
    }

    private string ResolvePrimaryImageUrl(string? imageUrl, IReadOnlyCollection<string?> detailImages)
    {
        var normalizedMainImage = NormalizeImageUrl(imageUrl);
        if (!string.IsNullOrWhiteSpace(normalizedMainImage))
        {
            return normalizedMainImage;
        }

        return detailImages
            .Select(x => NormalizeImageUrl(x))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;
    }

    private string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var trimmed = imageUrl.Trim();

        // 如果已经是完整的 URL，直接处理可能的重复前缀并返回
        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var duplicateMarkerIndex = trimmed.IndexOf("https://", 8, StringComparison.OrdinalIgnoreCase);
            if (duplicateMarkerIndex > 0) trimmed = trimmed[..duplicateMarkerIndex];

            duplicateMarkerIndex = trimmed.IndexOf("http://", 7, StringComparison.OrdinalIgnoreCase);
            if (duplicateMarkerIndex > 0) trimmed = trimmed[..duplicateMarkerIndex];

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
}
