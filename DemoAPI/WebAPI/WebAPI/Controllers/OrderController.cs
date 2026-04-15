using System.Security.Claims;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/order")]
public class OrderController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInventoryStatsService _inventoryStatsService;

    public OrderController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
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
            page = Math.Max(1, page);
            pageSize = Math.Max(1, pageSize);

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
            var commodityStats = await _inventoryStatsService.GetCommodityStatsAsync(
                commodities.Select(x => x.CommodityId),
                cancellationToken);

            var goods = commodities
                .Where(x => categoryKeyMap.TryGetValue(x.CategoryId, out var key) && key == currentCategory)
                .Select(x => new
                {
                    id = x.CommodityId,
                    name = x.ProductName,
                    image = x.ImageUrl ?? string.Empty,
                    price = ResolveCommodityPrice(x.ProductName),
                    sold = commodityStats.GetValueOrDefault(x.CommodityId)?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                    stock = commodityStats.GetValueOrDefault(x.CommodityId)?.Stock ?? (x.InStock ?? 0)
                })
                .ToList();

            var total = goods.Count;
            var pageGoods = goods.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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
            return Ok(ApiResult.Fail($"获取点餐页数据失败：{ex.Message}"));
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
            var commodityStats = await _inventoryStatsService.GetCommodityStatsAsync(
                commodities.Select(x => x.CommodityId),
                cancellationToken);

            var groupedGoods = commodities
                .GroupBy(x => categoryKeyMap.TryGetValue(x.CategoryId, out var key) ? key : $"category-{x.CategoryId}")
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(x => new
                    {
                        id = x.CommodityId,
                        name = x.ProductName,
                        image = x.ImageUrl ?? string.Empty,
                        price = ResolveCommodityPrice(x.ProductName),
                        sold = commodityStats.GetValueOrDefault(x.CommodityId)?.Sold ?? Math.Max(0, x.Quantity ?? 0),
                        stock = commodityStats.GetValueOrDefault(x.CommodityId)?.Stock ?? (x.InStock ?? 0)
                    }).ToList());

            return Ok(ApiResult.Success(new
            {
                data = new
                {
                    data = new
                    {
                        categories = categoryItems,
                        goodsList = groupedGoods
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
        return Ok(ApiResult.Success(new { updated = request?.Updates?.Count ?? 0 }));
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
    [HttpPost("getOrderData/create")]
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
            if (!TryNormalizeCreateOrderRequest(request, out var normalizedRequest, out var validationMessage))
            {
                return Ok(ApiResult.Fail(validationMessage, 400));
            }

            var userId = GetCurrentUserId();
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            var address = await _dbContext.ShippingAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AddressId == normalizedRequest.AddressId && x.UserId == userId, cancellationToken);

            if (address is null)
            {
                return Ok(ApiResult.Fail("收货地址不存在", 404));
            }

            var sourceItems = new List<OrderSourceItem>();
            if (normalizedRequest.CartIds.Count > 0)
            {
                // 改为从数据库的 ShippingCarts 获取购物车项
                var cartItems = await _dbContext.ShippingCarts
                    .Where(x => x.UserId == userId && normalizedRequest.CartIds.Contains(x.ShippingCartId))
                    .Select(x => new { Id = x.ShippingCartId, GoodsId = x.CommodityId, Count = x.CartQuantity })
                    .ToListAsync(cancellationToken);

                if (cartItems.Count == 0)
                {
                    return Ok(ApiResult.Fail("购物车商品不存在", 1003));
                }

                sourceItems = cartItems
                    .GroupBy(x => x.GoodsId)
                    .Select(x => new OrderSourceItem
                    {
                        GoodsId = x.Key,
                        Count = x.Sum(c => c.Count)
                    })
                    .ToList();
            }
            else if (normalizedRequest.GoodsId > 0)
            {
                sourceItems.Add(new OrderSourceItem
                {
                    GoodsId = normalizedRequest.GoodsId,
                    Count = normalizedRequest.Count
                });
            }

            if (sourceItems.Count == 0)
            {
                return Ok(ApiResult.Fail("未提供可下单的商品信息", 400));
            }

            var commodityIds = sourceItems.Select(x => x.GoodsId).Distinct().ToList();
            var commodityMap = await _dbContext.Commodities
                .Where(x => commodityIds.Contains(x.CommodityId) && (x.ProductStatus ?? 0) == 1)
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            foreach (var item in sourceItems)
            {
                if (!commodityMap.TryGetValue(item.GoodsId, out var commodity))
                {
                    return Ok(ApiResult.Fail("商品不存在", 404));
                }

                if ((commodity.InStock ?? 0) < item.Count)
                {
                    return Ok(ApiResult.Fail($"商品库存不足：{commodity.ProductName}", 1002));
                }
            }

            var totalAmount = sourceItems.Sum(x => ResolveCommodityPrice(commodityMap[x.GoodsId].ProductName) * x.Count);
            var now = DateTime.Now;
            var order = new OrderEntity
            {
                OrderNumber = GenerateOrderNumber(),
                UserId = userId,
                ActualPayment = totalAmount,
                TotalOrderAmount = totalAmount,
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
                PaymentMethods = 0
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var item in sourceItems)
            {
                var commodity = commodityMap[item.GoodsId];
                var unitPrice = ResolveCommodityPrice(commodity.ProductName);

                _dbContext.OrderDetails.Add(new OrderDetail
                {
                    OrderId = order.OrderId,
                    CommodityId = commodity.CommodityId,
                    UnitPrice = unitPrice,
                    ActualUnitPrice = unitPrice,
                    PurchaseQuantity = item.Count,
                    SubtotalAmount = unitPrice * item.Count
                });

                commodity.InStock = Math.Max(0, (commodity.InStock ?? 0) - item.Count);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 从购物车中移除已下单的商品（数据库实现）
            if (normalizedRequest.CartIds.Count > 0)
            {
                var removeCarts = _dbContext.ShippingCarts
                    .Where(x => x.UserId == userId && normalizedRequest.CartIds.Contains(x.ShippingCartId));
                _dbContext.ShippingCarts.RemoveRange(removeCarts);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(ApiResult.Success(new
            {
                id = order.OrderId,
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
    [HttpGet("getOrderData/list")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            page = Math.Max(1, page);
            pageSize = Math.Max(1, pageSize);

            var query = _dbContext.Orders.AsNoTracking().Where(x => x.UserId == userId);
            query = ApplyStatusFilter(query, status);

            var total = await query.CountAsync(cancellationToken);
            var orders = await query
                .OrderByDescending(x => x.OrderCreationTime)
                .ThenByDescending(x => x.OrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // 演示环境兜底：当数据库暂无订单时，返回静态假订单，方便前端联调。
            if (total == 0)
            {
                var mockOrders = BuildMockOrders();
                var filteredMockOrders = ApplyMockStatusFilter(mockOrders, status);
                var mockTotal = filteredMockOrders.Count;
                var pagedMockOrders = filteredMockOrders
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(ApiResult.Success(new
                {
                    orders = pagedMockOrders.Select(x => new
                    {
                        id = x.Id,
                        status = x.Status,
                        statusText = x.StatusText,
                        createTime = x.CreateTime,
                        totalPrice = x.TotalPrice,
                        orderType = 1,
                        orderTypeText = "商城订单",
                        items = x.Items
                    }).ToList(),
                    total = mockTotal,
                    page,
                    pageSize,
                    hasMore = page * pageSize < mockTotal
                }));
            }

            var orderIds = orders.Select(x => x.OrderId).ToList();
            var items = await _dbContext.OrderDetails
                .AsNoTracking()
                .Where(x => orderIds.Contains(x.OrderId))
                .OrderBy(x => x.OrderDetailsId)
                .ToListAsync(cancellationToken);

            var commodityIds = items.Select(x => x.CommodityId).Distinct().ToList();
            var commodities = await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            var groupedItems = items
                .GroupBy(x => x.OrderId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(d => (object)new
                    {
                        id = d.CommodityId.ToString(),
                        name = commodities.TryGetValue(d.CommodityId, out var goods) ? goods.ProductName : $"商品{d.CommodityId}",
                        price = d.ActualUnitPrice,
                        quantity = d.PurchaseQuantity,
                        image = commodities.TryGetValue(d.CommodityId, out var commodity) ? commodity.ImageUrl ?? string.Empty : string.Empty
                    }).ToList());

            return Ok(ApiResult.Success(new
            {
                orders = orders.Select(x => new
                {
                    id = x.OrderId.ToString(),
                    status = MapDocumentStatus(x.OrderStatus, x.PaymentStatus),
                    statusText = MapDocumentStatusText(x.OrderStatus, x.PaymentStatus),
                    createTime = x.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    totalPrice = x.TotalOrderAmount,
                    orderType = x.OrderType,
                    orderTypeText = MapOrderTypeText(x.OrderType),
                    items = groupedItems.TryGetValue(x.OrderId, out var list) ? list : new List<object>()
                }).ToList(),
                total,
                page,
                pageSize,
                hasMore = page * pageSize < total
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取订单列表失败：{ex.Message}"));
        }
    }

    [HttpGet("info")]
    [HttpGet("detail")]
    [HttpGet("getOrderData/detail")]
    [HttpGet("getOrderData/{orderId:long}")]
    public async Task<IActionResult> Detail([FromQuery] long orderId, [FromRoute] long routeOrderId, CancellationToken cancellationToken)
    {
        try
        {
            orderId = orderId > 0 ? orderId : routeOrderId;
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

            var details = await _dbContext.OrderDetails
                .AsNoTracking()
                .Where(x => x.OrderId == order.OrderId)
                .OrderBy(x => x.OrderDetailsId)
                .ToListAsync(cancellationToken);

            var commodityIds = details.Select(x => x.CommodityId).Distinct().ToList();
            var commodities = await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            return Ok(ApiResult.Success(new
            {
                totalAmount = order.TotalOrderAmount,
                order = new
                {
                    id = order.OrderId.ToString(),
                    status = MapDocumentStatus(order.OrderStatus, order.PaymentStatus),
                    statusText = MapDocumentStatusText(order.OrderStatus, order.PaymentStatus),
                    createTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    payTime = order.PaymentStatus == 1 ? order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    totalPrice = order.TotalOrderAmount,
                    totalAmount = order.TotalOrderAmount,
                    orderType = order.OrderType,
                    orderTypeText = MapOrderTypeText(order.OrderType),
                    shippingAddress = new
                    {
                        name = address?.ContactName ?? order.ContactPerson,
                        phone = order.ContactNumber,
                        address = address is null ? order.ShippingAddress : BuildAddressText(address)
                    },
                    items = details.Select(x => new
                    {
                        id = x.CommodityId.ToString(),
                        name = commodities.TryGetValue(x.CommodityId, out var goods) ? goods.ProductName : $"商品{x.CommodityId}",
                        price = x.ActualUnitPrice,
                        quantity = x.PurchaseQuantity,
                        image = NormalizeImageUrl(commodities.TryGetValue(x.CommodityId, out var commodity) ? commodity.ImageUrl ?? string.Empty : string.Empty)
                    }).ToList()
                }
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取订单详情失败：{ex.Message}"));
        }
    }

    [HttpPut("cancel")]
    [HttpPost("{id:long}/cancel")]
    [HttpPost("getOrderData/cancel/{id:long}")]
    public async Task<IActionResult> Cancel(long id, [FromBody] CancelOrderRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var orderId = request?.OrderId > 0 ? request.OrderId : id;
            if (orderId <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 1004));
            }

            if (order.PaymentStatus == 1)
            {
                return Ok(ApiResult.Fail("已支付订单不能取消", 400));
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

    [HttpPost("{id:long}/pay")]
    [HttpPost("getOrderData/pay/{id:long}")]
    public async Task<IActionResult> Pay(long id, [FromBody] PayOrderRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.PayAmount <= 0 || string.IsNullOrWhiteSpace(request.PaymentMethod))
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            order.PaymentStatus = 1;
            order.OrderStatus = 1;
            order.PaymentMethods = string.Equals(request.PaymentMethod, "wallet", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            order.PaymentTime = DateTime.Now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                status = "paid",
                statusText = "已支付"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"订单支付失败：{ex.Message}"));
        }
    }

    [HttpPost("{id:long}/confirm")]
    [HttpPost("getOrderData/confirm/{id:long}")]
    public async Task<IActionResult> Confirm(long id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            order.OrderStatus = 3;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                status = "completed",
                statusText = "已完成"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"确认收货失败：{ex.Message}"));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static bool TryNormalizeCreateOrderRequest(
        CreateOrderRequest? request,
        out CreateOrderRequest normalizedRequest,
        out string message)
    {
        normalizedRequest = new CreateOrderRequest();
        message = "请求参数不正确";

        if (request is null)
        {
            message = "请求体不能为空";
            return false;
        }

        var addressId = request.AddressId > 0 ? request.AddressId : request.AddressIdAlias;
        if (addressId <= 0)
        {
            message = "addressId 缺失或不正确";
            return false;
        }

        var cartIds = request.CartIds.Count > 0 ? request.CartIds : request.CartIdsAlias;
        var goodsId = request.GoodsId > 0 ? request.GoodsId : request.GoodsIdAlias;
        var count = request.Count > 0 ? request.Count : request.Quantity;

            if (cartIds.Count == 0 && goodsId <= 0)
            {
            message = "请提供 cartIds 或 goodsId";
            return false;
        }

        if (cartIds.Count == 0 && count <= 0)
        {
            message = "count 缺失或不正确";
            return false;
        }

        normalizedRequest = new CreateOrderRequest
        {
            AddressId = addressId,
            CartIds = cartIds.Distinct().Where(x => x > 0).ToList(),
            GoodsId = goodsId,
            Count = count <= 0 ? 1 : count,
            TotalPrice = request.TotalPrice,
            Remark = request.Remark
        };

        return true;
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
        var map = new Dictionary<int, (string Key, string Name)>
        {
            [1] = ("vegetables", "新鲜蔬菜"),
            [2] = ("meat", "肉类产品"),
            [3] = ("eggs", "蛋类产品"),
            [4] = ("dairy", "乳制品"),
            [5] = ("staple", "主食")
        };

        var result = new List<OrderCategoryItem>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories)
        {
            var fallback = map.TryGetValue(category.Id, out var predefined)
    ? predefined
    : (Key: $"category-{category.Id}", Name: category.CategoryName);

            var key = used.Add(fallback.Key) ? fallback.Key : $"{fallback.Key}-{category.Id}";
            result.Add(new OrderCategoryItem
            {
                CategoryId = category.Id,
                Id = key,
                Name = string.IsNullOrWhiteSpace(category.CategoryName) ? fallback.Name : category.CategoryName
            });

        }

        foreach (var categoryId in commodities.Select(x => x.CategoryId).Distinct())
        {
            if (result.Any(x => x.CategoryId == categoryId))
            {
                continue;
            }

            result.Add(new OrderCategoryItem
            {
                CategoryId = categoryId,
                Id = $"category-{categoryId}",
                Name = $"分类{categoryId}"
            });
        }

        return result;
    }

    private static string MapDocumentStatus(int orderStatus, int paymentStatus)
    {
        if (orderStatus == 4)
        {
            return "cancelled";
        }

        if (paymentStatus == 0)
        {
            return "pending";
        }

        return orderStatus switch
        {
            1 => "paid",
            2 => "shipping",
            3 => "completed",
            _ => "pending"
        };
    }

    private static string MapDocumentStatusText(int orderStatus, int paymentStatus)
    {
        return MapDocumentStatus(orderStatus, paymentStatus) switch
        {
            "paid" => "已支付",
            "shipping" => "待收货",
            "completed" => "已完成",
            "cancelled" => "已取消",
            _ => "待支付"
        };
    }

    private static string MapOrderTypeText(int orderType)
    {
        return orderType switch
        {
            2 => "餐饮订单",
            _ => "商城订单"
        };
    }

    private static string BuildAddressText(ShippingAddress address)
    {
        return $"{address.Province}{address.City}{address.MunicipalDistrict}{address.Town}{address.HouseNumber}";
    }

    private static string GenerateOrderNumber()
    {
        return $"{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static decimal ResolveCommodityPrice(string? productName)
    {
        return productName switch
        {
            "有机生菜" => 12.8m,
            "黄金甜玉米" => 8.8m,
            "农家西红柿" => 9.9m,
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

        [JsonPropertyName("address_id")]
        public int AddressIdAlias { get; set; }

        [JsonPropertyName("cart_ids")]
        public List<int> CartIdsAlias { get; set; } = [];

        public int GoodsId { get; set; }

        [JsonPropertyName("goods_id")]
        public int GoodsIdAlias { get; set; }

        public int Count { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    public sealed class CancelOrderRequest
    {
        public long OrderId { get; set; }
    }

    public sealed class PayOrderRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal PayAmount { get; set; }
    }

    private string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var trimmed = imageUrl.Trim();

        // 如果是本地文件名（不包含 http），拼接 API 接口地址
        if (!trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/api/file/image/{trimmed}";
        }

        return trimmed;
    }

    private sealed class OrderCategoryItem
    {
        public int CategoryId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class OrderSourceItem
    {
        public int GoodsId { get; set; }
        public int Count { get; set; }
    }

    private static List<MockOrderItem> BuildMockOrders()
    {
        return
        [
            new MockOrderItem
            {
                Id = "MOCK-20260407-1001",
                Status = "pending",
                StatusText = "待支付",
                CreateTime = DateTime.Now.AddHours(-2).ToString("yyyy-MM-dd HH:mm:ss"),
                TotalPrice = 58.80m,
                Items =
                [
                    new { id = "101", name = "有机生菜", price = 12.8m, quantity = 2, image = "" },
                    new { id = "102", name = "甜脆玉米", price = 8.8m, quantity = 3, image = "" }
                ]
            },
            new MockOrderItem
            {
                Id = "MOCK-20260407-1002",
                Status = "pending",
                StatusText = "待支付",
                CreateTime = DateTime.Now.AddHours(-6).ToString("yyyy-MM-dd HH:mm:ss"),
                TotalPrice = 39.60m,
                Items =
                [
                    new { id = "103", name = "农家西红柿", price = 9.9m, quantity = 4, image = "" }
                ]
            },
            new MockOrderItem
            {
                Id = "MOCK-20260406-2001",
                Status = "shipping",
                StatusText = "待收货",
                CreateTime = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"),
                TotalPrice = 76.00m,
                Items =
                [
                    new { id = "104", name = "土猪肉", price = 38.0m, quantity = 2, image = "" }
                ]
            },
            new MockOrderItem
            {
                Id = "MOCK-20260405-2002",
                Status = "shipping",
                StatusText = "待收货",
                CreateTime = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss"),
                TotalPrice = 99.80m,
                Items =
                [
                    new { id = "105", name = "农家大米", price = 49.9m, quantity = 2, image = "" }
                ]
            },
            new MockOrderItem
            {
                Id = "MOCK-20260404-3001",
                Status = "paid",
                StatusText = "已支付",
                CreateTime = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd HH:mm:ss"),
                TotalPrice = 45.70m,
                Items =
                [
                    new { id = "106", name = "鲜牛奶", price = 19.9m, quantity = 1, image = "" },
                    new { id = "107", name = "香甜橙子", price = 12.9m, quantity = 2, image = "" }
                ]
            }
        ];
    }

    private static List<MockOrderItem> ApplyMockStatusFilter(List<MockOrderItem> orders, string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return orders
                .OrderByDescending(x => x.CreateTime)
                .ThenByDescending(x => x.Id)
                .ToList();
        }

        var normalized = status.Trim().ToLowerInvariant();
        return orders
            .Where(x => normalized switch
            {
                "pending_payment" or "pending" => x.Status == "pending",
                "paid" => x.Status == "paid",
                "shipped" or "shipping" => x.Status == "shipping",
                "completed" => x.Status == "completed",
                "cancelled" => x.Status == "cancelled",
                _ => true
            })
            .OrderByDescending(x => x.CreateTime)
            .ThenByDescending(x => x.Id)
            .ToList();
    }

    private sealed class MockOrderItem
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string CreateTime { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public List<object> Items { get; set; } = [];
    }
}
