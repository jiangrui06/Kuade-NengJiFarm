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

    [HttpGet("detail")]
    public async Task<IActionResult> Detail([FromQuery] int goodsId, CancellationToken cancellationToken)
    {
        try
        {
            if (goodsId <= 0)
            {
                return Ok(ApiResult.Fail("Invalid goodsId", 400));
            }

            var commodity = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.CommodityId == goodsId && (x.ProductStatus ?? 0) == 1,
                    cancellationToken);

            if (commodity is null)
            {
                return Ok(ApiResult.Fail("Goods not found", 404));
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

            var imageUrl = ResolveImageUrl(commodity.ImageUrl);

            return Ok(ApiResult.Success(new GoodsDetailResponse
            {
                Id = commodity.CommodityId,
                Name = commodity.ProductName,
                Price = ResolveCommodityPrice(commodity.CategoryId),
                Image = imageUrl,
                DetailImage = imageUrl,
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
            return Ok(ApiResult.Fail($"Failed to load goods detail: {ex.Message}"));
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

    private static string ResolveImageUrl(string? imageUrl)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }

        return "https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=1200&q=80";
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

        var tagText = tags.Count > 0 ? $"; tags: {string.Join(", ", tags)}" : string.Empty;
        var categoryText = string.IsNullOrWhiteSpace(categoryName) ? "farm selection" : categoryName;
        return $"{commodity.ProductName} from {categoryText}, fresh and ready to deliver{tagText}.";
    }

    private static string ResolveStorageText(int categoryId)
    {
        return categoryId switch
        {
            3 or 4 => "Frozen",
            _ => "Cold"
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
        public List<string> Tags { get; set; } = new();
        public string CategoryName { get; set; } = string.Empty;
    }
}
