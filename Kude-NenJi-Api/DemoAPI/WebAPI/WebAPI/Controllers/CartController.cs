using System.Security.Claims;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public CartController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public Task<IActionResult> ListRoot(CancellationToken cancellationToken)
    {
        return List(cancellationToken);
    }

    [HttpGet("list")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var cartItems = await _dbContext.ShippingCarts
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.JoinTime)
                .ToListAsync(cancellationToken);

            var commodityIds = cartItems.Select(x => x.CommodityId).Distinct().ToList();
            var commodityMap = await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            var tags = await LoadCommodityTagsAsync(commodityIds, cancellationToken);

            var data = new CartListResponse
            {
                CartList = cartItems
                    .Where(x => commodityMap.ContainsKey(x.CommodityId))
                    .Select(x =>
                    {
                        var commodity = commodityMap[x.CommodityId];
                        var firstTag = tags.TryGetValue(x.CommodityId, out var itemTags)
                            ? itemTags.FirstOrDefault() ?? string.Empty
                            : string.Empty;

                        return new CartItemResponse
                        {
                            Id = x.ShippingCartId,
                            GoodsId = x.CommodityId,
                            Name = commodity.ProductName,
                            Image = NormalizeImageUrl(commodity.ImageUrl) ?? string.Empty,
                            Tag = firstTag,
                            Price = ResolveCommodityPrice(commodity.ProductName),
                            Count = x.CartQuantity,
                            Checked = true
                        };
                    })
                    .ToList()
            };

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取购物车失败：{ex.Message}"));
        }
    }

    [HttpPost("items")]
    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] CartSyncRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request?.CartList is null)
            {
                return Ok(ApiResult.Fail("购物车数据不能为空", 400));
            }

            var userId = GetCurrentUserId();

            var existingCarts = await _dbContext.ShippingCarts
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);

            _dbContext.ShippingCarts.RemoveRange(existingCarts);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var groupedItems = request.CartList
                .Select(item => new
                {
                    CommodityId = item.GoodsId > 0
                        ? item.GoodsId
                        : item.GoodsIdAlias > 0
                            ? item.GoodsIdAlias
                            : item.Id,
                    Count = item.Count > 0 ? item.Count : 1
                })
                .Where(x => x.CommodityId > 0)
                .GroupBy(x => x.CommodityId)
                .Select(g => new { CommodityId = g.Key, Count = g.Sum(x => x.Count) });

            foreach (var item in groupedItems)
            {
                _dbContext.ShippingCarts.Add(new ShippingCart
                {
                    UserId = userId,
                    CommodityId = item.CommodityId,
                    CartQuantity = item.Count,
                    JoinTime = DateTime.Now
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await List(cancellationToken);
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"同步购物车失败：{ex.Message}"));
        }
    }

    [HttpPost("add")]
    [HttpPost("addToCart")]
    public async Task<IActionResult> Add([FromBody] CartAddRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (!TryNormalizeCartAddRequest(request, out var normalizedRequest, out var validationMessage))
            {
                return Ok(ApiResult.Fail(validationMessage, 400));
            }

            var goods = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.CommodityId == normalizedRequest.GoodsId && (x.ProductStatus ?? 0) == 1,
                    cancellationToken);

            if (goods is null)
            {
                return Ok(ApiResult.Fail("商品不存在", 404));
            }

            var userId = GetCurrentUserId();

            var existingCart = await _dbContext.ShippingCarts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CommodityId == normalizedRequest.GoodsId, cancellationToken);

            var targetCount = (existingCart?.CartQuantity ?? 0) + normalizedRequest.Count;
            if ((goods.InStock ?? 0) < targetCount)
            {
                return Ok(ApiResult.Fail("商品库存不足", 1002));
            }

            if (existingCart is null)
            {
                _dbContext.ShippingCarts.Add(new ShippingCart
                {
                    UserId = userId,
                    CommodityId = normalizedRequest.GoodsId,
                    CartQuantity = targetCount,
                    JoinTime = DateTime.Now
                });
            }
            else
            {
                existingCart.CartQuantity = targetCount;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                goodsId = normalizedRequest.GoodsId,
                count = targetCount
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"添加购物车失败：{ex.Message}"));
        }
    }

    [HttpPut("items/{id:int}")]
    public Task<IActionResult> UpdateItem(int id, [FromBody] CartItemUpdateBody? body, CancellationToken cancellationToken)
    {
        return Update(new CartUpdateRequest
        {
            CartId = id,
            Count = body?.Count ?? 0
        }, cancellationToken);
    }

    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] CartUpdateRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.CartId <= 0 || request.Count <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var cartItem = await _dbContext.ShippingCarts
                .FirstOrDefaultAsync(x => x.ShippingCartId == request.CartId && x.UserId == userId, cancellationToken);

            if (cartItem is null)
            {
                return Ok(ApiResult.Fail("购物车商品不存在", 1003));
            }

            var goods = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CommodityId == cartItem.CommodityId && (x.ProductStatus ?? 0) == 1, cancellationToken);

            if (goods is null)
            {
                return Ok(ApiResult.Fail("商品不存在", 404));
            }

            if ((goods.InStock ?? 0) < request.Count)
            {
                return Ok(ApiResult.Fail("商品库存不足", 1002));
            }

            cartItem.CartQuantity = request.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"更新购物车失败：{ex.Message}"));
        }
    }

    [HttpDelete("items/{id:int}")]
    public Task<IActionResult> DeleteItem(int id, CancellationToken cancellationToken)
    {
        return Delete(new CartDeleteRequest { CartId = id }, cancellationToken);
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromBody] CartDeleteRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.CartId <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var cartItem = await _dbContext.ShippingCarts
                .FirstOrDefaultAsync(x => x.ShippingCartId == request.CartId && x.UserId == userId, cancellationToken);

            if (cartItem is null)
            {
                return Ok(ApiResult.Fail("购物车商品不存在", 1003));
            }

            _dbContext.ShippingCarts.Remove(cartItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除购物车商品失败：{ex.Message}"));
        }
    }

    [HttpDelete]
    public Task<IActionResult> ClearRoot(CancellationToken cancellationToken)
    {
        return Clear(cancellationToken);
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var cartItems = await _dbContext.ShippingCarts
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);

            _dbContext.ShippingCarts.RemoveRange(cartItems);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"清空购物车失败：{ex.Message}"));
        }
    }

    public static List<ShippingCart> GetCartItemsByIds(int userId, IEnumerable<int> cartIds)
    {
        throw new NotImplementedException("Use database query instead.");
    }

    public static void RemoveCartItems(int userId, IEnumerable<int> cartIds)
    {
        throw new NotImplementedException("Use database query instead.");
    }

    private async Task<Dictionary<int, List<string>>> LoadCommodityTagsAsync(
        IReadOnlyCollection<int> commodityIds,
        CancellationToken cancellationToken)
    {
        if (commodityIds.Count == 0)
        {
            return new Dictionary<int, List<string>>();
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

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static bool TryNormalizeCartAddRequest(
        CartAddRequest? request,
        out CartAddRequest normalizedRequest,
        out string validationMessage)
    {
        normalizedRequest = new CartAddRequest();
        validationMessage = "请求参数不正确";

        if (request is null)
        {
            validationMessage = "请求体不能为空";
            return false;
        }

        var goodsId = request.GoodsId > 0
            ? request.GoodsId
            : request.GoodsIdAlias > 0
                ? request.GoodsIdAlias
                : request.CommodityIdAlias;

        if (goodsId <= 0)
        {
            validationMessage = "goodsId 缺失或不正确";
            return false;
        }

        var count = request.Count > 0
            ? request.Count
            : request.Quantity > 0
                ? request.Quantity
                : request.Num;

        if (count <= 0)
        {
            validationMessage = "count 缺失或不正确";
            return false;
        }

        normalizedRequest = new CartAddRequest
        {
            GoodsId = goodsId,
            Count = count
        };

        return true;
    }

    private static decimal ResolveCommodityPrice(string? productName)
    {
        return productName switch
        {
            "有机生菜" => 12.8m,
            "散养土鸡蛋" => 16.8m,
            "黑猪梅花肉" => 38.0m,
            "黄金甜玉米" => 8.8m,
            "农家番茄" => 9.9m,
            "农家花生油" => 9.9m,
            "农家橘子" => 9.9m,
            "甜脆玉米" => 8.8m,
            "农家西红柿" => 9.9m,
            "红富士苹果" => 19.9m,
            "香甜橙子" => 15.9m,
            "土猪肉" => 38m,
            "鲜牛奶" => 19.9m,
            "农家大米" => 49.9m,
            _ => 19.9m
        };
    }

    private string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var trimmed = imageUrl.Trim();

        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var duplicateMarkerIndex = trimmed.IndexOf("https://", 8, StringComparison.OrdinalIgnoreCase);
            if (duplicateMarkerIndex > 0) trimmed = trimmed[..duplicateMarkerIndex];

            duplicateMarkerIndex = trimmed.IndexOf("http://", 7, StringComparison.OrdinalIgnoreCase);
            if (duplicateMarkerIndex > 0) trimmed = trimmed[..duplicateMarkerIndex];

            return trimmed.Trim();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var ext = Path.GetExtension(trimmed).ToLowerInvariant();

        if (ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".mkv" || ext == ".wmv")
        {
            return $"{baseUrl}/api/file/video/{trimmed}";
        }

        return $"{baseUrl}/api/file/image/{trimmed}";
    }

    public sealed class CartSyncRequest
    {
        [JsonPropertyName("cartList")]
        public List<CartSyncItem>? CartList { get; set; }
    }

    public sealed class CartSyncItem
    {
        public int Id { get; set; }

        [JsonPropertyName("goodsId")]
        public int GoodsId { get; set; }

        [JsonPropertyName("goods_id")]
        public int GoodsIdAlias { get; set; }

        public int Count { get; set; }
    }

    public sealed class CartAddRequest
    {
        public int GoodsId { get; set; }
        public int Count { get; set; }

        [JsonPropertyName("goods_id")]
        public int GoodsIdAlias { get; set; }

        [JsonPropertyName("commodityId")]
        public int CommodityIdAlias { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("num")]
        public int Num { get; set; }
    }

    public sealed class CartUpdateRequest
    {
        public int CartId { get; set; }
        public int Count { get; set; }
    }

    public sealed class CartDeleteRequest
    {
        public int CartId { get; set; }
    }

    public sealed class CartItemUpdateBody
    {
        public int Count { get; set; }
    }

    private sealed class CartListResponse
    {
        public List<CartItemResponse> CartList { get; set; } = new();
    }

    private sealed class CartItemResponse
    {
        public int Id { get; set; }
        public int GoodsId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Count { get; set; }
        public bool Checked { get; set; }
    }
}
