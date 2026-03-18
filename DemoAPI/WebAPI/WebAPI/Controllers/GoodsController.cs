using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;

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
                return Ok(ApiResult.Fail("goodsId 参数不正确", 400));
            }

            var commodity = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CommodityId == goodsId && (x.ProductStatus ?? 0) == 1, cancellationToken);

            if (commodity is null)
            {
                return Ok(ApiResult.Fail("商品不存在", 404));
            }

            var tags = await (
                from relation in _dbContext.CommodityTagRelations.AsNoTracking()
                join tag in _dbContext.Tags.AsNoTracking() on relation.TagId equals tag.TagId
                where relation.CommodityId == goodsId
                select tag.TagName
            ).Distinct().ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(new GoodsDetailResponse
            {
                Id = commodity.CommodityId,
                Name = commodity.ProductName,
                Price = ResolveCommodityPrice(commodity.ProductName),
                Image = commodity.ImageUrl ?? string.Empty,
                DetailImage = commodity.ImageUrl ?? string.Empty,
                Description = commodity.SpecDescription ?? string.Empty,
                Weight = BuildWeightText(commodity.Quantity),
                Storage = "常温",
                Stock = commodity.InStock ?? 0,
                Tags = tags
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取商品详情失败：{ex.Message}"));
        }
    }

    private static string BuildWeightText(int? quantity)
    {
        if (!quantity.HasValue || quantity.Value <= 0)
        {
            return string.Empty;
        }

        return $"{quantity.Value}g";
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
    }
}
