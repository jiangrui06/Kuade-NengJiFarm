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
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private static Dictionary<int, string>? _dishStatusCache;
    private static readonly object _dishStatusCacheLock = new();
    private static Dictionary<int, string>? _commodityStatusTextCache;
    private static readonly object _commodityStatusCacheLock = new();
    private static Dictionary<int, string>? _activityStatusTextCache;
    private static readonly object _activityStatusCacheLock = new();

    public OrdersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private async Task EnsureDishStatusCacheAsync()
    {
        if (_dishStatusCache != null) return;
        var statuses = await _dbContext.DishOrderStatuses.AsNoTracking().ToListAsync();
        lock (_dishStatusCacheLock)
        {
            _dishStatusCache ??= statuses.ToDictionary(x => x.OrderStatusId, x => x.StatusName);
        }
    }

    private async Task EnsureCommodityStatusCacheAsync()
    {
        if (_commodityStatusTextCache != null) return;
        var json = await _dbContext.SysConfigs
            .AsNoTracking()
            .Where(x => x.ConfigKey == "commodity_order_status_names")
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            lock (_commodityStatusCacheLock)
            {
                _commodityStatusTextCache ??= parsed;
            }
        }
        catch { }
    }

    private async Task EnsureActivityStatusCacheAsync()
    {
        if (_activityStatusTextCache != null) return;
        var json = await _dbContext.SysConfigs
            .AsNoTracking()
            .Where(x => x.ConfigKey == "activity_order_status_names")
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            lock (_activityStatusCacheLock)
            {
                _activityStatusTextCache ??= parsed;
            }
        }
        catch { }
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        await EnsureDishStatusCacheAsync();
        await EnsureCommodityStatusCacheAsync();
        await EnsureActivityStatusCacheAsync();
        var userId = ResolveCurrentUserId();
        var normalizedType = NormalizeOrderType(type);
        var normalizedStatus = NormalizeStatus(status);
        var take = page * pageSize;

        var total = await CountAggregatedAsync(userId, normalizedType, normalizedStatus, keyword, cancellationToken);
        var slice = await LoadAggregatedSliceAsync(userId, normalizedType, normalizedStatus, keyword, take, cancellationToken);
        slice.Sort((a, b) => b.CreateTime.CompareTo(a.CreateTime));
        var pageSlice = slice.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var itemMap = await LoadItemsAsync(pageSlice, cancellationToken);

        var diningTableIds = pageSlice
            .Where(x => x.Type == "food" && x.DiningTableId > 0)
            .Select(x => x.DiningTableId)
            .Distinct()
            .ToList();
        var diningTableMap = diningTableIds.Count == 0
            ? new Dictionary<long, string>()
            : (await _dbContext.DiningTables.AsNoTracking()
                .Where(x => diningTableIds.Contains(x.DiningTableId))
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.DiningTableId, x => FormatDiningTableNo(x.TableNo));

        var activeRefundOrderIds = await LoadActiveRefundOrderIdsAsync(pageSlice, userId, cancellationToken);
        var refundStatusMap = await LoadRefundStatusMapAsync(pageSlice, userId, cancellationToken);
        var orders = pageSlice.Select(x => BuildOrderSummary(x, itemMap, diningTableMap, activeRefundOrderIds, refundStatusMap)).ToList();

        return Ok(ApiResult.Success(new
        {
            orders,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        }));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Ok(ApiResult.Fail("搜索关键词不能为空", 400));

        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var userId = ResolveCurrentUserId();
        var normalizedType = NormalizeOrderType(type ?? "all");
        var normalizedStatus = NormalizeStatus(status ?? "all");
        var value = keyword.Trim();
        var take = page * pageSize;

        var total = await CountSearchAggregatedAsync(userId, normalizedType, normalizedStatus, value, cancellationToken);
        var slice = await LoadSearchAggregatedSliceAsync(userId, normalizedType, normalizedStatus, value, take, cancellationToken);
        slice.Sort((a, b) => b.CreateTime.CompareTo(a.CreateTime));
        var pageSlice = slice.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var itemMap = await LoadItemsAsync(pageSlice, cancellationToken);

        var diningTableIds = pageSlice
            .Where(x => x.Type == "food" && x.DiningTableId > 0)
            .Select(x => x.DiningTableId)
            .Distinct()
            .ToList();
        var diningTableMap = diningTableIds.Count == 0
            ? new Dictionary<long, string>()
            : (await _dbContext.DiningTables.AsNoTracking()
                .Where(x => diningTableIds.Contains(x.DiningTableId))
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.DiningTableId, x => FormatDiningTableNo(x.TableNo));

        var activeRefundOrderIds = await LoadActiveRefundOrderIdsAsync(pageSlice, userId, cancellationToken);
        var refundStatusMap = await LoadRefundStatusMapAsync(pageSlice, userId, cancellationToken);
        var list = pageSlice.Select(x => BuildOrderSummary(x, itemMap, diningTableMap, activeRefundOrderIds, refundStatusMap)).ToList();

        return Ok(ApiResult.Success(new { list, total, page, pageSize }));
    }

    [HttpGet("counts")]
    public async Task<IActionResult> Counts([FromQuery] string? type, CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        var normalizedType = NormalizeOrderType(type);

        var pending = await CountAggregatedAsync(userId, normalizedType, "pending", null, cancellationToken);
        var paid = await CountAggregatedAsync(userId, normalizedType, "paid", null, cancellationToken);
        var shipping = await CountAggregatedAsync(userId, normalizedType, "shipping", null, cancellationToken);
        var completed = await CountAggregatedAsync(userId, normalizedType, "completed", null, cancellationToken);
        var cancelled = await CountAggregatedAsync(userId, normalizedType, "cancelled", null, cancellationToken);

        return Ok(ApiResult.Success(new { pending, paid, shipping, completed, cancelled }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        await EnsureDishStatusCacheAsync();
        await EnsureCommodityStatusCacheAsync();
        await EnsureActivityStatusCacheAsync();
        var order = await FindOrderAsync(id, userId, tracking: false, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        var items = await LoadItemsAsync([order], cancellationToken);
        var address = order.Type == "goods"
            ? await _dbContext.ShippingAddresses.AsNoTracking().FirstOrDefaultAsync(
                x => x.UserId == userId && x.AddressId == order.AddressId,
                cancellationToken)
            : null;

        var shippingAddressText = address is null
            ? string.Empty
            : $"{address.Province}{address.City}{address.MunicipalDistrict}{address.Addres}";

        var shippingAddress = new
        {
            name = address?.ContactName ?? string.Empty,
            phone = address?.ContactPhone ?? string.Empty,
            address = shippingAddressText
        };

        var diningTableNo = order.Type == "food" && order.DiningTableId > 0
            ? FormatDiningTableNo(await _dbContext.DiningTables.AsNoTracking()
                .Where(x => x.DiningTableId == order.DiningTableId)
                .Select(x => x.TableNo)
                .FirstOrDefaultAsync(cancellationToken))
            : null;

        object logistics = order.Type == "goods" && (order.RawStatusId == 3 || order.RawStatusId == 4)
            ? GenerateOrderDetailLogistics(order)
            : Array.Empty<object>();

        // 活动订单加载有效期信息（有效期：当天起至当年10月1日）
        object? validity = null;
        if (order.Type == "activity")
        {
            var now = DateTime.Now;
            var endDate = new DateTime(now.Year, 10, 1, 23, 59, 59);
            validity = new
            {
                startTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                isValid = now <= endDate,
                expired = now > endDate
            };
        }

        // 退款信息
        var refundRecord = await _dbContext.RefundRecords
            .AsNoTracking()
            .Where(x => x.OrderId == order.OrderId && x.UserId == userId)
            .OrderByDescending(x => x.CreateTime)
            .FirstOrDefaultAsync(cancellationToken);

        var hasRefund = refundRecord is not null;
        var refundStatus = refundRecord?.Status switch
        {
            "pending" or "approved" or "processing" => "refunding",
            "completed" => "refunded",
            _ => (string?)null
        };

        var payload = BuildOrderDetail(order, items, shippingAddress, diningTableNo, logistics, validity, hasRefund, refundStatus);
        return Ok(ApiResult.Success(payload));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateOrderStatusRequest? request, CancellationToken cancellationToken = default)
    {
        var targetStatus = NormalizeStatus(request?.Status);
        if (string.IsNullOrWhiteSpace(targetStatus) || targetStatus == "all")
        {
            return Ok(ApiResult.Fail("status 参数不正确", 400));
        }

        var userId = ResolveCurrentUserId();
        var order = await FindOrderAsync(id, userId, tracking: true, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        var (ok, message) = await ApplyStatusTransitionAsync(order, targetStatus, cancellationToken);
        if (!ok)
        {
            return Ok(ApiResult.Fail(message, 409));
        }

        return Ok(ApiResult.Success(new
        {
            orderId = order.OrderNo,
            id = order.OrderNo,
            status = order.Status,
            statusText = order.StatusText,
            updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        }));
    }

    [HttpPost("{id}/mock-pay")]
    public async Task<IActionResult> MockPay(string id, [FromBody] MockPayRequest? request, CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        var order = await FindOrderAsync(id, userId, tracking: true, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        if (!string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(ApiResult.Success(new { orderId = order.OrderNo, status = order.Status }, "订单无需支付"));
        }

        await MarkPaidAsync(order, $"MOCK_{DateTime.Now:yyyyMMddHHmmssfff}", cancellationToken);

        return Ok(ApiResult.Success(new
        {
            orderId = order.OrderNo,
            id = order.OrderNo,
            status = order.Status,
            statusText = order.StatusText
        }, "支付成功"));
    }

    [HttpGet("{id}/qrcode")]
    public async Task<IActionResult> QrCode(string id, CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        await EnsureDishStatusCacheAsync();
        await EnsureCommodityStatusCacheAsync();
        await EnsureActivityStatusCacheAsync();
        var order = await FindOrderAsync(id, userId, tracking: false, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404));
        }

        if (order.Type != "activity")
        {
            return Ok(ApiResult.Fail("只有活动订单支持核销码", 400));
        }

        // 只有待核销状态才能生成二维码
        if (order.RawStatusId == 1)
        {
            return Ok(ApiResult.Fail("该券未支付，无法核销", 403));
        }

        if (order.RawStatusId == 3)
        {
            return Ok(ApiResult.Fail("该券已被使用，不能重复核销", 409));
        }

        if (order.RawStatusId == 4)
        {
            return Ok(ApiResult.Fail("该券已取消，无法核销", 403));
        }

        // 查找订单详情，获取或生成核销码
        var detail = await _dbContext.ActivityOrderDetails
            .FirstOrDefaultAsync(x => x.ActivityOrderId == order.OrderId, cancellationToken);

        if (detail is null)
        {
            return Ok(ApiResult.Fail("未找到该券详情信息", 404));
        }

        // 如果尚未生成核销码则生成并存储
        if (string.IsNullOrWhiteSpace(detail.ActivityQrcode))
        {
            detail.ActivityQrcode = GenerateVoucherCode();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var code = detail.ActivityQrcode;
        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=320x320&data={Uri.EscapeDataString(code)}";

        return Ok(ApiResult.Success(new
        {
            qrCodeUrl,
            code
        }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        var order = await FindOrderAsync(id, userId, tracking: true, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        if (order.IsCancelled != true)
        {
            return Ok(ApiResult.Fail("仅支持删除已取消订单", 409));
        }

        await DeleteOrderAsync(order, cancellationToken);
        return Ok(ApiResult.Success(new { orderId = order.OrderNo, id = order.OrderNo, deleted = true }));
    }

    private int ResolveCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static string NormalizeOrderType(string? type)
    {
        var value = (type ?? "all").Trim().ToLowerInvariant();
        return value switch
        {
            "" => "all",
            "all" => "all",
            "goods" => "goods",
            "cart" => "goods",
            "commodity" => "goods",
            "food" => "food",
            "dish" => "food",
            "activity" => "activity",
            _ => value
        };
    }

    private static string NormalizeStatus(string? status)
    {
        var value = (status ?? "all").Trim().ToLowerInvariant();
        return value switch
        {
            "" => "all",
            "all" => "all",
            "pending_payment" => "pending",
            "delivered" => "shipped",
            _ => value
        };
    }

    private async Task<long> CountAggregatedAsync(
        int userId,
        string type,
        string status,
        string? keyword,
        CancellationToken cancellationToken)
    {
        long total = 0;

        if (type is "all" or "goods")
        {
            var q = _dbContext.CommodityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyCommodityStatusFilter(q, status);
            q = ApplyKeywordFilter(q, keyword);
            total += await q.CountAsync(cancellationToken);
        }

        if (type is "all" or "food")
        {
            var q = _dbContext.DishOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyDishStatusFilter(q, status);
            q = ApplyKeywordFilter(q, keyword);
            total += await q.CountAsync(cancellationToken);
        }

        if (type is "all" or "activity")
        {
            var q = _dbContext.ActivityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyActivityStatusFilter(q, status);
            q = ApplyKeywordFilter(q, keyword);
            total += await q.CountAsync(cancellationToken);
        }

        return total;
    }

    private async Task<List<OrderKey>> LoadAggregatedSliceAsync(
        int userId,
        string type,
        string status,
        string? keyword,
        int take,
        CancellationToken cancellationToken)
    {
        var result = new List<OrderKey>();

        if (type is "all" or "goods")
        {
            var q = _dbContext.CommodityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyCommodityStatusFilter(q, status);
            q = ApplyKeywordFilter(q, keyword);
            var rows = await q.OrderByDescending(x => x.CreateTime).Take(take).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(OrderKey.FromCommodity));
        }

        if (type is "all" or "food")
        {
            var q = _dbContext.DishOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyDishStatusFilter(q, status);
            q = ApplyKeywordFilter(q, keyword);
            var rows = await q.OrderByDescending(x => x.CreateTime).Take(take).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(OrderKey.FromDish));
        }

        if (type is "all" or "activity")
        {
            var q = _dbContext.ActivityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyActivityStatusFilter(q, status);
            q = ApplyKeywordFilter(q, keyword);
            var rows = await q.OrderByDescending(x => x.CreateTime).Take(take).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(OrderKey.FromActivity));
        }

        return result;
    }

    private async Task<long> CountSearchAggregatedAsync(
        int userId,
        string type,
        string status,
        string keyword,
        CancellationToken cancellationToken)
    {
        long total = 0;
        var value = keyword.Trim();

        if (type is "all" or "goods")
        {
            var q = _dbContext.CommodityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyCommodityStatusFilter(q, status);
            q = ApplySearchKeywordFilter(q, value);
            total += await q.CountAsync(cancellationToken);
        }

        if (type is "all" or "food")
        {
            var q = _dbContext.DishOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyDishStatusFilter(q, status);
            q = ApplySearchKeywordFilter(q, value);
            total += await q.CountAsync(cancellationToken);
        }

        if (type is "all" or "activity")
        {
            var q = _dbContext.ActivityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyActivityStatusFilter(q, status);
            q = ApplySearchKeywordFilter(q, value);
            total += await q.CountAsync(cancellationToken);
        }

        return total;
    }

    private async Task<List<OrderKey>> LoadSearchAggregatedSliceAsync(
        int userId,
        string type,
        string status,
        string keyword,
        int take,
        CancellationToken cancellationToken)
    {
        var result = new List<OrderKey>();
        var value = keyword.Trim();

        if (type is "all" or "goods")
        {
            var q = _dbContext.CommodityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyCommodityStatusFilter(q, status);
            q = ApplySearchKeywordFilter(q, value);
            var rows = await q.OrderByDescending(x => x.CreateTime).Take(take).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(OrderKey.FromCommodity));
        }

        if (type is "all" or "food")
        {
            var q = _dbContext.DishOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyDishStatusFilter(q, status);
            q = ApplySearchKeywordFilter(q, value);
            var rows = await q.OrderByDescending(x => x.CreateTime).Take(take).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(OrderKey.FromDish));
        }

        if (type is "all" or "activity")
        {
            var q = _dbContext.ActivityOrders.AsNoTracking().Where(x => x.UserId == userId);
            q = ApplyActivityStatusFilter(q, status);
            q = ApplySearchKeywordFilter(q, value);
            var rows = await q.OrderByDescending(x => x.CreateTime).Take(take).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(OrderKey.FromActivity));
        }

        return result;
    }

    private IQueryable<CommodityOrder> ApplySearchKeywordFilter(IQueryable<CommodityOrder> query, string value)
    {
        var matchedIds = _dbContext.CommodityOrderDetails
            .Where(d => _dbContext.Commodities
                .Where(c => c.ProductName.Contains(value))
                .Select(c => c.CommodityId)
                .Contains(d.CommodityId))
            .Select(d => d.OrderId);
        return query.Where(x => x.OrderNo.Contains(value) || matchedIds.Contains(x.OrderId));
    }

    private IQueryable<DishOrder> ApplySearchKeywordFilter(IQueryable<DishOrder> query, string value)
    {
        var matchedIds = _dbContext.DishOrderDetails
            .Where(d => _dbContext.Dishes
                .Where(dish => dish.DishName.Contains(value))
                .Select(dish => dish.DishId)
                .Contains(d.DishId))
            .Select(d => d.DishOrderId);
        return query.Where(x => x.OrderNo.Contains(value) || matchedIds.Contains(x.OrderId));
    }

    private IQueryable<ActivityOrder> ApplySearchKeywordFilter(IQueryable<ActivityOrder> query, string value)
    {
        var matchedIds = _dbContext.ActivityOrderDetails
            .Where(d => _dbContext.Activities
                .Where(a => a.Title.Contains(value))
                .Select(a => a.ActivityId)
                .Contains(d.ActivityId))
            .Select(d => d.ActivityOrderId);
        return query.Where(x => x.OrderNo.Contains(value) || matchedIds.Contains(x.OrderId));
    }

    private static IQueryable<CommodityOrder> ApplyKeywordFilter(IQueryable<CommodityOrder> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var value = keyword.Trim();
        return query.Where(x => x.OrderNo.Contains(value));
    }

    private static IQueryable<DishOrder> ApplyKeywordFilter(IQueryable<DishOrder> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var value = keyword.Trim();
        return query.Where(x => x.OrderNo.Contains(value));
    }

    private static IQueryable<ActivityOrder> ApplyKeywordFilter(IQueryable<ActivityOrder> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var value = keyword.Trim();
        return query.Where(x => x.OrderNo.Contains(value));
    }

    private static IQueryable<CommodityOrder> ApplyCommodityStatusFilter(IQueryable<CommodityOrder> query, string status)
    {
        // 逗号分隔多状态：如 "refunding,refunded" → 同时查 status 6 和 7
        if (status.Contains(','))
        {
            var ids = ParseCommodityStatusIds(status);
            return ids.Count > 0 ? query.Where(x => ids.Contains(x.OrderStatusId)) : query.Where(x => false);
        }

        return status switch
        {
            "all" => query,
            "pending" => query.Where(x => x.OrderStatusId == 1),
            "paid" => query.Where(x => x.OrderStatusId == 2),
            "shipping" => query.Where(x => x.OrderStatusId == 3),
            "completed" => query.Where(x => x.OrderStatusId == 4),
            "cancelled" => query.Where(x => x.OrderStatusId == 5),
            "refunding" => query.Where(x => x.OrderStatusId == 6),
            "refunded" => query.Where(x => x.OrderStatusId == 7),
            _ => query.Where(x => false)
        };
    }

    private static IQueryable<DishOrder> ApplyDishStatusFilter(IQueryable<DishOrder> query, string status)
    {
        if (status.Contains(','))
        {
            var ids = ParseDishStatusIds(status);
            return ids.Count > 0 ? query.Where(x => ids.Contains(x.OrderStatusId)) : query.Where(x => false);
        }

        return status switch
        {
            "all" => query,
            "pending" => query.Where(x => x.OrderStatusId == 1),
            "paid" => query.Where(x => x.OrderStatusId == 2),
            "completed" => query.Where(x => x.OrderStatusId == 3),
            "cancelled" => query.Where(x => x.OrderStatusId == 4),
            "refunding" => query.Where(x => x.OrderStatusId == 5),
            "refunded" => query.Where(x => x.OrderStatusId == 6),
            "shipping" => query.Where(x => false),
            _ => query.Where(x => false)
        };
    }

    private static IQueryable<ActivityOrder> ApplyActivityStatusFilter(IQueryable<ActivityOrder> query, string status)
    {
        if (status.Contains(','))
        {
            var ids = ParseActivityStatusIds(status);
            return ids.Count > 0 ? query.Where(x => ids.Contains(x.OrderStatusId)) : query.Where(x => false);
        }

        return status switch
        {
            "all" => query,
            "pending" => query.Where(x => x.OrderStatusId == 1),
            "paid" => query.Where(x => x.OrderStatusId == 2),
            "completed" => query.Where(x => x.OrderStatusId == 3),
            "cancelled" => query.Where(x => x.OrderStatusId == 4),
            "refunding" => query.Where(x => x.OrderStatusId == 5),
            "refunded" => query.Where(x => x.OrderStatusId == 6),
            "shipping" => query.Where(x => false),
            _ => query.Where(x => false)
        };
    }

    private static List<int> ParseCommodityStatusIds(string status)
    {
        return status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(s => s switch
            {
                "pending" => new[] { 1 },
                "paid" => new[] { 2 },
                "shipping" => new[] { 3 },
                "completed" => new[] { 4 },
                "cancelled" => new[] { 5 },
                "refunding" => new[] { 6 },
                "refunded" => new[] { 7 },
                _ => []
            })
            .Distinct()
            .ToList();
    }

    private static List<int> ParseDishStatusIds(string status)
    {
        return status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(s => s switch
            {
                "pending" => new[] { 1 },
                "paid" => new[] { 2 },
                "completed" => new[] { 3 },
                "cancelled" => new[] { 4 },
                "refunding" => new[] { 5 },
                "refunded" => new[] { 6 },
                _ => []
            })
            .Distinct()
            .ToList();
    }

    private static List<int> ParseActivityStatusIds(string status)
    {
        return status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(s => s switch
            {
                "pending" => new[] { 1 },
                "paid" => new[] { 2 },
                "completed" => new[] { 3 },
                "cancelled" => new[] { 4 },
                "refunding" => new[] { 5 },
                "refunded" => new[] { 6 },
                _ => []
            })
            .Distinct()
            .ToList();
    }

    private async Task<Dictionary<string, List<OrderItem>>> LoadItemsAsync(IReadOnlyCollection<OrderKey> orders, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<OrderItem>>(StringComparer.OrdinalIgnoreCase);
        if (orders.Count == 0)
        {
            return result;
        }

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
                    commodityMap.TryGetValue(d.CommodityId, out var commodity);
                    return new OrderItem
                    {
                        Id = d.CommodityId.ToString(),
                        Name = commodity?.ProductName ?? $"商品{d.CommodityId}",
                        Price = d.UnitPrice,
                        Quantity = d.Quantity,
                        Image = NormalizeMediaUrl(commodity?.ImageUrl)
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
                    return new OrderItem
                    {
                        Id = d.DishId.ToString(),
                        Name = dish?.DishName ?? $"菜品{d.DishId}",
                        Price = d.UnitPrice,
                        Quantity = d.Quantity,
                        Image = NormalizeMediaUrl(dish?.ImageUrl)
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
            var activityIds = details.Select(x => (int)x.ActivityId).Distinct().ToList();
            var activityMap = activityIds.Count == 0
                ? new Dictionary<int, ActivityEntity>()
                : await _dbContext.Activities.AsNoTracking()
                    .Where(x => activityIds.Contains((int)x.ActivityId))
                    .ToDictionaryAsync(x => (int)x.ActivityId, cancellationToken);

            foreach (var group in details.GroupBy(x => x.ActivityOrderId))
            {
                var orderNo = activity.First(o => o.OrderId == group.Key).OrderNo;
                result[orderNo] = group.Select(d =>
                {
                    activityMap.TryGetValue((int)d.ActivityId, out var a);
                    return new OrderItem
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

    private static object BuildOrderSummary(OrderKey order, IReadOnlyDictionary<string, List<OrderItem>> itemMap, IReadOnlyDictionary<long, string>? diningTableMap = null, HashSet<long>? activeRefundOrderIds = null, Dictionary<long, string>? refundStatusMap = null)
    {
        itemMap.TryGetValue(order.OrderNo, out var items);
        items ??= [];

        var diningTableNo = order.Type == "food" && order.DiningTableId > 0
            ? diningTableMap?.GetValueOrDefault(order.DiningTableId)
            : null;

        return new
        {
            id = order.OrderId,
            orderId = order.OrderId,
            orderNumber = order.OrderNo,
            orderNo = order.OrderNo,
            type = order.Type,
            typeText = order.TypeText,
            status = order.Status,
            statusText = order.StatusText,
            orderStatusId = order.RawStatusId,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            transactionId = order.WxPayNo,
            diningTableNo,
            items = items.Select(x => new { id = x.Id, name = x.Name, price = x.Price, quantity = x.Quantity, image = x.Image }).ToList(),
            remark = order.Remark,
            verified = order.Type == "activity" && order.RawStatusId == 3,
            hasRefund = activeRefundOrderIds?.Contains(order.OrderId) ?? false,
            refundStatus = refundStatusMap?.GetValueOrDefault(order.OrderId)
        };
    }

    private static object BuildOrderDetail(OrderKey order, IReadOnlyDictionary<string, List<OrderItem>> itemMap, object shippingAddress, string? diningTableNo = null, object? logistics = null, object? validity = null, bool hasRefund = false, string? refundStatus = null)
    {
        itemMap.TryGetValue(order.OrderNo, out var items);
        items ??= [];

        return new
        {
            id = order.OrderId,
            orderId = order.OrderId,
            orderNumber = order.OrderNo,
            orderNo = order.OrderNo,
            type = order.Type,
            typeText = order.TypeText,
            status = order.Status,
            statusText = order.StatusText,
            orderStatusId = order.RawStatusId,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            paymentTime = (string?)null,
            shippingTime = (string?)null,
            completeTime = (string?)null,
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            totalQuantity = order.TotalQuantity,
            shippingAddress,
            items = items.Select(x => new { id = x.Id, name = x.Name, price = x.Price, quantity = x.Quantity, image = x.Image }).ToList(),
            paymentMethod = (string?)null,
            transactionId = order.WxPayNo,
            diningTableNo,
            remark = order.Remark,
            logistics = logistics ?? Array.Empty<object>(),
            validity,
            verified = order.Type == "activity" && order.RawStatusId == 3,
            hasRefund,
            refundStatus
        };
    }

    private async Task<OrderKey?> FindOrderAsync(string id, int userId, bool tracking, CancellationToken cancellationToken)
    {
        var raw = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var numeric = long.TryParse(raw, out var orderId) && orderId > 0;

        if (tracking)
        {
            if (numeric)
            {
                var goods = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, cancellationToken);
                if (goods is not null) return OrderKey.FromCommodity(goods, goods);
                var food = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, cancellationToken);
                if (food is not null) return OrderKey.FromDish(food, food);
                var act = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, cancellationToken);
                if (act is not null) return OrderKey.FromActivity(act, act);
            }
            else
            {
                var goods = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == raw, cancellationToken);
                if (goods is not null) return OrderKey.FromCommodity(goods, goods);
                var food = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == raw, cancellationToken);
                if (food is not null) return OrderKey.FromDish(food, food);
                var act = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == raw, cancellationToken);
                if (act is not null) return OrderKey.FromActivity(act, act);
            }

            return null;
        }

        if (numeric)
        {
            var goods = await _dbContext.CommodityOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, cancellationToken);
            if (goods is not null) return OrderKey.FromCommodity(goods);
            var food = await _dbContext.DishOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, cancellationToken);
            if (food is not null) return OrderKey.FromDish(food);
            var act = await _dbContext.ActivityOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, cancellationToken);
            if (act is not null) return OrderKey.FromActivity(act);
        }
        else
        {
            var goods = await _dbContext.CommodityOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == raw, cancellationToken);
            if (goods is not null) return OrderKey.FromCommodity(goods);
            var food = await _dbContext.DishOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == raw, cancellationToken);
            if (food is not null) return OrderKey.FromDish(food);
            var act = await _dbContext.ActivityOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == raw, cancellationToken);
            if (act is not null) return OrderKey.FromActivity(act);
        }

        return null;
    }

    private async Task<(bool ok, string message)> ApplyStatusTransitionAsync(OrderKey order, string targetStatus, CancellationToken cancellationToken)
    {
        if (order.Type == "goods")
        {
            var entity = (CommodityOrder)order.TrackingEntity!;
            if (targetStatus == "cancelled")
            {
                if (entity.OrderStatusId != 1) return (false, "当前状态不可取消");
                await RestoreCommodityStockAsync(entity.OrderId, cancellationToken);
                entity.OrderStatusId = 5;
            }
            else if (targetStatus == "shipped")
            {
                if (entity.OrderStatusId != 2) return (false, "当前状态不可发货");
                entity.OrderStatusId = 3;
                entity.TrackingNumber = $"EMS{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}";
                entity.TrackingTypeId = 2;
            }
            else if (targetStatus == "completed")
            {
                if (entity.OrderStatusId != 3 && entity.OrderStatusId != 2) return (false, "当前状态不可确认收货");
                entity.OrderStatusId = 4;
            }
            else
            {
                return (false, "不支持的状态更新");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            order.RefreshFrom(entity);
            return (true, "ok");
        }

        if (order.Type == "food")
        {
            var entity = (DishOrder)order.TrackingEntity!;
            if (targetStatus == "cancelled")
            {
                if (entity.OrderStatusId != 1) return (false, "当前状态不可取消");
                entity.OrderStatusId = 4;
            }
            else if (targetStatus == "completed")
            {
                if (entity.OrderStatusId != 2) return (false, "当前状态不可完成");
                entity.OrderStatusId = 3;
            }
            else
            {
                return (false, "不支持的状态更新");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            order.RefreshFrom(entity);
            return (true, "ok");
        }

        if (order.Type == "activity")
        {
            var entity = (ActivityOrder)order.TrackingEntity!;
            if (targetStatus == "cancelled")
            {
                if (entity.OrderStatusId != 1) return (false, "当前状态不可取消");
                entity.OrderStatusId = 4;
            }
            else
            {
                return (false, "不支持的状态更新");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            order.RefreshFrom(entity);
            return (true, "ok");
        }

        return (false, "订单类型不支持");
    }

    private async Task MarkPaidAsync(OrderKey order, string wxPayNo, CancellationToken cancellationToken)
    {
        if (order.Type == "goods")
        {
            var entity = (CommodityOrder)order.TrackingEntity!;
            if (entity.OrderStatusId == 1)
            {
                entity.OrderStatusId = 2;
                entity.WxPayNo = wxPayNo;
                await _dbContext.SaveChangesAsync(cancellationToken);
                order.RefreshFrom(entity);
            }

            return;
        }

        if (order.Type == "food")
        {
            var entity = (DishOrder)order.TrackingEntity!;
            if (entity.OrderStatusId == 1)
            {
                entity.OrderStatusId = 2;
                entity.WxPayNo = wxPayNo;
                await _dbContext.SaveChangesAsync(cancellationToken);
                order.RefreshFrom(entity);
            }

            return;
        }

        if (order.Type == "activity")
        {
            var entity = (ActivityOrder)order.TrackingEntity!;
            if (entity.OrderStatusId == 1)
            {
                entity.OrderStatusId = 2;
                entity.WxPayNo = wxPayNo;
                await _dbContext.SaveChangesAsync(cancellationToken);
                order.RefreshFrom(entity);
            }
        }
    }

    private async Task DeleteOrderAsync(OrderKey order, CancellationToken cancellationToken)
    {
        if (order.Type == "goods")
        {
            _dbContext.CommodityOrders.Remove((CommodityOrder)order.TrackingEntity!);
        }
        else if (order.Type == "food")
        {
            _dbContext.DishOrders.Remove((DishOrder)order.TrackingEntity!);
        }
        else if (order.Type == "activity")
        {
            _dbContext.ActivityOrders.Remove((ActivityOrder)order.TrackingEntity!);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public sealed class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public sealed class MockPayRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
    }

    private sealed class OrderKey
    {
        public string Type { get; init; } = string.Empty;
        public string TypeText { get; init; } = string.Empty;
        public long OrderId { get; init; }
        public string OrderNo { get; init; } = string.Empty;
        public DateTime CreateTime { get; init; }
        public decimal TotalAmount { get; init; }
        public int TotalQuantity { get; init; }
        public int RawStatusId { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public string StatusText { get; private set; } = string.Empty;
        public string? WxPayNo { get; private set; }
        public long AddressId { get; private set; }
        public object? TrackingEntity { get; private set; }
        public bool? IsCancelled { get; private set; }
        public long DiningTableId { get; private set; }
        public string? Remark { get; private set; }

        public static OrderKey FromCommodity(CommodityOrder order) => FromCommodity(order, null);

        public static OrderKey FromCommodity(CommodityOrder order, CommodityOrder? trackingEntity)
        {
            var (status, text, cancelled) = MapCommodityStatus(order.OrderStatusId);
            return new OrderKey
            {
                Type = "goods",
                TypeText = "商品订单",
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                CreateTime = order.CreateTime,
                TotalAmount = order.TotalAmount,
                TotalQuantity = order.TotalQuantity,
                RawStatusId = order.OrderStatusId,
                Status = status,
                StatusText = text,
                WxPayNo = order.WxPayNo,
                AddressId = order.AddressId,
                TrackingEntity = trackingEntity,
                IsCancelled = cancelled
            };
        }

        public static OrderKey FromDish(DishOrder order) => FromDish(order, null);

        public static OrderKey FromDish(DishOrder order, DishOrder? trackingEntity)
        {
            var (status, text, cancelled) = MapDishStatus(order.OrderStatusId);
            return new OrderKey
            {
                Type = "food",
                TypeText = "点餐订单",
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                CreateTime = order.CreateTime,
                TotalAmount = order.TotalAmount,
                TotalQuantity = order.TotalQuantity,
                RawStatusId = order.OrderStatusId,
                Status = status,
                StatusText = text,
                WxPayNo = order.WxPayNo,
                TrackingEntity = trackingEntity,
                IsCancelled = cancelled,
                DiningTableId = order.DiningTableId,
                Remark = order.Remark
            };
        }

        public static OrderKey FromActivity(ActivityOrder order) => FromActivity(order, null);

        public static OrderKey FromActivity(ActivityOrder order, ActivityOrder? trackingEntity)
        {
            var (status, text, cancelled) = MapActivityStatus(order.OrderStatusId);
            return new OrderKey
            {
                Type = "activity",
                TypeText = "活动订单",
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                CreateTime = order.CreateTime,
                TotalAmount = order.TotalAmount,
                TotalQuantity = order.TotalQuantity,
                RawStatusId = order.OrderStatusId,
                Status = status,
                StatusText = text,
                WxPayNo = order.WxPayNo,
                TrackingEntity = trackingEntity,
                IsCancelled = cancelled
            };
        }

        public void RefreshFrom(object entity)
        {
            TrackingEntity = entity;
            switch (entity)
            {
                case CommodityOrder goods:
                    RawStatusId = goods.OrderStatusId;
                    (Status, StatusText, IsCancelled) = MapCommodityStatus(goods.OrderStatusId);
                    WxPayNo = goods.WxPayNo;
                    AddressId = goods.AddressId;
                    break;
                case DishOrder food:
                    RawStatusId = food.OrderStatusId;
                    (Status, StatusText, IsCancelled) = MapDishStatus(food.OrderStatusId);
                    WxPayNo = food.WxPayNo;
                    DiningTableId = food.DiningTableId;
                    break;
                case ActivityOrder activity:
                    RawStatusId = activity.OrderStatusId;
                    (Status, StatusText, IsCancelled) = MapActivityStatus(activity.OrderStatusId);
                    WxPayNo = activity.WxPayNo;
                    break;
            }
        }

        private static (string status, string text, bool cancelled) MapCommodityStatus(int id)
        {
            var text = _commodityStatusTextCache?.TryGetValue(id, out var t) == true ? t : "未知";
            return id switch
            {
                1 => ("pending", text, false),
                2 => ("paid", text, false),
                3 => ("shipping", text, false),
                4 => ("completed", text, false),
                5 => ("cancelled", text, true),
                6 => ("refunding", text, false),
                7 => ("refunded", text, false),
                _ => ("unknown", text, false)
            };
        }

        private static (string status, string text, bool cancelled) MapDishStatus(int id)
        {
            var text = _dishStatusCache?.TryGetValue(id, out var t) == true ? t : "未知";
            var status = id switch
            {
                1 => "pending", 2 => "paid", 3 => "completed", 4 => "cancelled",
                5 => "refunding", 6 => "refunded",
                _ => "unknown"
            };
            return (status, text, id == 4);
        }

        private static (string status, string text, bool cancelled) MapActivityStatus(int id)
        {
            var text = _activityStatusTextCache?.TryGetValue(id, out var t) == true ? t : "未知";
            return id switch
            {
                1 => ("pending", text, false),
                2 => ("verify_pending", text, false),
                3 => ("verified", text, false),
                4 => ("cancelled", text, true),
                5 => ("refunding", text, false),
                6 => ("refunded", text, false),
                _ => ("unknown", text, false)
            };
        }
    }

    private static string NormalizeMediaUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var value = raw.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return value;
        if (value.StartsWith("/api/file/", StringComparison.OrdinalIgnoreCase)) return value;
        if (value.StartsWith("api/file/", StringComparison.OrdinalIgnoreCase)) return $"/{value}";
        var name = value.TrimStart('/');
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" ? $"/api/file/video/{name}" : $"/api/file/image/{name}";
    }

    /// <summary>
    /// 为订单详情生成内嵌物流轨迹（商品订单已发货/已完成时）
    /// </summary>
    private static List<object> GenerateOrderDetailLogistics(OrderKey order)
    {
        var shipTime = order.CreateTime.AddHours(4);
        var now = DateTime.Now;
        var isCompleted = order.RawStatusId == 4;
        var list = new List<object>();

        if (isCompleted)
        {
            list.Add(new
            {
                time = ClampToNow(shipTime.AddDays(2).AddHours(10), now).ToString("yyyy-MM-dd HH:mm:ss"),
                desc = "快件已签收，感谢使用"
            });
        }

        list.Add(new
        {
            time = ClampToNow(shipTime.AddDays(isCompleted ? 2 : 1).AddHours(8), now).ToString("yyyy-MM-dd HH:mm:ss"),
            desc = isCompleted ? "快件已到达【广州转运中心】" : "快递员正在派送中"
        });

        list.Add(new
        {
            time = ClampToNow(shipTime.AddDays(1).AddHours(6), now).ToString("yyyy-MM-dd HH:mm:ss"),
            desc = "快件已从【深圳集散中心】发出"
        });

        list.Add(new
        {
            time = shipTime.ToString("yyyy-MM-dd HH:mm:ss"),
            desc = "商品已发货"
        });

        return list.OrderByDescending(e => GetTimeValue(e)).ToList();
    }

    private static DateTime GetTimeValue(object obj)
    {
        var prop = obj.GetType().GetProperty("time")?.GetValue(obj) as string;
        return DateTime.TryParse(prop, out var t) ? t : DateTime.MinValue;
    }

    private static DateTime ClampToNow(DateTime value, DateTime now)
    {
        return value > now ? now : value;
    }

    private async Task<HashSet<long>> LoadActiveRefundOrderIdsAsync(IReadOnlyCollection<OrderKey> orders, int userId, CancellationToken ct)
    {
        var orderIds = orders.Select(x => x.OrderId).Distinct().ToList();
        if (orderIds.Count == 0) return [];

        var activeStatuses = new[] { "pending", "approved", "processing" };
        var ids = await _dbContext.RefundRecords
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId) && x.UserId == userId && activeStatuses.Contains(x.Status))
            .Select(x => x.OrderId)
            .Distinct()
            .ToListAsync(ct);

        return new HashSet<long>(ids);
    }

    /// <summary>
    /// 查询订单的退款状态映射（基于 refund_record 表）
    /// </summary>
    private async Task<Dictionary<long, string>> LoadRefundStatusMapAsync(IReadOnlyCollection<OrderKey> orders, int userId, CancellationToken ct)
    {
        var orderIds = orders.Select(x => x.OrderId).Distinct().ToList();
        if (orderIds.Count == 0) return [];

        var records = await _dbContext.RefundRecords
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId) && x.UserId == userId)
            .GroupBy(x => x.OrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                Status = g.OrderByDescending(r => r.CreateTime).Select(r => r.Status).FirstOrDefault()
            })
            .ToListAsync(ct);

        var map = new Dictionary<long, string>();
        foreach (var r in records)
        {
            var refundStatus = r.Status switch
            {
                "pending" or "approved" or "processing" => "refunding",
                "completed" => "refunded",
                _ => (string?)null
            };
            if (refundStatus is not null)
            {
                map[r.OrderId] = refundStatus;
            }
        }
        return map;
    }

    private static string FormatDiningTableNo(string? tableNo)
    {
        if (string.IsNullOrWhiteSpace(tableNo)) return tableNo ?? string.Empty;
        var digits = string.Concat(tableNo.Where(char.IsDigit));
        return string.IsNullOrEmpty(digits) ? tableNo : $"桌台{digits.TrimStart('0')}";
    }

    private sealed class OrderItem
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public int Quantity { get; init; }
        public string Image { get; init; } = string.Empty;
    }

    private static string GenerateVoucherCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        var code = new char[12];
        for (int i = 0; i < 12; i++)
            code[i] = chars[random.Next(chars.Length)];
        return new string(code);
    }

    private async Task RestoreCommodityStockAsync(long orderId, CancellationToken cancellationToken)
    {
        var details = await _dbContext.CommodityOrderDetails
            .Where(x => x.OrderId == orderId)
            .ToListAsync(cancellationToken);

        foreach (var detail in details)
        {
            var commodity = await _dbContext.Commodities.FindAsync(new object[] { detail.CommodityId }, cancellationToken);
            if (commodity is not null)
            {
                commodity.InStock = (commodity.InStock ?? 0) + detail.Quantity;
            }
        }
    }
}
