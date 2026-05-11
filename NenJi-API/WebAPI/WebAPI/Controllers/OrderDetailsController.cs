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
[Route("api/OrderDetails")]
public class OrderDetailsController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";
    private readonly AppDbContext _dbContext;
    private readonly IInventoryService _inventoryService;

    public OrderDetailsController(AppDbContext dbContext, IInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
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
            page = Math.Max(1, page);
            pageSize = Math.Max(1, pageSize);

            await EnsureDishStatusCacheAsync();
            var userId = ResolveCurrentUserId();
            var normalizedType = NormalizeOrderType(type);
            var normalizedStatus = NormalizeStatus(status);
            var asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
            var byPrice = string.Equals(sortBy, "price", StringComparison.OrdinalIgnoreCase);

            var allOrders = new List<AggregatedOrder>();

            if (normalizedType is "all" or "goods")
            {
                var commodityOrders = await _dbContext.CommodityOrders.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .Where(CommodityStatusFilter(normalizedStatus))
                    .ToListAsync(cancellationToken);
                allOrders.AddRange(commodityOrders.Select(x => AggregatedOrder.FromCommodity(x)));
            }

            if (normalizedType is "all" or "food")
            {
                var dishOrders = await _dbContext.DishOrders.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .Where(DishStatusFilter(normalizedStatus))
                    .ToListAsync(cancellationToken);
                allOrders.AddRange(dishOrders.Select(x => AggregatedOrder.FromDish(x)));
            }

            if (normalizedType is "all" or "activity")
            {
                var activityOrders = await _dbContext.ActivityOrders.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .Where(ActivityStatusFilter(normalizedStatus))
                    .ToListAsync(cancellationToken);
                allOrders.AddRange(activityOrders.Select(x => AggregatedOrder.FromActivity(x)));
            }

            var total = allOrders.Count;

            if (byPrice)
            {
                allOrders = asc
                    ? allOrders.OrderBy(x => x.TotalAmount).ToList()
                    : allOrders.OrderByDescending(x => x.TotalAmount).ToList();
            }
            else
            {
                allOrders = asc
                    ? allOrders.OrderBy(x => x.CreateTime).ToList()
                    : allOrders.OrderByDescending(x => x.CreateTime).ToList();
            }

            var pageOrders = allOrders.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var itemMap = await LoadAggregatedItemsAsync(pageOrders, cancellationToken);

            var items = pageOrders.Select(order => BuildAggregatedListView(order, itemMap)).ToList();

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
            await EnsureDishStatusCacheAsync();
            await EnsureDishStatusCacheAsync();

            var commodityOrder = await _dbContext.CommodityOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (commodityOrder is not null)
            {
                var agg = AggregatedOrder.FromCommodity(commodityOrder);
                var itemMap = await LoadAggregatedItemsAsync([agg], cancellationToken);
                var address = await _dbContext.ShippingAddresses.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.AddressId == commodityOrder.AddressId, cancellationToken);
                return Ok(ApiResult.Success(BuildAggregatedDetailView(agg, itemMap, address)));
            }

            var dishOrder = await _dbContext.DishOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (dishOrder is not null)
            {
                var agg = AggregatedOrder.FromDish(dishOrder);
                var itemMap = await LoadAggregatedItemsAsync([agg], cancellationToken);
                return Ok(ApiResult.Success(BuildAggregatedDetailView(agg, itemMap, null)));
            }

            var activityOrder = await _dbContext.ActivityOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (activityOrder is not null)
            {
                var agg = AggregatedOrder.FromActivity(activityOrder);
                var itemMap = await LoadAggregatedItemsAsync([agg], cancellationToken);
                return Ok(ApiResult.Success(BuildAggregatedDetailView(agg, itemMap, null)));
            }

            return Ok(ApiResult.Fail("Order not found", 404));
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

            var items = NormalizeRequestItems(request);
            var totalPrice = request.MergedTotalPrice > 0
                ? request.MergedTotalPrice
                : items.Sum(x => x.Price * x.Quantity);
            if (totalPrice <= 0)
            {
                return Ok(ApiResult.Fail("Invalid request", 400));
            }

            var sourceType = (request.MergedSourceType ?? string.Empty).Trim().ToLowerInvariant();
            switch (sourceType)
            {
                case "goods":
                case "cart":
                case "commodity":
                    return await CreateCommodityOrderAsync(request, items, cancellationToken);
                case "food":
                case "dish":
                    return await CreateDishOrderAsync(request, items, cancellationToken);
                case "activity":
                    return await CreateActivityOrderAsync(request, items, cancellationToken);
                default:
                    return Ok(ApiResult.Fail("sourceType 参数不正确", 400));
            }
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to create order: {ex.Message}"));
        }
    }

    private async Task<IActionResult> CreateCommodityOrderAsync(
        CreateOrderRequest request,
        List<NormalizedCreateItem> items,
        CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(request.MergedUserId);
        var normalizedAddress = NormalizeAddress(request.MergedAddress);
        var shippingAddress = await ResolveShippingAddressAsync(userId, normalizedAddress, cancellationToken);
        var addressId = shippingAddress?.AddressId
            ?? (normalizedAddress.AddressId > 0 ? normalizedAddress.AddressId : 0);

        if (addressId <= 0)
        {
            return Ok(ApiResult.Fail("address is required", 400));
        }

        var commodityIds = items
            .Where(x => x.CommodityId > 0)
            .Select(x => x.CommodityId)
            .Distinct()
            .ToList();
        if (commodityIds.Count == 0)
        {
            return Ok(ApiResult.Fail("commodity not found", 404));
        }

        var commodities = await _dbContext.Commodities
            .Where(x => commodityIds.Contains(x.CommodityId) && (x.ProductStatus ?? 0) == 1)
            .ToListAsync(cancellationToken);
        var commodityMap = commodities.ToDictionary(x => x.CommodityId);

        if (commodityMap.Count == 0 || items.Any(x => x.CommodityId <= 0 || !commodityMap.ContainsKey(x.CommodityId)))
        {
            return Ok(ApiResult.Fail("commodity not found", 404));
        }

        var normalizedItems = items.Select(x =>
        {
            var commodity = commodityMap[x.CommodityId];
            var price = (commodity.UnitPrice ?? 0m) > 0 ? commodity.UnitPrice!.Value : x.Price;
            return new
            {
                x.CommodityId,
                Price = price,
                Quantity = Math.Max(1, x.Quantity),
                Name = commodity.ProductName
            };
        }).ToList();

        if (normalizedItems.Any(x => x.Price <= 0))
        {
            return Ok(ApiResult.Fail("商品价格异常", 409));
        }

        var totalAmount = normalizedItems.Sum(x => x.Price * x.Quantity);
        if (totalAmount <= 0)
        {
            return Ok(ApiResult.Fail("订单金额异常", 409));
        }

        var now = DateTime.Now;
        var order = new CommodityOrder
        {
            OrderNo = GenerateCommodityOrderNo(),
            WxPayNo = null,
            TotalAmount = totalAmount,
            TotalQuantity = normalizedItems.Sum(x => x.Quantity),
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now,
            AddressId = addressId,
            TrackingNumber = null,
            TrackingTypeId = null
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 原子扣减库存
        var deductResult = await _inventoryService.DeductBatchAsync(
            ProductType.Commodity,
            normalizedItems.Select(x => (x.CommodityId, x.Quantity, x.Name)).ToList());
        if (!deductResult.Success)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Ok(ApiResult.Fail(deductResult.ErrorMessage!, 409));
        }

        _dbContext.CommodityOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in normalizedItems)
        {
            _dbContext.CommodityOrderDetails.Add(new CommodityOrderDetail
            {
                OrderId = order.OrderId,
                CommodityId = item.CommodityId,
                GoodsName = item.Name,
                UnitPrice = item.Price,
                Quantity = item.Quantity,
                SubtotalAmount = item.Price * item.Quantity,
                StatusId = 1
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            id = order.OrderNo,
            orderId = order.OrderNo,
            orderNumber = order.OrderNo,
            orderType = "goods",
            status = "pending",
            totalPrice = order.TotalAmount,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        }));
    }

    private async Task<IActionResult> CreateDishOrderAsync(
        CreateOrderRequest request,
        List<NormalizedCreateItem> items,
        CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(request.MergedUserId);
        var diningTableId = request.MergedTableNumber;
        if (diningTableId <= 0)
        {
            return Ok(ApiResult.Fail("tableId 参数不正确", 400));
        }

        var tableExists = await _dbContext.DiningTables
            .AsNoTracking()
            .AnyAsync(x => x.DiningTableId == diningTableId, cancellationToken);
        if (!tableExists)
        {
            return Ok(ApiResult.Fail("桌台不存在", 404));
        }

        var dishIds = items
            .Where(x => x.DishId > 0)
            .Select(x => x.DishId)
            .Distinct()
            .ToList();

        if (dishIds.Count == 0)
        {
            return Ok(ApiResult.Fail("items 不能为空", 400));
        }

        var dishMap = await _dbContext.Dishes
            .AsNoTracking()
            .Where(x => dishIds.Contains(x.DishId) && x.Status == 1)
            .ToDictionaryAsync(x => x.DishId, cancellationToken);

        if (dishMap.Count == 0 || items.Any(x => x.DishId <= 0 || !dishMap.ContainsKey(x.DishId)))
        {
            return Ok(ApiResult.Fail("dish not found", 404));
        }

        var normalizedItems = items.Select(x =>
        {
            var dish = dishMap[x.DishId];
            return new
            {
                DishId = x.DishId,
                Price = dish.DishPrice,
                Quantity = Math.Max(1, x.Quantity),
                Name = dish.DishName
            };
        }).ToList();

        var totalAmount = normalizedItems.Sum(x => x.Price * x.Quantity);
        if (totalAmount <= 0)
        {
            return Ok(ApiResult.Fail("订单金额异常", 409));
        }

        var now = DateTime.Now;
        var order = new DishOrder
        {
            OrderNo = GenerateDishOrderNo(),
            WxPayNo = null,
            TotalAmount = totalAmount,
            TotalQuantity = normalizedItems.Sum(x => x.Quantity),
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now,
            DiningTableId = diningTableId,
            Remark = request.Remark
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 原子扣减菜品库存
        var deductResult = await _inventoryService.DeductBatchAsync(
            ProductType.Dish,
            normalizedItems.Select(x => (x.DishId, x.Quantity, x.Name)).ToList());
        if (!deductResult.Success)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Ok(ApiResult.Fail(deductResult.ErrorMessage!, 409));
        }

        _dbContext.DishOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in normalizedItems)
        {
            _dbContext.DishOrderDetails.Add(new DishOrderDetail
            {
                DishOrderId = order.OrderId,
                DishId = item.DishId,
                GoodsName = item.Name,
                UnitPrice = item.Price,
                Quantity = item.Quantity,
                SubtotalAmount = item.Price * item.Quantity,
                StatusId = 1
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await UpdateTableOccupiedAsync(diningTableId, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            id = order.OrderNo,
            orderId = order.OrderNo,
            orderNumber = order.OrderNo,
            orderType = "food",
            status = "pending",
            totalPrice = order.TotalAmount,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            remark = order.Remark
        }));
    }

    private async Task<IActionResult> CreateActivityOrderAsync(
        CreateOrderRequest request,
        List<NormalizedCreateItem> items,
        CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(request.MergedUserId);
        var activityId = request.MergedSourceId > 0 ? request.MergedSourceId : items.FirstOrDefault(x => x.ActivityId > 0)?.ActivityId ?? 0;
        if (activityId <= 0)
        {
            return Ok(ApiResult.Fail("activityId 参数不正确", 400));
        }

        var quantity = request.Quantity > 0 ? request.Quantity : items.Sum(x => Math.Max(1, x.Quantity));
        quantity = Math.Max(1, quantity);

        var activity = await _dbContext.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StatusId == 1 && x.ActivityId == activityId, cancellationToken);

        if (activity is null)
        {
            return Ok(ApiResult.Fail("活动不存在", 404));
        }

        var totalAmount = activity.Price * quantity;
        if (totalAmount <= 0)
        {
            return Ok(ApiResult.Fail("订单金额异常", 409));
        }

        var now = DateTime.Now;
        var order = new ActivityOrder
        {
            OrderNo = GenerateActivityOrderNo(),
            WxPayNo = null,
            TotalAmount = totalAmount,
            TotalQuantity = quantity,
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now
        };

        _dbContext.ActivityOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.ActivityOrderDetails.Add(new ActivityOrderDetail
        {
            ActivityOrderId = order.OrderId,
            ActivityId = activity.ActivityId,
            UnitPrice = activity.Price,
            Quantity = quantity,
            SubtotalAmount = totalAmount,
            ActivityQrcode = null
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            id = order.OrderNo,
            orderId = order.OrderNo,
            orderNumber = order.OrderNo,
            orderType = "activity",
            status = "pending",
            totalPrice = order.TotalAmount,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        }));
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
            await EnsureDishStatusCacheAsync();

            var commodityOrder = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (commodityOrder is not null)
            {
                if (commodityOrder.OrderStatusId == 1)
                {
                    commodityOrder.OrderStatusId = 2;
                    commodityOrder.WxPayNo = $"MOCK_{DateTime.Now:yyyyMMddHHmmssfff}";
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                return Ok(ApiResult.Success(new
                {
                    orderId = commodityOrder.OrderId.ToString(),
                    status = MapCommodityStatusValue(commodityOrder.OrderStatusId),
                    statusText = MapCommodityStatusText(commodityOrder.OrderStatusId)
                }));
            }

            var dishOrder = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (dishOrder is not null)
            {
                if (dishOrder.OrderStatusId == 1)
                {
                    dishOrder.OrderStatusId = 2;
                    dishOrder.WxPayNo = $"MOCK_{DateTime.Now:yyyyMMddHHmmssfff}";
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                return Ok(ApiResult.Success(new
                {
                    orderId = dishOrder.OrderId.ToString(),
                    status = MapDishStatusValue(dishOrder.OrderStatusId),
                    statusText = MapDishStatusText(dishOrder.OrderStatusId)
                }));
            }

            var activityOrder = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (activityOrder is not null)
            {
                if (activityOrder.OrderStatusId == 1)
                {
                    activityOrder.OrderStatusId = 2;
                    activityOrder.WxPayNo = $"MOCK_{DateTime.Now:yyyyMMddHHmmssfff}";
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                return Ok(ApiResult.Success(new
                {
                    orderId = activityOrder.OrderId.ToString(),
                    status = MapActivityStatusValue(activityOrder.OrderStatusId),
                    statusText = MapActivityStatusText(activityOrder.OrderStatusId)
                }));
            }

            return Ok(ApiResult.Fail("Order not found", 404));
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
            await EnsureDishStatusCacheAsync();

            var commodityOrder = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (commodityOrder is not null)
            {
                if (commodityOrder.OrderStatusId != 1)
                {
                    return Ok(ApiResult.Fail("Paid orders cannot be cancelled", 409));
                }

                // 恢复商品库存
                var details = await _dbContext.CommodityOrderDetails
                    .Where(x => x.OrderId == commodityOrder.OrderId)
                    .ToListAsync(cancellationToken);
                foreach (var d in details)
                {
                    await _inventoryService.RestoreAsync(ProductType.Commodity, d.CommodityId, d.Quantity);
                }

                commodityOrder.OrderStatusId = 5;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Ok(ApiResult.Success(new { orderId = commodityOrder.OrderId.ToString(), status = "cancelled", statusText = "Cancelled" }));
            }

            var dishOrder = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (dishOrder is not null)
            {
                if (dishOrder.OrderStatusId != 1)
                {
                    return Ok(ApiResult.Fail("Paid orders cannot be cancelled", 409));
                }

                // 恢复菜品库存
                var dishDetails = await _dbContext.DishOrderDetails
                    .Where(x => x.DishOrderId == dishOrder.OrderId)
                    .ToListAsync(cancellationToken);
                foreach (var d in dishDetails)
                {
                    await _inventoryService.RestoreAsync(ProductType.Dish, d.DishId, d.Quantity);
                }

                dishOrder.OrderStatusId = 4;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await TryFreeTableAsync(dishOrder.DiningTableId, cancellationToken);
                return Ok(ApiResult.Success(new { orderId = dishOrder.OrderId.ToString(), status = "cancelled", statusText = "Cancelled" }));
            }

            var activityOrder = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (activityOrder is not null)
            {
                if (activityOrder.OrderStatusId != 1)
                {
                    return Ok(ApiResult.Fail("Paid orders cannot be cancelled", 409));
                }
                activityOrder.OrderStatusId = 4;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Ok(ApiResult.Success(new { orderId = activityOrder.OrderId.ToString(), status = "cancelled", statusText = "Cancelled" }));
            }

            return Ok(ApiResult.Fail("Order not found", 404));
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
            await EnsureDishStatusCacheAsync();

            var commodityOrder = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (commodityOrder is not null)
            {
                if (commodityOrder.OrderStatusId is 2 or 3)
                {
                    commodityOrder.OrderStatusId = 4;
                    await SyncDetailStatusAsync(commodityOrder.OrderId, 4, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                return Ok(ApiResult.Success(new { orderId = commodityOrder.OrderNo, orderNumber = commodityOrder.OrderNo, orderNo = commodityOrder.OrderNo, status = "completed", statusText = "Completed" }));
            }

            var dishOrder = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (dishOrder is not null)
            {
                if (dishOrder.OrderStatusId == 2)
                {
                    dishOrder.OrderStatusId = 3;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await TryFreeTableAsync(dishOrder.DiningTableId, cancellationToken);
                }
                return Ok(ApiResult.Success(new { orderId = dishOrder.OrderNo, orderNumber = dishOrder.OrderNo, orderNo = dishOrder.OrderNo, status = "completed", statusText = "Completed" }));
            }

            return Ok(ApiResult.Fail("Order not found", 404));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"Failed to confirm receipt: {ex.Message}"));
        }
    }

    // ---- Aggregated order helpers ----

    private sealed class AggregatedOrder
    {
        public string Type { get; init; } = string.Empty;
        public string TypeText { get; init; } = string.Empty;
        public long OrderId { get; init; }
        public string OrderNo { get; init; } = string.Empty;
        public DateTime CreateTime { get; init; }
        public decimal TotalAmount { get; init; }
        public int TotalQuantity { get; init; }
        public int RawStatusId { get; init; }
        public long AddressId { get; init; }
        public string? WxPayNo { get; init; }
        public string? Remark { get; init; }

        public static AggregatedOrder FromCommodity(CommodityOrder o) => new()
        {
            Type = "goods", TypeText = "Mall Order", OrderId = o.OrderId,
            OrderNo = o.OrderNo, CreateTime = o.CreateTime, TotalAmount = o.TotalAmount,
            TotalQuantity = o.TotalQuantity, RawStatusId = o.OrderStatusId, AddressId = o.AddressId,
            WxPayNo = o.WxPayNo
        };

        public static AggregatedOrder FromDish(DishOrder o) => new()
        {
            Type = "food", TypeText = "Dining Order", OrderId = o.OrderId,
            OrderNo = o.OrderNo, CreateTime = o.CreateTime, TotalAmount = o.TotalAmount,
            TotalQuantity = o.TotalQuantity, RawStatusId = o.OrderStatusId, AddressId = 0,
            WxPayNo = o.WxPayNo, Remark = o.Remark
        };

        public static AggregatedOrder FromActivity(ActivityOrder o) => new()
        {
            Type = "activity", TypeText = "Activity Voucher", OrderId = o.OrderId,
            OrderNo = o.OrderNo, CreateTime = o.CreateTime, TotalAmount = o.TotalAmount,
            TotalQuantity = o.TotalQuantity, RawStatusId = o.OrderStatusId, AddressId = 0,
            WxPayNo = o.WxPayNo
        };
    }

    private async Task<Dictionary<string, List<AggregatedItem>>> LoadAggregatedItemsAsync(
        IReadOnlyCollection<AggregatedOrder> orders,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<AggregatedItem>>(StringComparer.OrdinalIgnoreCase);
        if (orders.Count == 0) return result;

        var goods = orders.Where(x => x.Type == "goods").ToList();
        if (goods.Count > 0)
        {
            var orderIds = goods.Select(x => x.OrderId).Distinct().ToList();
            var details = await _dbContext.CommodityOrderDetails.AsNoTracking()
                .Where(x => orderIds.Contains(x.OrderId))
                .ToListAsync(cancellationToken);
            var commodityIds = details.Select(x => x.CommodityId).Distinct().ToList();
            var commodityMap = commodityIds.Count == 0
                ? new Dictionary<int, Commodity>()
                : await _dbContext.Commodities.AsNoTracking()
                    .Where(x => commodityIds.Contains(x.CommodityId))
                    .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

            foreach (var group in details.GroupBy(x => x.OrderId))
            {
                var orderNo = goods.First(o => o.OrderId == group.Key).OrderNo;
                result[orderNo] = group.Select(d =>
                {
                    commodityMap.TryGetValue(d.CommodityId, out var c);
                    return new AggregatedItem
                    {
                        Id = d.CommodityId.ToString(),
                        Name = !string.IsNullOrEmpty(d.GoodsName) ? d.GoodsName
                            : c?.ProductName ?? $"商品{d.CommodityId}",
                        Price = d.UnitPrice,
                        Quantity = d.Quantity,
                        Image = !string.IsNullOrEmpty(d.ImageUrl) ? d.ImageUrl
                            : NormalizeMediaUrl(c?.ImageUrl),
                        StatusId = d.StatusId ?? 1
                    };
                }).ToList();
            }
        }

        var food = orders.Where(x => x.Type == "food").ToList();
        if (food.Count > 0)
        {
            var orderIds = food.Select(x => x.OrderId).Distinct().ToList();
            var details = await _dbContext.DishOrderDetails.AsNoTracking()
                .Where(x => orderIds.Contains(x.DishOrderId))
                .ToListAsync(cancellationToken);
            var dishIds = details.Select(x => x.DishId).Distinct().ToList();
            var dishMap = dishIds.Count == 0
                ? new Dictionary<int, Dish>()
                : await _dbContext.Dishes.AsNoTracking()
                    .Where(x => dishIds.Contains(x.DishId))
                    .ToDictionaryAsync(x => x.DishId, cancellationToken);

            foreach (var group in details.GroupBy(x => x.DishOrderId))
            {
                var orderNo = food.First(o => o.OrderId == group.Key).OrderNo;
                result[orderNo] = group.Select(d =>
                {
                    dishMap.TryGetValue(d.DishId, out var dish);
                    return new AggregatedItem
                    {
                        Id = d.DishId.ToString(),
                        Name = !string.IsNullOrEmpty(d.GoodsName) ? d.GoodsName
                            : dish?.DishName ?? $"菜品{d.DishId}",
                        Price = d.UnitPrice,
                        Quantity = d.Quantity,
                        Image = !string.IsNullOrEmpty(d.ImageUrl) ? d.ImageUrl
                            : NormalizeMediaUrl(dish?.ImageUrl),
                        StatusId = d.StatusId ?? 1
                    };
                }).ToList();
            }
        }

        var activity = orders.Where(x => x.Type == "activity").ToList();
        if (activity.Count > 0)
        {
            var orderIds = activity.Select(x => x.OrderId).Distinct().ToList();
            var details = await _dbContext.ActivityOrderDetails.AsNoTracking()
                .Where(x => orderIds.Contains(x.ActivityOrderId))
                .ToListAsync(cancellationToken);
            var activityIds = details.Select(d => d.ActivityId).Distinct().ToList();
            var activityMap = activityIds.Count == 0
                ? new Dictionary<long, ActivityEntity>()
                : await _dbContext.Activities.AsNoTracking()
                    .Where(x => activityIds.Contains(x.ActivityId))
                    .ToDictionaryAsync(x => x.ActivityId, cancellationToken);

            foreach (var group in details.GroupBy(x => x.ActivityOrderId))
            {
                var orderNo = activity.First(o => o.OrderId == group.Key).OrderNo;
                result[orderNo] = group.Select(d =>
                {
                    activityMap.TryGetValue(d.ActivityId, out var a);
                    return new AggregatedItem
                    {
                        Id = d.ActivityId.ToString(),
                        Name = a?.Title ?? $"活动{d.ActivityId}",
                        Price = d.UnitPrice,
                        Quantity = d.Quantity,
                        Image = NormalizeMediaUrl(a?.ImageUrl)
                    };
                }).ToList();
            }
        }

        return result;
    }

    private static object BuildAggregatedListView(AggregatedOrder order, IReadOnlyDictionary<string, List<AggregatedItem>> itemMap)
    {
        itemMap.TryGetValue(order.OrderNo, out var items);
        items ??= [];

        return new
        {
            id = order.OrderId.ToString(),
            orderId = order.OrderId.ToString(),
            orderNumber = order.OrderNo,
            orderNo = order.OrderNo,
            status = MapAggregateStatusValue(order.Type, order.RawStatusId),
            statusText = MapAggregateStatusText(order.Type, order.RawStatusId),
            orderStatusId = order.RawStatusId,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            paymentTime = order.RawStatusId >= 2 ? order.CreateTime.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            shippingTime = order.RawStatusId >= 3 ? order.CreateTime.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            completeTime = order.RawStatusId >= 4 ? order.CreateTime.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            totalQuantity = order.TotalQuantity,
            orderType = order.Type,
            orderTypeText = order.TypeText,
            transactionId = order.RawStatusId >= 2 ? order.WxPayNo : null,
            remark = order.Remark,
            verified = order.Type == "activity" && order.RawStatusId == 3,
            items = items.Select(x => new { id = x.Id, name = x.Name, price = x.Price, quantity = x.Quantity, image = x.Image, statusId = x.StatusId, status = MapDetailStatusValue(x.StatusId) }).ToList()
        };
    }

    private static string MapDetailStatusValue(int statusId) => statusId switch
    {
        1 => "pending",
        2 => "paid",
        3 => "shipping",
        4 => "completed",
        5 => "cancelled",
        6 => "refunding",
        7 => "refunded",
        _ => "unknown"
    };

    private async Task SyncDetailStatusAsync(long orderId, int statusId, CancellationToken ct)
    {
        var details = await _dbContext.CommodityOrderDetails
            .Where(x => x.OrderId == orderId)
            .ToListAsync(ct);
        foreach (var d in details)
        {
            d.StatusId = statusId;
        }
    }

    private static object BuildAggregatedDetailView(
        AggregatedOrder order,
        IReadOnlyDictionary<string, List<AggregatedItem>> itemMap,
        ShippingAddress? address)
    {
        itemMap.TryGetValue(order.OrderNo, out var items);
        items ??= [];

        var shippingAddress = address is null
            ? new { name = (string?)null, phone = (string?)null, address = (string?)null }
            : new { name = (string?)address.ContactName, phone = (string?)address.ContactPhone, address = (string?)$"{address.Province}{address.City}{address.MunicipalDistrict}{address.Addres}" };

        return new
        {
            order = new
            {
                id = order.OrderId.ToString(),
                orderId = order.OrderId.ToString(),
                orderNumber = order.OrderNo,
                orderNo = order.OrderNo,
                status = MapAggregateStatusValue(order.Type, order.RawStatusId),
                statusText = MapAggregateStatusText(order.Type, order.RawStatusId),
                orderStatusId = order.RawStatusId,
                createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                payTime = order.RawStatusId >= 2 ? order.CreateTime.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
                shippingTime = order.RawStatusId >= 3 ? order.CreateTime.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
                completeTime = order.RawStatusId >= 4 ? order.CreateTime.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
                totalPrice = order.TotalAmount,
                totalAmount = order.TotalAmount,
                totalQuantity = order.TotalQuantity,
                shippingAddress,
                verified = order.Type == "activity" && order.RawStatusId == 3,
                items = items.Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    price = x.Price,
                    quantity = x.Quantity,
                    image = x.Image,
                    statusId = x.StatusId,
                    status = MapDetailStatusValue(x.StatusId)
                }).ToList(),
                paymentMethod = order.RawStatusId >= 2 ? "wechat" : (string?)null,
                transactionId = order.RawStatusId >= 2 ? order.WxPayNo : null,
                remark = order.Remark
            }
        };
    }

    private static System.Linq.Expressions.Expression<Func<CommodityOrder, bool>> CommodityStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status == "all")
            return o => true;
        return status switch
        {
            "pending" => o => o.OrderStatusId == 1,
            "paid" => o => o.OrderStatusId == 2,
            "shipping" => o => o.OrderStatusId == 3,
            "completed" => o => o.OrderStatusId == 4,
            "cancelled" => o => o.OrderStatusId == 5,
            _ => o => true
        };
    }

    private static System.Linq.Expressions.Expression<Func<DishOrder, bool>> DishStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status == "all")
            return o => true;
        return status switch
        {
            "pending" => o => o.OrderStatusId == 1,
            "paid" => o => o.OrderStatusId == 2,
            "completed" => o => o.OrderStatusId == 3,
            "cancelled" => o => o.OrderStatusId == 4,
            _ => o => true
        };
    }

    private static System.Linq.Expressions.Expression<Func<ActivityOrder, bool>> ActivityStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status == "all")
            return o => true;
        return status switch
        {
            "pending" => o => o.OrderStatusId == 1,
            "paid" => o => o.OrderStatusId == 2,
            "completed" => o => o.OrderStatusId == 3,
            "cancelled" => o => o.OrderStatusId == 4,
            _ => o => true
        };
    }

    private static string MapAggregateStatusValue(string type, int rawStatusId)
    {
        return type switch
        {
            "goods" => MapCommodityStatusValue(rawStatusId),
            "food" => MapDishStatusValue(rawStatusId),
            "activity" => MapActivityStatusValue(rawStatusId),
            _ => "unknown"
        };
    }

    private static string MapAggregateStatusText(string type, int rawStatusId)
    {
        return type switch
        {
            "goods" => MapCommodityStatusText(rawStatusId),
            "food" => MapDishStatusText(rawStatusId),
            "activity" => MapActivityStatusText(rawStatusId),
            _ => "Unknown"
        };
    }

    private static string MapCommodityStatusValue(int statusId) => statusId switch
    {
        1 => "pending", 2 => "paid", 3 => "shipping", 4 => "completed", 5 => "cancelled", _ => "unknown"
    };

    private static string MapCommodityStatusText(int statusId) => statusId switch
    {
        1 => "Pending Payment", 2 => "Paid", 3 => "Shipping", 4 => "Completed", 5 => "Cancelled", _ => "Unknown"
    };

    private static string MapDishStatusValue(int statusId) => statusId switch
    {
        1 => "pending", 2 => "paid", 3 => "completed", 4 => "cancelled", _ => "unknown"
    };

    private static Dictionary<int, string>? _dishStatusCache;
    private static readonly object _dishStatusCacheLock = new();

    private async Task EnsureDishStatusCacheAsync()
    {
        if (_dishStatusCache != null) return;
        var statuses = await _dbContext.DishOrderStatuses.AsNoTracking().ToListAsync();
        lock (_dishStatusCacheLock)
        {
            _dishStatusCache ??= statuses.ToDictionary(x => x.OrderStatusId, x => x.StatusName);
        }
    }

    private static string MapDishStatusText(int statusId) =>
        _dishStatusCache?.TryGetValue(statusId, out var text) == true ? text : "未知";

    private static string MapActivityStatusValue(int statusId) => statusId switch
    {
        1 => "pending", 2 => "verify_pending", 3 => "verified", 4 => "cancelled", _ => "unknown"
    };

    private static string MapActivityStatusText(int statusId) => statusId switch
    {
        1 => "Pending Payment", 2 => "Pending Verification", 3 => "Verified", 4 => "Cancelled", _ => "Unknown"
    };

    // ---- Shared helpers ----

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

        return int.TryParse(userIdValue, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static string NormalizeOrderType(string? type)
    {
        var value = (type ?? "all").Trim().ToLowerInvariant();
        return value switch
        {
            "" or "all" => "all",
            "goods" or "cart" or "commodity" => "goods",
            "food" or "dish" => "food",
            "activity" => "activity",
            _ => value
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

    private List<NormalizedCreateItem> NormalizeRequestItems(CreateOrderRequest request)
    {
        var normalized = new List<NormalizedCreateItem>();

        foreach (var item in request.MergedItems ?? [])
        {
            var rawId = item.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawId))
            {
                rawId = item.IdAlias ?? string.Empty;
            }

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
                ActivityId = parsedId > 0 ? parsedId : 0,
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
                ActivityId = 0,
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

    private string NormalizeMediaUrl(string? media) => MediaUrlHelper.NormalizeFull(media, Request);

    private static string GenerateCommodityOrderNo()
    {
        return $"GOODS{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static string GenerateDishOrderNo()
    {
        return $"DISH{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static string GenerateActivityOrderNo()
    {
        return $"ACT{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
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

    private async Task UpdateTableOccupiedAsync(long diningTableId, CancellationToken cancellationToken)
    {
        var table = await _dbContext.DiningTables.FindAsync(new object[] { diningTableId }, cancellationToken);
        if (table != null)
        {
            table.TableStatusId = 2;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task TryFreeTableAsync(long diningTableId, CancellationToken cancellationToken)
    {
        var hasActiveOrders = await _dbContext.DishOrders
            .AnyAsync(x => x.DiningTableId == diningTableId
                && (x.OrderStatusId == 1 || x.OrderStatusId == 2), cancellationToken);

        if (!hasActiveOrders)
        {
            var table = await _dbContext.DiningTables.FindAsync(new object[] { diningTableId }, cancellationToken);
            if (table != null)
            {
                table.TableStatusId = 1;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public sealed class CreateOrderRequest
    {
        public int? UserId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int Quantity { get; set; }
        public int TableNumber { get; set; }
        public int TableId { get; set; }
        public int AddressId { get; set; }
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

        [JsonPropertyName("tableId")]
        public int? TableIdAlias { get; set; }

        [JsonPropertyName("addressId")]
        public int? AddressIdAlias { get; set; }

        [JsonPropertyName("total_price")]
        public decimal? TotalPriceAlias { get; set; }

        [JsonPropertyName("user_id")]
        public int? UserIdAlias { get; set; }

        [JsonPropertyName("address_info")]
        public CreateOrderAddress? AddressAlias { get; set; }

        [JsonPropertyName("item_list")]
        public List<CreateOrderItem>? ItemsAlias { get; set; }

        [JsonIgnore]
        public CreateOrderAddress? MergedAddress => Address ?? AddressAlias ?? (AddressId > 0 || AddressIdAlias > 0
            ? new CreateOrderAddress { AddressId = AddressId > 0 ? AddressId : AddressIdAlias ?? 0 }
            : null);

        [JsonIgnore]
        public List<CreateOrderItem>? MergedItems => Items ?? ItemsAlias;

        [JsonIgnore]
        public string MergedSourceType => string.IsNullOrWhiteSpace(SourceType) ? SourceTypeAlias ?? string.Empty : SourceType;

        [JsonIgnore]
        public string MergedSourceName => string.IsNullOrWhiteSpace(SourceName) ? SourceNameAlias ?? string.Empty : SourceName;

        [JsonIgnore]
        public int MergedSourceId => SourceId > 0 ? SourceId : SourceIdAlias ?? 0;

        [JsonIgnore]
        public int MergedTableNumber => TableNumber > 0 ? TableNumber : TableNumberAlias ?? (TableId > 0 ? TableId : TableIdAlias ?? 0);

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
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("item_id")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
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
        public int ActivityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;
    }

    private sealed class AggregatedItem
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public int Quantity { get; init; }
        public string Image { get; init; } = string.Empty;
        public int StatusId { get; init; }
    }

}
