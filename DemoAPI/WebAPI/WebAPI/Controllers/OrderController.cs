using System.Collections.Concurrent;
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/order")]
public class OrderController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private static readonly ConcurrentDictionary<long, List<OrderGoodsSnapshot>> OrderGoodsSnapshots = new();

    public OrderController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("getOrderData")]
    public async Task<IActionResult> GetOrderData(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _dbContext.Categories
                .AsNoTracking()
                .Where(x => (x.CategoryStatus ?? 0) == 1)
                .OrderBy(x => x.SortOrder ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var commodities = await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => (x.ProductStatus ?? 0) == 1)
                .OrderBy(x => x.CategoryId)
                .ThenBy(x => x.CommodityId)
                .ToListAsync(cancellationToken);

            var categoryItems = BuildOrderCategories(categories, commodities);
            var categoryKeyMap = categoryItems.ToDictionary(x => x.CategoryId, x => x.Id);
            var soldMap = commodities.ToDictionary(x => x.CommodityId, x => Math.Max(0, x.Quantity ?? 0));

            var groupedGoods = commodities
                .GroupBy(x => categoryKeyMap.TryGetValue(x.CategoryId, out var categoryKey) ? categoryKey : $"category-{x.CategoryId}")
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(x => new OrderGoodsItem
                    {
                        Id = x.CommodityId,
                        Name = x.ProductName,
                        Image = x.ImageUrl ?? string.Empty,
                        Price = ResolveCommodityPrice(x.ProductName),
                        Sold = soldMap[x.CommodityId],
                        Stock = x.InStock ?? 0
                    }).ToList());

            return Ok(ApiResult.Success(new
            {
                data = new
                {
                    data = new OrderDataResponse
                    {
                        Categories = categoryItems,
                        GoodsList = groupedGoods
                    }
                }
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取点餐数据失败：{ex.Message}"));
        }
    }

    private static List<OrderCategoryItem> BuildOrderCategories(
        IReadOnlyCollection<Category> categories,
        IReadOnlyCollection<Commodity> commodities)
    {
        var fallbackCategoryMap = new Dictionary<int, (string Key, string Name)>
        {
            [1] = ("vegetables", "新鲜蔬菜"),
            [2] = ("meat", "肉类产品"),
            [3] = ("eggs", "蛋类产品"),
            [4] = ("dairy", "乳制品"),
            [5] = ("staple", "主食")
        };

        var result = new List<OrderCategoryItem>();
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories)
        {
            var fallback = fallbackCategoryMap.TryGetValue(category.Id, out var predefined)
                ? predefined
                : (BuildCategoryKey(category.CategoryName), category.CategoryName);

            var key = fallback.Item1;
            if (!usedKeys.Add(key))
            {
                key = $"{key}-{category.Id}";
                usedKeys.Add(key);
            }

            result.Add(new OrderCategoryItem
            {
                CategoryId = category.Id,
                Id = key,
                Name = string.IsNullOrWhiteSpace(category.CategoryName) ? fallback.Item2 : category.CategoryName
            });
        }

        // Ensure the page can still render if category master data is incomplete.
        foreach (var commodityCategoryId in commodities.Select(x => x.CategoryId).Distinct())
        {
            if (result.Any(x => x.CategoryId == commodityCategoryId))
            {
                continue;
            }

            var fallback = fallbackCategoryMap.TryGetValue(commodityCategoryId, out var predefined)
                ? predefined
                : ($"category-{commodityCategoryId}", $"分类{commodityCategoryId}");

            var key = usedKeys.Add(fallback.Item1) ? fallback.Item1 : $"{fallback.Item1}-{commodityCategoryId}";
            usedKeys.Add(key);

            result.Add(new OrderCategoryItem
            {
                CategoryId = commodityCategoryId,
                Id = key,
                Name = fallback.Item2
            });
        }

        return result;
    }

    private static string BuildCategoryKey(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return "category";
        }

        return categoryName.Trim() switch
        {
            "新鲜蔬菜" => "vegetables",
            "肉类产品" => "meat",
            "蛋类产品" => "eggs",
            "乳制品" => "dairy",
            "主食" => "staple",
            _ => string.Concat(categoryName
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'))
                .Trim('-')
        };
    }

    [AllowAnonymous]
    [HttpPost("updateGoodsQuantity")]
    public IActionResult UpdateGoodsQuantity([FromBody] UpdateGoodsQuantityRequest? request)
    {
        return Ok(ApiResult.Success(new
        {
            updated = request?.Updates?.Count ?? 0
        }));
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.AddressId <= 0 || request.CartIds.Count == 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            var address = await _dbContext.ShippingAddresses
                .FirstOrDefaultAsync(x => x.AddressId == request.AddressId && x.UserId == userId, cancellationToken);

            if (address is null)
            {
                return Ok(ApiResult.Fail("收货地址不存在", 404));
            }

            var cartItems = CartController.GetCartItemsByIds(userId, request.CartIds);

            if (cartItems.Count == 0)
            {
                return Ok(ApiResult.Fail("购物车商品不存在", 1003));
            }

            var commodityIds = cartItems.Select(x => x.GoodsId).Distinct().ToList();
            var commodityMap = await _dbContext.Commodities
                .Where(x => commodityIds.Contains(x.CommodityId) && (x.ProductStatus ?? 0) == 1)
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            foreach (var cartItem in cartItems)
            {
                if (!commodityMap.TryGetValue(cartItem.GoodsId, out var commodity))
                {
                    return Ok(ApiResult.Fail("商品不存在", 404));
                }

                if ((commodity.InStock ?? 0) < cartItem.Count)
                {
                    return Ok(ApiResult.Fail($"商品库存不足：{commodity.ProductName}", 1002));
                }
            }

            var totalPrice = cartItems.Sum(x => ResolveCommodityPrice(commodityMap[x.GoodsId].ProductName) * x.Count);
            var now = DateTime.Now;
            var order = new OrderEntity
            {
                UserId = userId,
                OrderNumber = GenerateOrderNumber(),
                ActualPayment = totalPrice,
                TotalOrderAmount = totalPrice,
                OrderType = 1,
                OrderStatus = 0,
                PaymentStatus = 0,
                DeliveryMethods = 1,
                ShippingAddress = BuildAddressText(address),
                AddressId = address.AddressId,
                ContactPerson = address.ContactName,
                ContactNumber = user?.PhoneNumber ?? string.Empty,
                OrderCreationTime = now,
                PaymentTime = now,
                PaymentMethods = 0,
                OrderFormId = 0,
                SnapshotReceiverName = address.ContactName,
                SnapshotReceiverPhone = user?.PhoneNumber ?? string.Empty,
                SnapshotDeliveryAddress = BuildAddressText(address),
                SnapshotUserNickname = user?.WxName ?? string.Empty
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var snapshotGoods = new List<OrderGoodsSnapshot>();
            foreach (var cartItem in cartItems)
            {
                var commodity = commodityMap[cartItem.GoodsId];
                var price = ResolveCommodityPrice(commodity.ProductName);

                snapshotGoods.Add(new OrderGoodsSnapshot
                {
                    Id = commodity.CommodityId,
                    Name = commodity.ProductName,
                    Image = commodity.ImageUrl ?? string.Empty,
                    Price = price,
                    Count = cartItem.Count
                });

                commodity.InStock = Math.Max(0, (commodity.InStock ?? 0) - cartItem.Count);
            }

            OrderGoodsSnapshots[order.OrderId] = snapshotGoods;
            CartController.RemoveCartItems(userId, request.CartIds);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"创建订单失败：{ex.Message}"));
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var query = _dbContext.Orders
                .AsNoTracking()
                .Where(x => x.UserId == userId);

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.OrderStatus == MapOrderStatus(status));
            }

            var total = await query.CountAsync(cancellationToken);
            var orders = await query
                .OrderByDescending(x => x.OrderCreationTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var data = new OrderListResponse
            {
                OrderList = orders.Select(x => new OrderListItem
                {
                    Id = x.OrderId,
                    OrderNo = x.OrderNumber,
                    TotalPrice = x.TotalOrderAmount,
                    Status = MapOrderStatusText(x.OrderStatus),
                    CreateTime = x.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取订单列表失败：{ex.Message}"));
        }
    }

    [HttpGet("detail")]
    public async Task<IActionResult> Detail([FromQuery] long orderId, CancellationToken cancellationToken)
    {
        try
        {
            if (orderId <= 0)
            {
                return Ok(ApiResult.Fail("orderId 参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var order = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 1004));
            }

            var address = await _dbContext.ShippingAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AddressId == order.AddressId, cancellationToken);

            OrderGoodsSnapshots.TryGetValue(order.OrderId, out var snapshotGoods);
            snapshotGoods ??= new List<OrderGoodsSnapshot>();

            var data = new OrderDetailResponse
            {
                Id = order.OrderId,
                OrderNo = order.OrderNumber,
                TotalPrice = order.TotalOrderAmount,
                Status = MapOrderStatusText(order.OrderStatus),
                CreateTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Address = new OrderAddressResponse
                {
                    Name = address?.ContactName ?? order.ContactPerson,
                    Phone = order.ContactNumber,
                    Address = address is null ? order.ShippingAddress : BuildAddressText(address)
                },
                GoodsList = snapshotGoods
                    .Select(x => new OrderGoodsResponse
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Image = x.Image,
                        Price = x.Price,
                        Count = x.Count
                    })
                    .ToList()
            };

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取订单详情失败：{ex.Message}"));
        }
    }

    [HttpPut("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelOrderRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.OrderId <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var order = await _dbContext.Orders
                .FirstOrDefaultAsync(x => x.OrderId == request.OrderId && x.UserId == userId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 1004));
            }

            if (order.OrderStatus == 4)
            {
                return Ok(ApiResult.Success());
            }

            order.OrderStatus = 4;

            if (OrderGoodsSnapshots.TryGetValue(order.OrderId, out var snapshotGoods))
            {
                var commodityIds = snapshotGoods.Select(x => x.Id).Distinct().ToList();
                var commodityMap = await _dbContext.Commodities
                    .Where(x => commodityIds.Contains(x.CommodityId))
                    .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

                foreach (var detail in snapshotGoods)
                {
                    if (commodityMap.TryGetValue(detail.Id, out var commodity))
                    {
                        commodity.InStock = (commodity.InStock ?? 0) + detail.Count;
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"取消订单失败：{ex.Message}"));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static string GenerateOrderNumber()
    {
        return $"{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static int MapOrderStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" => 0,
            "paid" => 1,
            "shipped" => 2,
            "completed" => 3,
            "cancelled" => 4,
            _ => 0
        };
    }

    private static string MapOrderStatusText(int status)
    {
        return status switch
        {
            0 => "pending",
            1 => "paid",
            2 => "shipped",
            3 => "completed",
            4 => "cancelled",
            _ => "pending"
        };
    }

    private static string BuildAddressText(ShippingAddress address)
    {
        return $"{address.Province}{address.City}{address.MunicipalDistrict}{address.Town}{address.HouseNumber}";
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

    public sealed class UpdateGoodsQuantityRequest
    {
        public Dictionary<int, int> Updates { get; set; } = new();
    }

    public sealed class CreateOrderRequest
    {
        public int AddressId { get; set; }
        public List<int> CartIds { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public string Remark { get; set; } = string.Empty;
    }

    public sealed class CancelOrderRequest
    {
        public long OrderId { get; set; }
    }

    private sealed class OrderDataResponse
    {
        public List<OrderCategoryItem> Categories { get; set; } = new();
        public Dictionary<string, List<OrderGoodsItem>> GoodsList { get; set; } = new();
    }

    private sealed class OrderCategoryItem
    {
        public int CategoryId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class OrderGoodsItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Sold { get; set; }
        public int Stock { get; set; }
    }

    private sealed class OrderListResponse
    {
        public List<OrderListItem> OrderList { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    private sealed class OrderListItem
    {
        public long Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreateTime { get; set; } = string.Empty;
    }

    private sealed class OrderDetailResponse
    {
        public long Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreateTime { get; set; } = string.Empty;
        public OrderAddressResponse Address { get; set; } = new();
        public List<OrderGoodsResponse> GoodsList { get; set; } = new();
    }

    private sealed class OrderAddressResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    private sealed class OrderGoodsResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Count { get; set; }
    }

    private sealed class OrderGoodsSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Count { get; set; }
    }
}
