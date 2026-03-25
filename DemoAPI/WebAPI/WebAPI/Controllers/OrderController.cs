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

    public OrderController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetPageData(
        [FromQuery] string categoryId = "vegetables",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        try
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 6 : pageSize;

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
            var currentCategory = categoryItems.Any(x => x.Id == categoryId)
                ? categoryId
                : categoryItems.FirstOrDefault()?.Id ?? "vegetables";
            var categoryKeyMap = categoryItems.ToDictionary(x => x.CategoryId, x => x.Id);

            var goods = commodities
                .Where(x => categoryKeyMap.TryGetValue(x.CategoryId, out var key) && key == currentCategory)
                .Select(x => new OrderGoodsItem
                {
                    Id = x.CommodityId,
                    Name = x.ProductName,
                    Image = x.ImageUrl ?? string.Empty,
                    Price = ResolveCommodityPrice(x.ProductName),
                    Sold = Math.Max(0, x.Quantity ?? 0),
                    Stock = x.InStock ?? 0
                })
                .ToList();

            var total = goods.Count;
            var pageGoods = goods
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(ApiResult.Success(new
            {
                categories = categoryItems.Select(x => new { id = x.Id, name = x.Name }),
                currentCategory,
                goodsList = pageGoods,
                page,
                pageSize,
                total,
                hasMore = page * pageSize < total
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取点餐页面数据失败：{ex.Message}"));
        }
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

    [AllowAnonymous]
    [HttpPost("updateGoodsQuantity")]
    public IActionResult UpdateGoodsQuantity([FromBody] UpdateGoodsQuantityRequest? request)
    {
        return Ok(ApiResult.Success(new
        {
            updated = request?.Updates?.Count ?? 0
        }));
    }

    [HttpGet("status-list")]
    public IActionResult GetStatusList()
    {
        return Ok(ApiResult.Success(new[]
        {
            new { value = "all", label = "全部" },
            new { value = "pending_payment", label = "待支付" },
            new { value = "paid", label = "已支付" },
            new { value = "shipped", label = "待收货" },
            new { value = "completed", label = "已完成" },
            new { value = "cancelled", label = "已取消" }
        }));
    }

    [HttpPost("create")]
    public Task<IActionResult> Create([FromBody] CreateOrderRequest? request, CancellationToken cancellationToken)
    {
        return CreatePaymentOrder(request, cancellationToken);
    }

    [HttpPost("create-payment-order")]
    public async Task<IActionResult> CreatePaymentOrder(
        [FromBody] CreateOrderRequest? request,
        CancellationToken cancellationToken)
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
                .AsNoTracking()
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

            var totalAmount = cartItems.Sum(x => ResolveCommodityPrice(commodityMap[x.GoodsId].ProductName) * x.Count);
            var now = DateTime.Now;
            var orderNumber = GenerateOrderNumber();
            var addressText = BuildAddressText(address);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var order = new OrderEntity
            {
                OrderNumber = orderNumber,
                UserId = userId,
                ActualPayment = totalAmount,
                TotalOrderAmount = totalAmount,
                OrderType = 1,
                OrderStatus = 0,
                PaymentStatus = 0,
                DeliveryMethods = 1,
                ShippingAddress = addressText,
                AddressId = address.AddressId,
                ContactPerson = address.ContactName,
                ContactNumber = user?.PhoneNumber ?? string.Empty,
                OrderCreationTime = now,
                PaymentTime = now,
                PaymentMethods = 0
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var cartItem in cartItems)
            {
                var commodity = commodityMap[cartItem.GoodsId];
                var unitPrice = ResolveCommodityPrice(commodity.ProductName);

                _dbContext.OrderDetails.Add(new OrderDetail
                {
                    OrderId = order.OrderId,
                    CommodityId = commodity.CommodityId,
                    UnitPrice = unitPrice,
                    ActualUnitPrice = unitPrice,
                    PurchaseQuantity = cartItem.Count,
                    SubtotalAmount = unitPrice * cartItem.Count
                });

                commodity.InStock = Math.Max(0, (commodity.InStock ?? 0) - cartItem.Count);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            CartController.RemoveCartItems(userId, request.CartIds);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId,
                orderNumber = order.OrderNumber,
                orderStatus = "pending_payment",
                paymentStatus = 0,
                amount = order.ActualPayment,
                createTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss")
            }, "订单创建成功"));
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

            query = ApplyStatusFilter(query, status);

            var total = await query.CountAsync(cancellationToken);
            var orders = await query
                .OrderByDescending(x => x.OrderCreationTime)
                .ThenByDescending(x => x.OrderId)
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
                    Status = MapOrderStatusText(x.OrderStatus, x.PaymentStatus),
                    PaymentStatus = x.PaymentStatus,
                    CreateTime = x.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    PaymentTime = x.PaymentStatus == 1 ? x.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null
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

            var orderDetails = await (
                from detail in _dbContext.OrderDetails.AsNoTracking()
                join commodity in _dbContext.Commodities.AsNoTracking() on detail.CommodityId equals commodity.CommodityId into commodityJoin
                from commodity in commodityJoin.DefaultIfEmpty()
                where detail.OrderId == order.OrderId
                orderby detail.OrderDetailsId
                select new OrderGoodsResponse
                {
                    Id = detail.CommodityId,
                    Name = commodity != null ? commodity.ProductName : $"商品{detail.CommodityId}",
                    Image = commodity != null ? commodity.ImageUrl ?? string.Empty : string.Empty,
                    Price = detail.ActualUnitPrice,
                    Count = detail.PurchaseQuantity
                })
                .ToListAsync(cancellationToken);

            var data = new OrderDetailResponse
            {
                Id = order.OrderId,
                OrderNo = order.OrderNumber,
                TotalPrice = order.TotalOrderAmount,
                Status = MapOrderStatusText(order.OrderStatus, order.PaymentStatus),
                PaymentStatus = order.PaymentStatus,
                CreateTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                PaymentTime = order.PaymentStatus == 1 ? order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null,
                Address = new OrderAddressResponse
                {
                    Name = address?.ContactName ?? order.ContactPerson,
                    Phone = order.ContactNumber,
                    Address = address is null ? order.ShippingAddress : BuildAddressText(address)
                },
                GoodsList = orderDetails
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

            if (order.PaymentStatus == 1)
            {
                return Ok(ApiResult.Fail("已支付订单不能取消", 400));
            }

            if (order.OrderStatus == 4)
            {
                return Ok(ApiResult.Success());
            }

            var details = await _dbContext.OrderDetails
                .Where(x => x.OrderId == order.OrderId)
                .ToListAsync(cancellationToken);

            var commodityIds = details.Select(x => x.CommodityId).Distinct().ToList();
            var commodityMap = await _dbContext.Commodities
                .Where(x => commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            foreach (var detail in details)
            {
                if (commodityMap.TryGetValue(detail.CommodityId, out var commodity))
                {
                    commodity.InStock = (commodity.InStock ?? 0) + detail.PurchaseQuantity;
                }
            }

            order.OrderStatus = 4;
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

    private static IQueryable<OrderEntity> ApplyStatusFilter(IQueryable<OrderEntity> query, string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "pending_payment" or "pending" => query.Where(x => x.PaymentStatus == 0 && x.OrderStatus != 4),
            "paid" => query.Where(x => x.PaymentStatus == 1 && x.OrderStatus == 1),
            "shipped" => query.Where(x => x.OrderStatus == 2),
            "completed" => query.Where(x => x.OrderStatus == 3),
            "cancelled" => query.Where(x => x.OrderStatus == 4),
            _ => query
        };
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

    private static string MapOrderStatusText(int orderStatus, int paymentStatus)
    {
        if (orderStatus == 4)
        {
            return "cancelled";
        }

        if (paymentStatus == 0)
        {
            return "pending_payment";
        }

        return orderStatus switch
        {
            1 => "paid",
            2 => "shipped",
            3 => "completed",
            _ => "pending_payment"
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
            "黄金甜玉米" => 8.8m,
            "农家番茄" => 9.9m,
            "红富士苹果" => 19.9m,
            "香甜橙子" => 15.9m,
            "散养土鸡蛋" => 16.8m,
            "黑猪梅花肉" => 38m,
            "鲜牛奶" => 19.9m,
            "农家大米" => 49.9m,
            _ => 19.9m
        };
    }

    public sealed class UpdateGoodsQuantityRequest
    {
        public Dictionary<int, int> Updates { get; set; } = [];
    }

    public sealed class CreateOrderRequest
    {
        public int AddressId { get; set; }
        public List<int> CartIds { get; set; } = [];
        public decimal TotalPrice { get; set; }
        public string Remark { get; set; } = string.Empty;
    }

    public sealed class CancelOrderRequest
    {
        public long OrderId { get; set; }
    }

    private sealed class OrderDataResponse
    {
        public List<OrderCategoryItem> Categories { get; set; } = [];
        public Dictionary<string, List<OrderGoodsItem>> GoodsList { get; set; } = [];
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
        public List<OrderListItem> OrderList { get; set; } = [];
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
        public int PaymentStatus { get; set; }
        public string CreateTime { get; set; } = string.Empty;
        public string? PaymentTime { get; set; }
    }

    private sealed class OrderDetailResponse
    {
        public long Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public int PaymentStatus { get; set; }
        public string CreateTime { get; set; } = string.Empty;
        public string? PaymentTime { get; set; }
        public OrderAddressResponse Address { get; set; } = new();
        public List<OrderGoodsResponse> GoodsList { get; set; } = [];
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
}
