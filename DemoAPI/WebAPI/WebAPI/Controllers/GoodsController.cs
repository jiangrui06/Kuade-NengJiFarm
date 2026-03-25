using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/goods")]
public class GoodsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public GoodsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{id:int}")]
    public Task<IActionResult> DetailByRoute(int id, CancellationToken cancellationToken)
    {
        return BuildDetailResponseAsync(id, cancellationToken);
    }

    [HttpGet("detail")]
    public Task<IActionResult> Detail([FromQuery] int goodsId, CancellationToken cancellationToken)
    {
        return BuildDetailResponseAsync(goodsId, cancellationToken);
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
                    x => x.CommodityId == goodsId && (x.ProductStatus ?? 0) == 1,
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

            var detailImages = await _dbContext.CommodityImages
                .AsNoTracking()
                .Where(x => x.CommodityId == goodsId)
                .OrderBy(x => x.SortOrder ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .Select(x => x.Url)
                .ToListAsync(cancellationToken);

            var primaryImage = ResolvePrimaryImageUrl(commodity.ImageUrl, detailImages);
            var detailImage = ResolveDetailImageUrl(primaryImage, detailImages);

            return Ok(ApiResult.Success(new GoodsDetailResponse
            {
                Id = commodity.CommodityId,
                Name = commodity.ProductName,
                Price = ResolveCommodityPrice(commodity.CategoryId),
                Image = primaryImage,
                DetailImage = detailImage,
                Description = BuildDescription(commodity, category?.CategoryName, tags),
                Weight = BuildWeightText(commodity.Quantity),
                Storage = ResolveStorageText(commodity.CategoryId),
                Stock = commodity.InStock ?? 0,
                Tags = tags,
                CategoryName = category?.CategoryName ?? string.Empty
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取商品详情失败：{ex.Message}"));
        }
    }

    private static string BuildWeightText(int? quantity)
    {
        if (quantity.HasValue && quantity.Value > 0)
        {
            return $"{quantity.Value}g";
        }

        return "500g";
    }

    private static string ResolveDetailImageUrl(string primaryImage, IReadOnlyCollection<string?> detailImages)
    {
        var detailImage = detailImages
            .Select(NormalizeImageUrl)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return string.IsNullOrWhiteSpace(detailImage) ? primaryImage : detailImage;
    }

    private static string ResolvePrimaryImageUrl(string? imageUrl, IReadOnlyCollection<string?> detailImages)
    {
        var normalizedMainImage = NormalizeImageUrl(imageUrl);
        if (!string.IsNullOrWhiteSpace(normalizedMainImage))
        {
            return normalizedMainImage;
        }

        return detailImages
            .Select(NormalizeImageUrl)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var trimmed = imageUrl.Trim();
        var duplicateMarkerIndex = trimmed.IndexOf("https://", 8, StringComparison.OrdinalIgnoreCase);
        if (duplicateMarkerIndex > 0)
        {
            trimmed = trimmed[..duplicateMarkerIndex];
        }

        duplicateMarkerIndex = trimmed.IndexOf("http://", 7, StringComparison.OrdinalIgnoreCase);
        if (duplicateMarkerIndex > 0)
        {
            trimmed = trimmed[..duplicateMarkerIndex];
        }

        return trimmed.Trim();
    }

    private static string BuildDescription(
        Commodity commodity,
        string? categoryName,
        IReadOnlyCollection<string> tags)
    {
        if (!string.IsNullOrWhiteSpace(commodity.SpecDescription))
        {
            return commodity.SpecDescription!;
        }

        if (tags.Count > 0)
        {
            return $"{commodity.ProductName}，分类：{categoryName ?? "农场优选"}，特色标签：{string.Join("、", tags)}。";
        }

        return $"{commodity.ProductName}，分类：{categoryName ?? "农场优选"}，新鲜直发。";
    }

    private static string ResolveStorageText(int categoryId)
    {
        return categoryId switch
        {
            3 or 4 => "冷冻",
            _ => "冷藏"
        };
    }

    private static decimal ResolveCommodityPrice(int categoryId)
    {
        return categoryId switch
        {
            1 => 12.8m,
            2 => 9.9m,
            3 => 38m,
            4 => 16.8m,
            5 => 49.9m,
            _ => 19.9m
        };
    }

    private sealed class GoodsDetailResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Image { get; set; } = string.Empty;
        public string DetailImage { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public string Storage { get; set; } = string.Empty;
        public int Stock { get; set; }
        public List<string> Tags { get; set; } = [];
        public string CategoryName { get; set; } = string.Empty;
    }
}
