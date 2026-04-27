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
[AllowAnonymous]
[Route("api/OrderDetails")]
public class OrderDetailsController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";
    private readonly AppDbContext _dbContext;

    public OrderDetailsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(
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
            query = ApplyStatusFilter(query, status);

            var byPrice = string.Equals(sortBy, "price", StringComparison.OrdinalIgnoreCase);
            var asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

            query = byPrice
                ? asc ? query.OrderBy(x => x.TotalOrderAmount) : query.OrderByDescending(x => x.TotalOrderAmount)
                : asc ? query.OrderBy(x => x.OrderCreationTime) : query.OrderByDescending(x => x.OrderCreationTime);

            var total = await query.CountAsync(cancellationToken);
            var orders = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var orderIds = orders.Select(x => x.OrderId).ToList();
            var dataBundle = await LoadOrderDataBundleAsync(orderIds, cancellationToken);

            var items = orders
                .Select(order => BuildOrderListView(order, dataBundle))
                .ToList();

            return Ok(ApiResult.Success(new
            {
                orders = items,
                total,
                page,
                pageSize,
                hasMore = page * pageSize < total
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to load orders: {ex.Message}"));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!long.TryParse(id, out var orderId) || orderId <= 0)
            {
                return Ok(ApiResult.Fail("Invalid order id", 400));
            }

            var userId = ResolveCurrentUserId();
            var order = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            var bundle = await LoadOrderDataBundleAsync([order.OrderId], cancellationToken);
            var items = BuildOrderItems(order, bundle);

            return Ok(ApiResult.Success(new
            {
                order = new
                {
                    id = order.OrderId.ToString(),
                    status = MapStatus(order.OrderStatus, order.PaymentStatus),
                    statusText = MapStatusText(order.OrderStatus, order.PaymentStatus),
                    createTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    payTime = order.PaymentStatus == 1 ? order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    shippingTime = order.OrderStatus >= 2 ? order.OrderCreationTime.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss") : null,
                    completeTime = order.OrderStatus == 3 ? order.OrderCreationTime.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss") : null,
                    totalPrice = order.TotalOrderAmount,
                    shippingAddress = new
                    {
                        name = string.IsNullOrWhiteSpace(order.ContactPerson) ? "-" : order.ContactPerson,
                        phone = string.IsNullOrWhiteSpace(order.ContactNumber) ? "-" : order.ContactNumber,
                        address = string.IsNullOrWhiteSpace(order.ShippingAddress) ? "-" : order.ShippingAddress
                    },
                    items,
                    paymentMethod = MapPaymentMethod(order.PaymentMethods),
                    transactionId = order.PaymentStatus == 1 ? $"tx{order.OrderNumber}" : null
                }
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to load order detail: {ex.Message}"));
        }
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null)
            {
                return Ok(ApiResult.Fail("Invalid request", 400));
            }

            var totalPrice = request.MergedTotalPrice;
            if (totalPrice <= 0)
            {
                return Ok(ApiResult.Fail("Invalid request", 400));
            }

            var sourceType = request.MergedSourceType;
            var sourceName = request.MergedSourceName;
            var userId = ResolveCurrentUserId(request.MergedUserId);
            var now = DateTime.Now;
            var items = NormalizeRequestItems(request);
            var normalizedAddress = NormalizeAddress(request.MergedAddress);
            var shippingAddress = await ResolveShippingAddressAsync(userId, normalizedAddress, cancellationToken);
            var receiverName = shippingAddress?.ContactName;
            if (string.IsNullOrWhiteSpace(receiverName))
            {
                receiverName = string.IsNullOrWhiteSpace(normalizedAddress.Name) ? "N/A" : normalizedAddress.Name.Trim();
            }

            var receiverPhone = shippingAddress?.ContactPhone;
            if (string.IsNullOrWhiteSpace(receiverPhone))
            {
                receiverPhone = string.IsNullOrWhiteSpace(normalizedAddress.Phone) ? "N/A" : normalizedAddress.Phone.Trim();
            }

            var shippingAddressText = shippingAddress is not null
                ? BuildShippingAddressText(shippingAddress)
                : string.IsNullOrWhiteSpace(normalizedAddress.Address) ? "N/A" : normalizedAddress.Address.Trim();
            var addressId = shippingAddress?.AddressId
                ?? (normalizedAddress.AddressId > 0 ? normalizedAddress.AddressId : 0);

            var order = new OrderEntity
            {
                OrderNumber = GenerateOrderNumber(),
                UserId = userId,
                ActualPayment = totalPrice,
                TotalOrderAmount = totalPrice,
                OrderType = MapOrderType(sourceType),
                OrderStatus = 0,
                PaymentStatus = 0,
                DeliveryMethods = 1,
                ShippingAddress = Truncate(shippingAddressText, 45),
                AddressId = addressId,
                ContactPerson = Truncate(receiverName, 45),
                ContactNumber = Truncate(receiverPhone, 45),
                OrderCreationTime = now,
                PaymentTime = now,
                PaymentMethods = 0
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var orderFood = new OrderFood
            {
                OrderId = order.OrderId,
                MenuNumber = Truncate(string.IsNullOrWhiteSpace(sourceType) ? "mixed" : sourceType, 50),
                TableNumber = request.MergedTableNumber < 0 ? 0 : request.MergedTableNumber,
                NumberOfDiners = Math.Max(1, request.Quantity > 0 ? request.Quantity : items.Sum(x => x.Quantity)),
                Remark = Truncate(string.IsNullOrWhiteSpace(sourceName) ? request.Remark : sourceName, 255),
                CreationTime = now,
                MealServingTime = now,
                OrderStatus = 0,
                UserId = userId
            };

            _dbContext.OrderFoods.Add(orderFood);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var item in items)
            {
                var wroteGoodsDetail = false;

                if (order.OrderType == 1 && item.CommodityId > 0)
                {
                    _dbContext.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.OrderId,
                        CommodityId = item.CommodityId,
                        ActualUnitPrice = item.Price,
                        UnitPrice = item.Price,
                        PurchaseQuantity = item.Quantity,
                        SubtotalAmount = item.Price * item.Quantity
                    });

                    wroteGoodsDetail = true;
                }

                if (order.OrderType != 1 || !wroteGoodsDetail)
                {
                    _dbContext.MealsOrderDetails.Add(new MealsOrderDetail
                    {
                        OrderFoodId = orderFood.OrderFoodId,
                        DishId = item.DishId,
                        DishName = Truncate(item.Name, 255),
                        MealUnitPrice = item.Price,
                        MealOrderQuantity = item.Quantity,
                        MealSubtotalAmount = item.Price * item.Quantity,
                        Taste = Truncate(item.Image, 255),
                        MealStatus = 0
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                id = order.OrderId.ToString(),
                orderId = order.OrderId.ToString(),
                orderNumber = order.OrderNumber,
                status = "pending",
                totalPrice = order.TotalOrderAmount,
                createTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to create order: {ex.Message}"));
        }
    }

    [HttpPost("{id}/pay")]
    public async Task<IActionResult> Pay(string id, [FromBody] OrderDetailsPayRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!long.TryParse(id, out var orderId) || orderId <= 0)
            {
                return Ok(ApiResult.Fail("Invalid order id", 400));
            }

            var userId = ResolveCurrentUserId();
            var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            if (order.PaymentStatus == 1)
            {
                return Ok(ApiResult.Fail("Order already paid", 409));
            }

            order.PaymentStatus = 1;
            order.OrderStatus = order.OrderStatus == 4 ? 4 : 1;
            order.PaymentMethods = string.Equals(request?.PaymentMethod, "wechat", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            order.PaymentTime = DateTime.Now;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var stamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            var prepayId = $"wx{DateTime.Now:yyyyMMddHHmmssfff}";

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                status = MapStatus(order.OrderStatus, order.PaymentStatus),
                statusText = MapStatusText(order.OrderStatus, order.PaymentStatus),
                paymentInfo = new
                {
                    prepayId,
                    timeStamp = stamp,
                    nonceStr = Guid.NewGuid().ToString("N").Substring(0, 16),
                    package = $"prepay_id={prepayId}",
                    signType = "MD5",
                    paySign = Guid.NewGuid().ToString("N")
                }
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to pay order: {ex.Message}"));
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id, [FromBody] OrderDetailsCancelRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!long.TryParse(id, out var orderId) || orderId <= 0)
            {
                return Ok(ApiResult.Fail("Invalid order id", 400));
            }

            var userId = ResolveCurrentUserId();
            var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            if (order.PaymentStatus == 1)
            {
                return Ok(ApiResult.Fail("Paid orders cannot be cancelled", 409));
            }

            order.OrderStatus = 4;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                status = "cancelled",
                statusText = "Cancelled"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to cancel order: {ex.Message}"));
        }
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!long.TryParse(id, out var orderId) || orderId <= 0)
            {
                return Ok(ApiResult.Fail("Invalid order id", 400));
            }

            var userId = ResolveCurrentUserId();
            var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("Order not found", 404));
            }

            if (order.PaymentStatus == 0 || order.OrderStatus == 4)
            {
                return Ok(ApiResult.Fail("Order status mismatch", 409));
            }

            order.OrderStatus = 3;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId.ToString(),
                status = "completed",
                statusText = "Completed"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to confirm receipt: {ex.Message}"));
        }
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

    private object BuildOrderListView(OrderEntity order, OrderDataBundle bundle)
    {
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
            status = MapStatus(order.OrderStatus, order.PaymentStatus),
            statusText = MapStatusText(order.OrderStatus, order.PaymentStatus),
            createTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
            totalPrice = order.TotalOrderAmount,
            orderType = order.OrderType,
            orderTypeText = MapOrderTypeText(order.OrderType),
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
            var orderFood = bundle.OrderFoods.FirstOrDefault(x => x.OrderId == order.OrderId);
            result.Add(new OrderItemView
            {
                Id = order.OrderId.ToString(),
                Name = string.IsNullOrWhiteSpace(orderFood?.Remark) ? MapOrderTypeText(order.OrderType) : orderFood!.Remark,
                Price = order.TotalOrderAmount,
                Quantity = 1,
                Image = string.Empty
            });
        }

        return result;
    }

    private int ResolveCurrentUserId(int? preferredUserId = null)
    {
        if (preferredUserId.HasValue && preferredUserId.Value > 0)
        {
            return preferredUserId.Value;
        }

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? Request.Query["userId"].FirstOrDefault()
            ?? Request.Headers["X-User-Id"].FirstOrDefault();

        return int.TryParse(userIdValue, out var userId) && userId > 0 ? userId : 9;
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
            "paid" => query.Where(x => x.PaymentStatus == 1 && x.OrderStatus == 1),
            "shipping" => query.Where(x => x.OrderStatus == 2),
            "completed" => query.Where(x => x.OrderStatus == 3),
            "cancelled" => query.Where(x => x.OrderStatus == 4),
            _ => query
        };
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

    private static string MapStatusText(int orderStatus, int paymentStatus)
    {
        return MapStatus(orderStatus, paymentStatus) switch
        {
            "pending" => "Pending Payment",
            "paid" => "Paid",
            "shipping" => "Pending Receipt",
            "completed" => "Completed",
            "cancelled" => "Cancelled",
            _ => "Pending Payment"
        };
    }

    private static int MapOrderType(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return 1;
        }

        return sourceType.Trim().ToLowerInvariant() switch
        {
            "food" => 2,
            "acre" => 3,
            "activity" => 4,
            "cart" => 1,
            "goods" => 1,
            _ => 1
        };
    }

    private static string MapOrderTypeText(int orderType)
    {
        return orderType switch
        {
            2 => "Dining Order",
            3 => "Acre Subscription",
            4 => "Activity Voucher",
            _ => "Mall Order"
        };
    }

    private static string? MapPaymentMethod(int paymentMethods)
    {
        return paymentMethods switch
        {
            1 => "wechat",
            2 => "wallet",
            _ => null
        };
    }

    private List<NormalizedCreateItem> NormalizeRequestItems(CreateOrderRequest request)
    {
        var normalized = new List<NormalizedCreateItem>();

        foreach (var item in request.MergedItems ?? [])
        {
            var rawId = string.IsNullOrWhiteSpace(item.Id) ? item.IdAlias ?? string.Empty : item.Id;
            var name = string.IsNullOrWhiteSpace(item.Name) ? item.NameAlias ?? "Item" : item.Name.Trim();
            var price = item.Price > 0 ? item.Price : item.PriceAlias ?? 0;
            if (price < 0)
            {
                price = 0;
            }

            var quantity = item.Quantity > 0 ? item.Quantity : item.QuantityAlias ?? 1;
            if (quantity <= 0)
            {
                quantity = 1;
            }

            var image = string.IsNullOrWhiteSpace(item.Image) ? item.ImageAlias ?? string.Empty : item.Image;

            int.TryParse(rawId, out var parsedId);

            normalized.Add(new NormalizedCreateItem
            {
                CommodityId = parsedId > 0 ? parsedId : 0,
                DishId = parsedId > 0 ? parsedId : 0,
                Name = name,
                Price = price,
                Quantity = quantity,
                Image = image
            });
        }

        if (normalized.Count == 0)
        {
            normalized.Add(new NormalizedCreateItem
            {
                CommodityId = 0,
                DishId = 0,
                Name = string.IsNullOrWhiteSpace(request.MergedSourceName) ? "Item" : request.MergedSourceName.Trim(),
                Price = request.MergedTotalPrice,
                Quantity = request.Quantity <= 0 ? 1 : request.Quantity,
                Image = string.Empty
            });
        }

        return normalized;
    }

    private static CreateOrderAddress NormalizeAddress(CreateOrderAddress? raw)
    {
        if (raw is null)
        {
            return new CreateOrderAddress();
        }

        return new CreateOrderAddress
        {
            AddressId = raw.AddressId > 0 ? raw.AddressId : raw.AddressIdAlias ?? 0,
            Name = string.IsNullOrWhiteSpace(raw.Name) ? raw.ContactNameAlias ?? string.Empty : raw.Name,
            Phone = string.IsNullOrWhiteSpace(raw.Phone) ? raw.ContactPhoneAlias ?? string.Empty : raw.Phone,
            Address = string.IsNullOrWhiteSpace(raw.Address) ? raw.DetailAlias ?? string.Empty : raw.Address
        };
    }

    private async Task<ShippingAddress?> ResolveShippingAddressAsync(int userId, CreateOrderAddress address, CancellationToken cancellationToken)
    {
        var query = _dbContext.ShippingAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (address.AddressId > 0)
        {
            var matchedAddress = await query
                .FirstOrDefaultAsync(x => x.AddressId == address.AddressId, cancellationToken);

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

    private static string BuildShippingAddressText(ShippingAddress address)
    {
        return $"{address.Province}{address.City}{address.MunicipalDistrict}{address.Town}{address.HouseNumber}";
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

    private static string GenerateOrderNumber()
    {
        return $"{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public sealed class OrderDetailsPayRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal PayAmount { get; set; }
    }

    public sealed class OrderDetailsCancelRequest
    {
        public string? Reason { get; set; }
    }

    public sealed class CreateOrderRequest
    {
        public int? UserId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int Quantity { get; set; }
        public int TableNumber { get; set; }
        public decimal TotalPrice { get; set; }
        public string Remark { get; set; } = string.Empty;
        public CreateOrderAddress? Address { get; set; }
        public List<CreateOrderItem>? Items { get; set; }

        [JsonPropertyName("source_type")]
        public string? SourceTypeAlias { get; set; }

        [JsonPropertyName("source_name")]
        public string? SourceNameAlias { get; set; }

        [JsonPropertyName("source_id")]
        public int? SourceIdAlias { get; set; }

        [JsonPropertyName("table_number")]
        public int? TableNumberAlias { get; set; }

        [JsonPropertyName("total_price")]
        public decimal? TotalPriceAlias { get; set; }

        [JsonPropertyName("user_id")]
        public int? UserIdAlias { get; set; }

        [JsonPropertyName("address_info")]
        public CreateOrderAddress? AddressAlias { get; set; }

        [JsonPropertyName("item_list")]
        public List<CreateOrderItem>? ItemsAlias { get; set; }

        [JsonIgnore]
        public CreateOrderAddress? MergedAddress => Address ?? AddressAlias;

        [JsonIgnore]
        public List<CreateOrderItem>? MergedItems => Items ?? ItemsAlias;

        [JsonIgnore]
        public string MergedSourceType => string.IsNullOrWhiteSpace(SourceType) ? SourceTypeAlias ?? string.Empty : SourceType;

        [JsonIgnore]
        public string MergedSourceName => string.IsNullOrWhiteSpace(SourceName) ? SourceNameAlias ?? string.Empty : SourceName;

        [JsonIgnore]
        public int MergedSourceId => SourceId > 0 ? SourceId : SourceIdAlias ?? 0;

        [JsonIgnore]
        public int MergedTableNumber => TableNumber > 0 ? TableNumber : TableNumberAlias ?? 0;

        [JsonIgnore]
        public decimal MergedTotalPrice => TotalPrice > 0 ? TotalPrice : TotalPriceAlias ?? 0;

        [JsonIgnore]
        public int? MergedUserId => UserId ?? UserIdAlias;
    }

    public sealed class CreateOrderAddress
    {
        public int AddressId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("address_id")]
        public int? AddressIdAlias { get; set; }

        [JsonPropertyName("contact_name")]
        public string? ContactNameAlias { get; set; }

        [JsonPropertyName("contact_phone")]
        public string? ContactPhoneAlias { get; set; }

        [JsonPropertyName("detail")]
        public string? DetailAlias { get; set; }
    }

    public sealed class CreateOrderItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("item_id")]
        public string? IdAlias { get; set; }

        [JsonPropertyName("item_name")]
        public string? NameAlias { get; set; }

        [JsonPropertyName("unit_price")]
        public decimal? PriceAlias { get; set; }

        [JsonPropertyName("count")]
        public int? QuantityAlias { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageAlias { get; set; }
    }

    private sealed class NormalizedCreateItem
    {
        public int CommodityId { get; set; }
        public int DishId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;
    }

    private sealed class OrderDataBundle
    {
        public List<OrderDetail> OrderDetails { get; set; } = [];
        public Dictionary<int, Commodity> CommodityMap { get; set; } = [];
        public List<OrderFood> OrderFoods { get; set; } = [];
        public List<MealsOrderDetail> MealDetails { get; set; } = [];
        public Dictionary<int, Dish> DishMap { get; set; } = [];
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
