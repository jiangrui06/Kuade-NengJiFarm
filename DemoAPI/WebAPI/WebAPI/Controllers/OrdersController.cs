using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";
    private readonly AppDbContext _dbContext;

    public OrdersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "createTime",
        [FromQuery] string? sortOrder = "desc",
        CancellationToken cancellationToken = default)
    {
        try
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var userId = ResolveCurrentUserId();
            var query = _dbContext.Orders.AsNoTracking().Where(x => x.UserId == userId);
            query = ApplyTypeFilter(query, type);
            query = ApplyStatusFilter(query, status);

            var byPrice = string.Equals(sortBy, "totalPrice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sortBy, "price", StringComparison.OrdinalIgnoreCase);
            var asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

            query = byPrice
                ? asc ? query.OrderBy(x => x.TotalOrderAmount).ThenBy(x => x.OrderId) : query.OrderByDescending(x => x.TotalOrderAmount).ThenByDescending(x => x.OrderId)
                : asc ? query.OrderBy(x => x.OrderCreationTime).ThenBy(x => x.OrderId) : query.OrderByDescending(x => x.OrderCreationTime).ThenByDescending(x => x.OrderId);

            var total = await query.CountAsync(cancellationToken);
            var orderEntities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var orderIds = orderEntities.Select(x => x.OrderId).ToList();
            var bundle = await LoadOrderDataBundleAsync(orderIds, cancellationToken);

            var orders = orderEntities.Select(order => BuildOrderSummary(order, bundle)).ToList();

            return Ok(ApiResult.Success(new
            {
                orders,
                total,
                page,
                pageSize
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to load orders: {ex.Message}", 500));
        }
    }

    [HttpGet("counts")]
    public async Task<IActionResult> Counts([FromQuery] string? type, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ResolveCurrentUserId();
            var query = _dbContext.Orders.AsNoTracking().Where(x => x.UserId == userId);
            query = ApplyTypeFilter(query, type);

            var pending = await query.CountAsync(x => x.PaymentStatus == 0 && x.OrderStatus != 4, cancellationToken);
            var paid = await query.CountAsync(x => x.OrderType == 1 && x.PaymentStatus == 1 && x.OrderStatus == 1, cancellationToken);
            var shipping = await query.CountAsync(x => x.OrderType == 1 && x.OrderStatus == 2, cancellationToken);
            var completed = await query.CountAsync(x => x.OrderStatus == 3, cancellationToken);
            var cancelled = await query.CountAsync(x => x.OrderStatus == 4, cancellationToken);

            return Ok(ApiResult.Success(new
            {
                pending,
                paid,
                shipping,
                completed,
                cancelled
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to load order counts: {ex.Message}", 500));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ResolveCurrentUserId();
            var order = await FindOrderForCurrentUserAsync(id, userId, false, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            var bundle = await LoadOrderDataBundleAsync(new List<long> { order.OrderId }, cancellationToken);
            var items = BuildOrderItems(order, bundle);
            var orderType = MapType(order.OrderType);
            var statusValue = MapStatus(order.OrderStatus, order.PaymentStatus);
            var paymentMethod = MapPaymentMethod(order.PaymentMethods);

            var address = await LoadShippingAddressAsync(userId, order.AddressId, cancellationToken);

            var shippingAddressText = address is null
                ? (string.IsNullOrWhiteSpace(order.ShippingAddress) ? "-" : order.ShippingAddress)
                : $"{address.Province}{address.City}{address.MunicipalDistrict}{address.Town}{address.HouseNumber}";

            var addressPayload = new
            {
                name = address?.ContactName ?? (string.IsNullOrWhiteSpace(order.ContactPerson) ? "-" : order.ContactPerson),
                phone = address?.ContactPhone ?? (string.IsNullOrWhiteSpace(order.ContactNumber) ? "-" : order.ContactNumber),
                province = address?.Province ?? string.Empty,
                city = address?.City ?? string.Empty,
                district = address?.MunicipalDistrict ?? string.Empty,
                detail = address is null ? shippingAddressText : $"{address.Town}{address.HouseNumber}"
            };

            var tradeNo = order.PaymentStatus == 1 ? BuildTradeNo(order) : string.Empty;

            return Ok(ApiResult.Success(new
            {
                id = order.OrderId.ToString(),
                orderNumber = order.OrderNumber,
                type = orderType,
                typeText = MapTypeText(orderType),
                status = statusValue,
                statusText = MapStatusText(statusValue, order.OrderType),
                createTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                paymentTime = order.PaymentStatus == 1 ? order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null,
                totalPrice = order.TotalOrderAmount,
                items,
                address = addressPayload,
                payment = new
                {
                    method = paymentMethod,
                    status = order.PaymentStatus == 1 ? "success" : "pending",
                    amount = order.TotalOrderAmount
                },
                shippingAddress = new
                {
                    name = addressPayload.name,
                    phone = addressPayload.phone,
                    address = shippingAddressText
                },
                payTime = order.PaymentStatus == 1 ? order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null,
                shippingTime = order.OrderStatus >= 2 ? order.PaymentTime.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss") : null,
                completeTime = order.OrderStatus == 3 ? order.PaymentTime.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss") : null,
                paymentMethod,
                transactionId = string.IsNullOrWhiteSpace(tradeNo) ? null : tradeNo
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to load order detail: {ex.Message}", 500));
        }
    }

    [HttpPost("{id}/pay")]
    [HttpPost("{id}/mock-pay")]
    public async Task<IActionResult> Pay(string id, [FromBody] PayOrderRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.PaymentMethod))
            {
                return Ok(ApiResult.Fail("Invalid request parameters", 400));
            }

            var userId = ResolveCurrentUserId();
            var order = await FindOrderForCurrentUserAsync(id, userId, true, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            if (order.OrderStatus == 4)
            {
                return Ok(ApiResult.Fail("Cancelled order cannot be paid", 409));
            }

            if (order.PaymentStatus == 1)
            {
                return Ok(ApiResult.Fail("Order already paid", 409));
            }

            if (request.Amount > 0 && Math.Abs(request.Amount - order.TotalOrderAmount) > 0.01m)
            {
                return Ok(ApiResult.Fail("Payment amount mismatch", 400));
            }

            order.PaymentStatus = 1;
            order.OrderStatus = order.OrderStatus == 0 ? 1 : order.OrderStatus;
            order.PaymentMethods = string.Equals(request.PaymentMethod, "wechat", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            order.PaymentTime = DateTime.Now;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var tradeNo = BuildTradeNo(order);
            var prepayId = $"wx{DateTime.Now:yyyyMMddHHmmssfff}";
            var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                paymentStatus = "success",
                paymentTime = order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss"),
                tradeNo,
                paymentMode = "mock",
                paymentInfo = new
                {
                    prepayId,
                    timeStamp,
                    nonceStr = Guid.NewGuid().ToString("N")[..16],
                    package = $"prepay_id={prepayId}",
                    signType = "MD5",
                    paySign = Guid.NewGuid().ToString("N")
                }
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Payment failed: {ex.Message}", 5001));
        }
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateOrderStatusRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetStatus = NormalizeStatus(request?.Status);
            if (string.IsNullOrWhiteSpace(targetStatus) || targetStatus == "pending")
            {
                return Ok(ApiResult.Fail("Invalid target status", 400));
            }

            var userId = ResolveCurrentUserId();
            var order = await FindOrderForCurrentUserAsync(id, userId, true, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            if (!CanTransitionStatus(order, targetStatus, out var transitionMessage))
            {
                return Ok(ApiResult.Fail(transitionMessage, 5002));
            }

            switch (targetStatus)
            {
                case "paid":
                    order.PaymentStatus = 1;
                    order.OrderStatus = 1;
                    if (order.PaymentTime <= DateTime.MinValue.AddDays(1))
                    {
                        order.PaymentTime = DateTime.Now;
                    }

                    break;
                case "shipping":
                    order.OrderStatus = 2;
                    break;
                case "completed":
                    order.OrderStatus = 3;
                    break;
                case "cancelled":
                    order.OrderStatus = 4;
                    break;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                status = targetStatus,
                statusText = MapStatusText(targetStatus, order.OrderType),
                updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Update status failed: {ex.Message}", 5002));
        }
    }

    [HttpGet("{id}/qrcode")]
    public async Task<IActionResult> ActivityQrCode(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ResolveCurrentUserId();
            var order = await FindOrderForCurrentUserAsync(id, userId, false, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            if (!string.Equals(MapType(order.OrderType), "activity", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResult.Fail("Only activity orders support QR code", 400));
            }

            var verifyCode = $"ACT-{order.OrderNumber}-{order.UserId}";
            var qrData = $"orderNo={order.OrderNumber}&userId={order.UserId}&verifyCode={verifyCode}";
            var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=320x320&data={Uri.EscapeDataString(qrData)}";

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                qrCodeUrl,
                verifyCode
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to load activity QR code: {ex.Message}", 500));
        }
    }

    private async Task<OrderEntity?> FindOrderForCurrentUserAsync(string id, int userId, bool tracking, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var query = tracking
            ? _dbContext.Orders.Where(x => x.UserId == userId)
            : _dbContext.Orders.AsNoTracking().Where(x => x.UserId == userId);

        if (long.TryParse(id.Trim(), out var orderId) && orderId > 0)
        {
            return await query.FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        }

        var orderNumber = id.Trim();
        return await query.FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);
    }

    private async Task<OrderDataBundle> LoadOrderDataBundleAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return new OrderDataBundle();
        }

        var orderDetails = await _dbContext.OrderDetails
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId))
            .OrderBy(x => x.OrderDetailsId)
            .ToListAsync(cancellationToken);

        var commodityIds = orderDetails.Select(x => x.CommodityId).Distinct().ToList();
        var commodityMap = commodityIds.Count == 0
            ? new Dictionary<int, Commodity>()
            : await _dbContext.Commodities
                .AsNoTracking()
                .Where(x => commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

        var orderFoods = await _dbContext.OrderFoods
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId))
            .OrderBy(x => x.OrderFoodId)
            .ToListAsync(cancellationToken);

        var orderFoodIds = orderFoods.Select(x => x.OrderFoodId).Distinct().ToList();
        var mealDetails = orderFoodIds.Count == 0
            ? new List<MealsOrderDetail>()
            : await _dbContext.MealsOrderDetails
                .AsNoTracking()
                .Where(x => orderFoodIds.Contains(x.OrderFoodId))
                .OrderBy(x => x.MealsOrderDetailsId)
                .ToListAsync(cancellationToken);

        var dishIds = mealDetails.Where(x => x.DishId > 0).Select(x => x.DishId).Distinct().ToList();
        var dishMap = dishIds.Count == 0
            ? new Dictionary<int, Dish>()
            : await _dbContext.Dishes
                .AsNoTracking()
                .Where(x => dishIds.Contains(x.DishId))
                .ToDictionaryAsync(x => x.DishId, cancellationToken);

        return new OrderDataBundle
        {
            OrderDetails = orderDetails,
            CommodityMap = commodityMap,
            OrderFoods = orderFoods,
            MealDetails = mealDetails,
            DishMap = dishMap
        };
    }

    private async Task<ShippingAddress?> LoadShippingAddressAsync(int userId, int addressId, CancellationToken cancellationToken)
    {
        var query = _dbContext.ShippingAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (addressId > 0)
        {
            var matchedAddress = await query
                .FirstOrDefaultAsync(x => x.AddressId == addressId, cancellationToken);

            if (matchedAddress is not null)
            {
                return matchedAddress;
            }
        }

        return await query
            .OrderByDescending(x => EF.Property<bool>(x, DefaultFlagProperty))
            .ThenByDescending(x => x.AddressId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private object BuildOrderSummary(OrderEntity order, OrderDataBundle bundle)
    {
        var type = MapType(order.OrderType);
        var status = MapStatus(order.OrderStatus, order.PaymentStatus);
        var items = BuildOrderItems(order, bundle)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                price = x.Price,
                quantity = x.Quantity,
                image = x.Image
            })
            .ToList();

        return new
        {
            id = order.OrderId.ToString(),
            orderNumber = order.OrderNumber,
            type,
            typeText = MapTypeText(type),
            status,
            statusText = MapStatusText(status, order.OrderType),
            createTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
            totalPrice = order.TotalOrderAmount,
            items
        };
    }

    private List<OrderItemView> BuildOrderItems(OrderEntity order, OrderDataBundle bundle)
    {
        var result = new List<OrderItemView>();

        var detailItems = bundle.OrderDetails
            .Where(x => x.OrderId == order.OrderId)
            .Select(detail => new OrderItemView
            {
                Id = detail.CommodityId.ToString(),
                Name = bundle.CommodityMap.TryGetValue(detail.CommodityId, out var commodity)
                    ? commodity.ProductName
                    : $"Goods-{detail.CommodityId}",
                Price = detail.ActualUnitPrice,
                Quantity = detail.PurchaseQuantity,
                Image = NormalizeMediaUrl(bundle.CommodityMap.TryGetValue(detail.CommodityId, out var itemCommodity)
                    ? itemCommodity.ImageUrl
                    : string.Empty)
            })
            .ToList();

        var orderFoodIds = bundle.OrderFoods
            .Where(x => x.OrderId == order.OrderId)
            .Select(x => x.OrderFoodId)
            .ToHashSet();

        var mealItems = bundle.MealDetails
            .Where(x => orderFoodIds.Contains(x.OrderFoodId))
            .Select(detail => new OrderItemView
            {
                Id = detail.DishId > 0 ? detail.DishId.ToString() : detail.MealsOrderDetailsId.ToString(),
                Name = !string.IsNullOrWhiteSpace(detail.DishName)
                    ? detail.DishName
                    : bundle.DishMap.TryGetValue(detail.DishId, out var dish) ? dish.DishName : "Item",
                Price = detail.MealUnitPrice,
                Quantity = detail.MealOrderQuantity,
                Image = !string.IsNullOrWhiteSpace(detail.Taste)
                    ? NormalizeMediaUrl(detail.Taste)
                    : NormalizeMediaUrl(bundle.DishMap.TryGetValue(detail.DishId, out var dishEntity) ? dishEntity.ImageUrl : string.Empty)
            })
            .ToList();

        // 同一订单可能同时存在两套明细表数据，按订单类型优先取单一来源避免重复项。
        if (order.OrderType == 2 || order.OrderType == 4)
        {
            result.AddRange(mealItems);
            if (result.Count == 0)
            {
                result.AddRange(detailItems);
            }
        }
        else
        {
            result.AddRange(detailItems);
            if (result.Count == 0)
            {
                result.AddRange(mealItems);
            }
        }

        if (result.Count == 0)
        {
            result.Add(new OrderItemView
            {
                Id = order.OrderId.ToString(),
                Name = MapTypeText(MapType(order.OrderType)),
                Price = order.TotalOrderAmount,
                Quantity = 1,
                Image = string.Empty
            });
        }

        return result;
    }

    private static IQueryable<OrderEntity> ApplyTypeFilter(IQueryable<OrderEntity> query, string? type)
    {
        var normalized = NormalizeType(type);
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "all")
        {
            return query;
        }

        return normalized switch
        {
            "cart" => query.Where(x => x.OrderType == 1),
            "food" => query.Where(x => x.OrderType == 2),
            "acre" => query.Where(x => x.OrderType == 3),
            "activity" => query.Where(x => x.OrderType == 4),
            _ => query
        };
    }

    private static IQueryable<OrderEntity> ApplyStatusFilter(IQueryable<OrderEntity> query, string? status)
    {
        var normalized = NormalizeStatus(status);
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "all")
        {
            return query;
        }

        return normalized switch
        {
            "pending" => query.Where(x => x.PaymentStatus == 0 && x.OrderStatus != 4),
            "paid" => query.Where(x => x.OrderType == 1 && x.PaymentStatus == 1 && x.OrderStatus == 1),
            "shipping" => query.Where(x => x.OrderType == 1 && x.OrderStatus == 2),
            "completed" => query.Where(x => x.OrderStatus == 3),
            "cancelled" => query.Where(x => x.OrderStatus == 4),
            _ => query
        };
    }

    private static bool CanTransitionStatus(OrderEntity order, string targetStatus, out string message)
    {
        message = "Status transition not allowed";

        if (targetStatus == "cancelled")
        {
            if (order.OrderStatus == 3)
            {
                message = "Completed order cannot be cancelled";
                return false;
            }

            return true;
        }

        if (targetStatus == "paid")
        {
            if (order.OrderStatus == 4)
            {
                message = "Cancelled order cannot be marked paid";
                return false;
            }

            return true;
        }

        if (targetStatus == "shipping")
        {
            if (!SupportsShippingStatus(order.OrderType))
            {
                message = "Only cart orders support shipping status";
                return false;
            }

            if (order.PaymentStatus == 0)
            {
                message = "Unpaid order cannot update to this status";
                return false;
            }

            if (order.OrderStatus == 4)
            {
                message = "Cancelled order cannot update to this status";
                return false;
            }

            return true;
        }

        if (targetStatus == "completed")
        {
            if (order.PaymentStatus == 0)
            {
                message = "Unpaid order cannot update to this status";
                return false;
            }

            if (order.OrderStatus == 4)
            {
                message = "Cancelled order cannot update to this status";
                return false;
            }

            return true;
        }

        return false;
    }

    private string NormalizeMediaUrl(string? media)
    {
        if (string.IsNullOrWhiteSpace(media))
        {
            return string.Empty;
        }

        var trimmed = media.Trim();
        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        if (trimmed.StartsWith("/api/file/image/", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("api/file/image/", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("/api/file/video/", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("api/file/video/", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}/{trimmed.TrimStart('/')}";
        }

        if (trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return $"{baseUrl}{trimmed}";
        }

        var normalizedName = trimmed.TrimStart('/');
        var ext = Path.GetExtension(trimmed).ToLowerInvariant();

        if (ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv")
        {
            return $"{baseUrl}/api/file/video/{normalizedName}";
        }

        return $"{baseUrl}/api/file/image/{normalizedName}";
    }

    private int ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? Request.Query["userId"].FirstOrDefault()
            ?? Request.Headers["X-User-Id"].FirstOrDefault();

        return int.TryParse(userIdValue, out var userId) && userId > 0 ? userId : 9;
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "待付款" => "pending",
            "pendingpayment" => "pending",
            "pending_payment" => "pending",
            "待收货" => "shipping",
            "receiving" => "shipping",
            "shipped" => "shipping",
            "已完成" => "completed",
            "已取消" => "cancelled",
            _ => status.Trim().ToLowerInvariant()
        };
    }

    private static string NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return string.Empty;
        }

        return type.Trim().ToLowerInvariant() switch
        {
            "all" => "all",
            "点餐" => "food",
            "认购" => "acre",
            "活动" => "activity",
            "购物车" => "cart",
            _ => type.Trim().ToLowerInvariant()
        };
    }

    private static string MapStatus(int orderStatus, int paymentStatus)
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

    private static bool SupportsShippingStatus(int orderType)
    {
        return orderType == 1;
    }

    private static string MapStatusText(string status, int orderType)
    {
        if (orderType == 2)
        {
            return status switch
            {
                "pending" => "待付款",
                "cancelled" => "已取消",
                _ => "已完成"
            };
        }

        if (!SupportsShippingStatus(orderType))
        {
            return status switch
            {
                "pending" => "待付款",
                "completed" => "已完成",
                "cancelled" => "已取消",
                _ => "\u672A\u5B8C\u6210"
            };
        }

        return status switch
        {
            "pending" => "待付款",
            "paid" => "待发货",
            "shipping" => "待收货",
            "completed" => "已完成",
            "cancelled" => "已取消",
            _ => "待付款"
        };
    }

    private static string MapType(int orderType)
    {
        return orderType switch
        {
            2 => "food",
            3 => "acre",
            4 => "activity",
            _ => "cart"
        };
    }

    private static string MapTypeText(string type)
    {
        return type switch
        {
            "food" => "点餐",
            "acre" => "认购",
            "activity" => "活动",
            "cart" => "购物车",
            _ => "订单"
        };
    }

    private static string MapPaymentMethod(int paymentMethods)
    {
        return paymentMethods switch
        {
            2 => "wallet",
            _ => "wechat"
        };
    }

    private static string BuildTradeNo(OrderEntity order)
    {
        return $"WX{order.OrderCreationTime:yyyyMMddHHmmss}{order.OrderId}";
    }

    public sealed class PayOrderRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public sealed class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    private sealed class OrderDataBundle
    {
        public List<OrderDetail> OrderDetails { get; set; } = new();
        public Dictionary<int, Commodity> CommodityMap { get; set; } = new();
        public List<OrderFood> OrderFoods { get; set; } = new();
        public List<MealsOrderDetail> MealDetails { get; set; } = new();
        public Dictionary<int, Dish> DishMap { get; set; } = new();
    }

    private sealed class OrderItemView
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;
    }
}
