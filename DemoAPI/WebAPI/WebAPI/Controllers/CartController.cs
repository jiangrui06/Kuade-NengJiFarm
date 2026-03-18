using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private static int _nextCartId = 1000;

    public static readonly ConcurrentDictionary<int, List<RuntimeCartItem>> CartStore = new();

    public CartController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("list")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var cartItems = GetCartSnapshot(userId);
            var commodityIds = cartItems.Select(x => x.GoodsId).Distinct().ToList();
            var commodityMap = await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            var tags = await LoadCommodityTagsAsync(commodityIds, cancellationToken);

            var data = new CartListResponse
            {
                CartList = cartItems
                    .Where(x => commodityMap.ContainsKey(x.GoodsId))
                    .Select(x =>
                    {
                        var commodity = commodityMap[x.GoodsId];
                        var firstTag = tags.TryGetValue(x.GoodsId, out var itemTags)
                            ? itemTags.FirstOrDefault() ?? string.Empty
                            : string.Empty;

                        return new CartItemResponse
                        {
                            Id = x.Id,
                            GoodsId = x.GoodsId,
                            Name = commodity.ProductName,
                            Image = commodity.ImageUrl ?? string.Empty,
                            Tag = firstTag,
                            Price = ResolveCommodityPrice(commodity.ProductName),
                            Count = x.Count,
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

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] CartAddRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.GoodsId <= 0 || request.Count <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var goods = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CommodityId == request.GoodsId && (x.ProductStatus ?? 0) == 1, cancellationToken);

            if (goods is null)
            {
                return Ok(ApiResult.Fail("商品不存在", 404));
            }

            var userId = GetCurrentUserId();
            var currentItem = FindCartItem(userId, request.GoodsId);
            var targetCount = (currentItem?.Count ?? 0) + request.Count;
            if ((goods.InStock ?? 0) < targetCount)
            {
                return Ok(ApiResult.Fail("商品库存不足", 1002));
            }

            UpsertCartItem(userId, request.GoodsId, targetCount);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"添加购物车失败：{ex.Message}"));
        }
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
            var cartItem = FindCartItemById(userId, request.CartId);
            if (cartItem is null)
            {
                return Ok(ApiResult.Fail("购物车商品不存在", 1003));
            }

            var goods = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CommodityId == cartItem.GoodsId && (x.ProductStatus ?? 0) == 1, cancellationToken);

            if (goods is null)
            {
                return Ok(ApiResult.Fail("商品不存在", 404));
            }

            if ((goods.InStock ?? 0) < request.Count)
            {
                return Ok(ApiResult.Fail("商品库存不足", 1002));
            }

            UpdateCartItemCount(userId, request.CartId, request.Count);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"更新购物车失败：{ex.Message}"));
        }
    }

    [HttpDelete("delete")]
    public IActionResult Delete([FromBody] CartDeleteRequest? request)
    {
        try
        {
            if (request is null || request.CartId <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var success = RemoveCartItem(GetCurrentUserId(), request.CartId);
            return Ok(success ? ApiResult.Success() : ApiResult.Fail("购物车商品不存在", 1003));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除购物车商品失败：{ex.Message}"));
        }
    }

    [HttpDelete("clear")]
    public IActionResult Clear()
    {
        try
        {
            ClearCart(GetCurrentUserId());
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"清空购物车失败：{ex.Message}"));
        }
    }

    public static List<RuntimeCartItem> GetCartSnapshot(int userId)
    {
        var cart = CartStore.GetOrAdd(userId, _ => new List<RuntimeCartItem>());
        lock (cart)
        {
            return cart
                .Select(x => new RuntimeCartItem
                {
                    Id = x.Id,
                    GoodsId = x.GoodsId,
                    Count = x.Count,
                    CreatedAt = x.CreatedAt
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }
    }

    public static List<RuntimeCartItem> GetCartItemsByIds(int userId, IEnumerable<int> cartIds)
    {
        var cartIdSet = cartIds.ToHashSet();
        return GetCartSnapshot(userId)
            .Where(x => cartIdSet.Contains(x.Id))
            .ToList();
    }

    public static void RemoveCartItems(int userId, IEnumerable<int> cartIds)
    {
        var cart = CartStore.GetOrAdd(userId, _ => new List<RuntimeCartItem>());
        var cartIdSet = cartIds.ToHashSet();
        lock (cart)
        {
            cart.RemoveAll(x => cartIdSet.Contains(x.Id));
        }
    }

    private static RuntimeCartItem? FindCartItem(int userId, int goodsId)
    {
        return GetCartSnapshot(userId).FirstOrDefault(x => x.GoodsId == goodsId);
    }

    private static RuntimeCartItem? FindCartItemById(int userId, int cartId)
    {
        return GetCartSnapshot(userId).FirstOrDefault(x => x.Id == cartId);
    }

    private static void UpsertCartItem(int userId, int goodsId, int count)
    {
        var cart = CartStore.GetOrAdd(userId, _ => new List<RuntimeCartItem>());
        lock (cart)
        {
            var item = cart.FirstOrDefault(x => x.GoodsId == goodsId);
            if (item is null)
            {
                cart.Add(new RuntimeCartItem
                {
                    Id = Interlocked.Increment(ref _nextCartId),
                    GoodsId = goodsId,
                    Count = count,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                item.Count = count;
            }
        }
    }

    private static void UpdateCartItemCount(int userId, int cartId, int count)
    {
        var cart = CartStore.GetOrAdd(userId, _ => new List<RuntimeCartItem>());
        lock (cart)
        {
            var item = cart.First(x => x.Id == cartId);
            item.Count = count;
        }
    }

    private static bool RemoveCartItem(int userId, int cartId)
    {
        var cart = CartStore.GetOrAdd(userId, _ => new List<RuntimeCartItem>());
        lock (cart)
        {
            return cart.RemoveAll(x => x.Id == cartId) > 0;
        }
    }

    private static void ClearCart(int userId)
    {
        var cart = CartStore.GetOrAdd(userId, _ => new List<RuntimeCartItem>());
        lock (cart)
        {
            cart.Clear();
        }
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

    public sealed class CartAddRequest
    {
        public int GoodsId { get; set; }
        public int Count { get; set; }
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

    public sealed class RuntimeCartItem
    {
        public int Id { get; set; }
        public int GoodsId { get; set; }
        public int Count { get; set; }
        public DateTime CreatedAt { get; set; }
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
