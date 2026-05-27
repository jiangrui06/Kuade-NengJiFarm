using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInventoryService _inventoryService;
    private readonly IPointsService _pointsService;
    private static Dictionary<int, string>? _dishStatusCache;
    private static readonly object _dishStatusCacheLock = new();
    private static Dictionary<int, string>? _commodityStatusTextCache;
    private static readonly object _commodityStatusCacheLock = new();
    private static Dictionary<int, string>? _activityStatusTextCache;
    private static readonly object _activityStatusCacheLock = new();

    public OrdersController(AppDbContext dbContext, IInventoryService inventoryService, IPointsService pointsService)
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
        _pointsService = pointsService;
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
        [FromQuery] string sortBy = "createTime",
        [FromQuery] string sortOrder = "desc",
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

        // type=acre 目前无独立订单表，直接返回空
        if (normalizedType == "acre")
        {
            return Ok(ApiResult.Success(new { orders = new List<object>(), total = 0, page, pageSize }));
        }

        var total = await CountAggregatedAsync(userId, normalizedType, normalizedStatus, keyword, cancellationToken);
        var slice = await LoadAggregatedSliceAsync(userId, normalizedType, normalizedStatus, keyword, take, cancellationToken);
        ApplySorting(slice, sortBy, sortOrder);
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
        [FromQuery] string sortBy = "createTime",
        [FromQuery] string sortOrder = "desc",
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
        ApplySorting(slice, sortBy, sortOrder);
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
        var ordered = await CountAggregatedAsync(userId, normalizedType, "ordered", null, cancellationToken);
        var verifyPending = await CountAggregatedAsync(userId, normalizedType, "verify_pending", null, cancellationToken);
        var refunding = await CountAggregatedAsync(userId, normalizedType, "refunding", null, cancellationToken);
        var refunded = await CountAggregatedAsync(userId, normalizedType, "refunded", null, cancellationToken);

        return Ok(ApiResult.Success(new { pending, paid, shipping, completed, cancelled, ordered, verifyPending, verify_pending = verifyPending, refunding, refunded }));
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

        // 活动订单加载有效期信息（基于活动设置的有效天数 Duration）
        object? validity = null;
        if (order.Type == "activity")
        {
            var detail = await _dbContext.ActivityOrderDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ActivityOrderId == order.OrderId, cancellationToken);
            int? duration = null;
            if (detail is not null)
            {
                var activity = await _dbContext.Activities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.IsDelete == 0 && x.ActivityId == detail.ActivityId, cancellationToken);
                duration = activity?.Duration;
            }

            if (duration > 0)
            {
                var startTime = order.CreateTime;
                var endTime = startTime.AddDays(duration.Value);
                var now = DateTime.Now;
                validity = new
                {
                    startTime = startTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    endTime = endTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    isValid = now <= endTime,
                    expired = now > endTime
                };
            }
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

        // 取消保护：订单创建 30 秒内拒绝取消，避免前端定时器与用户支付产生竞态
        if (targetStatus == "cancelled" && order.CreateTime >= DateTime.Now.AddSeconds(-30))
        {
            return Ok(ApiResult.Fail("订单刚创建，请勿频繁取消", 400));
        }

        var (ok, message) = await ApplyStatusTransitionAsync(order, targetStatus, request, cancellationToken);
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

        // 商品自取订单
        if (order.Type == "goods")
        {
            var entity = await _dbContext.CommodityOrders
                .FirstOrDefaultAsync(x => x.OrderId == order.OrderId, cancellationToken);

            if (entity is null)
                return Ok(ApiResult.Fail("订单不存在", 404));

            if (entity.DeliveryMethod != "pickup")
                return Ok(ApiResult.Fail("该订单不是自取订单", 400));

            if (entity.OrderStatusId == 1)
                return Ok(ApiResult.Fail("该订单未支付，无法核销", 403));

            if (entity.OrderStatusId == 9)
                return Ok(ApiResult.Fail("该订单已核销，不能重复核销", 409));

            if (entity.OrderStatusId == 5)
                return Ok(ApiResult.Fail("该订单已取消，无法核销", 403));

            // 如果尚未生成核销码则生成
            if (string.IsNullOrWhiteSpace(entity.VerifyCode))
            {
                entity.VerifyCode = GenerateVoucherCode();
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var code = entity.VerifyCode;
            var qrFileName = $"verify_{entity.OrderNo}.png";
            var qrFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "qrcode", qrFileName);
            if (!System.IO.File.Exists(qrFilePath))
            {
                var qrDir = Path.GetDirectoryName(qrFilePath)!;
                Directory.CreateDirectory(qrDir);
                var bytes = await Task.Run(() => GenerateQrPngBytes(code));
                await System.IO.File.WriteAllBytesAsync(qrFilePath, bytes, cancellationToken);
            }
            var qrCodeUrl = $"https://api.nengjifarm.com/api/file/image/{qrFileName}";

            return Ok(ApiResult.Success(new
            {
                qrCodeUrl,
                verifyCode = code,
                code,
                orderNo = entity.OrderNo
            }));
        }

        // 活动订单（原有逻辑）
        if (order.Type != "activity")
        {
            return Ok(ApiResult.Fail("只有活动/自取订单支持核销码", 400));
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

        var activityCode = detail.ActivityQrcode;
        var activityFileName = $"activity_{order.OrderNo}.png";
        var activityFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "qrcode", activityFileName);
        if (!System.IO.File.Exists(activityFilePath))
        {
            var qrDir = Path.GetDirectoryName(activityFilePath)!;
            Directory.CreateDirectory(qrDir);
            var bytes = await Task.Run(() => GenerateQrPngBytes(activityCode));
            await System.IO.File.WriteAllBytesAsync(activityFilePath, bytes, cancellationToken);
        }
        var activityQrCodeUrl = $"https://api.nengjifarm.com/api/file/image/{activityFileName}";

        return Ok(ApiResult.Success(new
        {
            qrCodeUrl = activityQrCodeUrl,
            code = activityCode,
            verifyCode = activityCode
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
            "acre" or "subscribe" => "acre",
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
            "ordered" => "ordered",
            _ => value
        };
    }

    private static string NormalizeTrackingNumber(string? trackingNumber)
    {
        return string.IsNullOrWhiteSpace(trackingNumber)
            ? string.Empty
            : trackingNumber.Trim().Replace(" ", "", StringComparison.Ordinal);
    }

    private static long? ResolveTrackingTypeId(UpdateOrderStatusRequest? request, string trackingNumber)
    {
        if (request?.TrackingTypeId is > 0)
        {
            return request.TrackingTypeId.Value;
        }

        var deliveryId = request?.DeliveryId?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(deliveryId))
        {
            var id = ResolveTrackingTypeIdByCode(deliveryId);
            if (id is not null) return id;
        }

        var trackingTypeName = request?.TrackingTypeName?.Trim();
        if (!string.IsNullOrWhiteSpace(trackingTypeName))
        {
            var id = ResolveTrackingTypeIdByName(trackingTypeName);
            if (id is not null) return id;
        }

        var upperNumber = trackingNumber.Trim().ToUpperInvariant();
        foreach (var (prefix, id) in TrackingNumberPrefixMap)
        {
            if (upperNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    private static readonly (string Code, long Id)[] TrackingDeliveryCodeMap =
    [
        ("SF", 1),
        ("EMS", 2),
        ("POST", 2),
        ("CHINAPOST", 2),
        ("YTO", 3),
        ("ZTO", 4),
        ("STO", 5),
        ("YD", 6),
        ("YUNDA", 6),
        ("JD", 7),
        ("JDL", 7),
    ];

    private static readonly (string Prefix, long Id)[] TrackingNumberPrefixMap =
    [
        ("JDX", 7),
        ("JD", 7),
        ("SF", 1),
        ("EMS", 2),
        ("YTO", 3),
        ("YT", 3),
        ("ZTO", 4),
        ("ZT", 4),
        ("STO", 5),
        ("ST", 5),
        ("YUNDA", 6),
        ("YD", 6),
    ];

    private static long? ResolveTrackingTypeIdByCode(string code)
    {
        foreach (var (knownCode, id) in TrackingDeliveryCodeMap)
        {
            if (string.Equals(code, knownCode, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    private static long? ResolveTrackingTypeIdByName(string name)
    {
        if (name.Contains("顺丰", StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.Contains("邮政", StringComparison.OrdinalIgnoreCase) || name.Contains("EMS", StringComparison.OrdinalIgnoreCase)) return 2;
        if (name.Contains("圆通", StringComparison.OrdinalIgnoreCase)) return 3;
        if (name.Contains("中通", StringComparison.OrdinalIgnoreCase)) return 4;
        if (name.Contains("申通", StringComparison.OrdinalIgnoreCase)) return 5;
        if (name.Contains("韵达", StringComparison.OrdinalIgnoreCase)) return 6;
        if (name.Contains("京东", StringComparison.OrdinalIgnoreCase)) return 7;

        return null;
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
                .Where(c => c.IsDelete == 0 && c.ProductName.Contains(value))
                .Select(c => c.CommodityId)
                .Contains(d.CommodityId))
            .Select(d => d.OrderId);
        return query.Where(x => x.OrderNo.Contains(value) || matchedIds.Contains(x.OrderId));
    }

    private IQueryable<DishOrder> ApplySearchKeywordFilter(IQueryable<DishOrder> query, string value)
    {
        var matchedIds = _dbContext.DishOrderDetails
            .Where(d => _dbContext.Dishes
                .Where(dish => dish.IsDelete == 0 && dish.DishName.Contains(value))
                .Select(dish => dish.DishId)
                .Contains(d.DishId))
            .Select(d => d.DishOrderId);
        return query.Where(x => x.OrderNo.Contains(value) || matchedIds.Contains(x.OrderId));
    }

    private IQueryable<ActivityOrder> ApplySearchKeywordFilter(IQueryable<ActivityOrder> query, string value)
    {
        var matchedIds = _dbContext.ActivityOrderDetails
            .Where(d => _dbContext.Activities
                .Where(a => a.IsDelete == 0 && a.Title.Contains(value))
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
            "verify_pending" => query.Where(x => x.OrderStatusId == 8),
            "verified" => query.Where(x => x.OrderStatusId == 9),
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
            "paid" or "ordered" => query.Where(x => x.OrderStatusId == 2),
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
            "paid" or "verify_pending" => query.Where(x => x.OrderStatusId == 2),
            "completed" or "verified" => query.Where(x => x.OrderStatusId == 3),
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
                "verify_pending" => new[] { 8 },
                "verified" => new[] { 9 },
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
                "paid" or "ordered" => new[] { 2 },
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
                "paid" or "verify_pending" => new[] { 2 },
                "completed" or "verified" => new[] { 3 },
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
                    .Where(x => x.IsDelete == 0 && commodityIds.Contains(x.CommodityId))
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
                        Name = !string.IsNullOrEmpty(d.GoodsName) ? d.GoodsName
                            : commodity?.ProductName ?? $"商品{d.CommodityId}",
                        Price = d.UnitPrice,
                        Quantity = d.Quantity,
                        Image = !string.IsNullOrEmpty(d.ImageUrl) ? d.ImageUrl
                            : NormalizeMediaUrl(commodity?.ImageUrl),
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
                    .Where(x => x.IsDelete == 0 && dishIds.Contains(x.DishId))
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
            var activityIds = details.Select(x => x.ActivityId).Distinct().ToList();
            var activityMap = activityIds.Count == 0
                ? new Dictionary<long, ActivityEntity>()
                : await _dbContext.Activities.AsNoTracking()
                    .Where(x => x.IsDelete == 0 && activityIds.Contains(x.ActivityId))
                    .ToDictionaryAsync(x => x.ActivityId, cancellationToken);

            foreach (var group in details.GroupBy(x => x.ActivityOrderId))
            {
                var orderNo = activity.First(o => o.OrderId == group.Key).OrderNo;
                result[orderNo] = group.Select(d =>
                {
                    activityMap.TryGetValue(d.ActivityId, out var a);
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
            id = order.OrderNo,
            orderNo = order.OrderNo,
            type = order.Type,
            typeText = order.TypeText,
            status = order.Status,
            statusText = order.StatusText,
            orderStatusId = order.RawStatusId,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            totalQuantity = order.TotalQuantity,
            paymentTime = order.RawStatusId >= 2 ? order.CreateTime.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            shippingTime = order.RawStatusId >= 3 ? order.CreateTime.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            completeTime = order.RawStatusId >= 4 ? order.CreateTime.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            transactionId = order.WxPayNo,
            diningTableNo,
            deliveryMethod = order.DeliveryMethod ?? (order.Type == "goods" ? "express" : null),
            isPickupOrder = (order.DeliveryMethod ?? (order.Type == "goods" ? "express" : null)) == "pickup",
            items = items.Select(x => new { id = x.Id, name = x.Name, price = x.Price, quantity = x.Quantity, image = x.Image, statusId = x.StatusId, status = MapDetailStatusValue(x.StatusId) }).ToList(),
            remark = order.Remark,
            verified = (order.Type == "activity" && order.RawStatusId == 3) || (order.Type == "goods" && order.RawStatusId == 9),
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
            id = order.OrderNo,
            orderNo = order.OrderNo,
            type = order.Type,
            typeText = order.TypeText,
            status = order.Status,
            statusText = order.StatusText,
            orderStatusId = order.RawStatusId,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            paymentTime = order.RawStatusId >= 2 ? order.CreateTime.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            shippingTime = order.RawStatusId >= 3 ? order.CreateTime.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            completeTime = order.RawStatusId >= 4 ? order.CreateTime.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss") : (string?)null,
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            totalQuantity = order.TotalQuantity,
            shippingAddress,
            deliveryMethod = order.DeliveryMethod ?? (order.Type == "goods" ? "express" : null),
            isPickupOrder = (order.DeliveryMethod ?? (order.Type == "goods" ? "express" : null)) == "pickup",
            verifyCode = order.VerifyCode,
            items = items.Select(x => new { id = x.Id, name = x.Name, price = x.Price, quantity = x.Quantity, image = x.Image, statusId = x.StatusId, status = MapDetailStatusValue(x.StatusId) }).ToList(),
            paymentMethod = (string?)null,
            transactionId = order.WxPayNo,
            diningTableNo,
            remark = order.Remark,
            logistics = logistics ?? Array.Empty<object>(),
            validity,
            verified = (order.Type == "activity" && order.RawStatusId == 3) || (order.Type == "goods" && order.RawStatusId == 9),
            hasRefund,
            refundStatus
        };
    }

    private async Task<OrderKey?> FindOrderAsync(string id, int userId, bool tracking, CancellationToken cancellationToken)
    {
        var orderNo = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return null;
        }

        if (tracking)
        {
            var g = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == orderNo, cancellationToken);
            if (g is not null) return OrderKey.FromCommodity(g, g);
            var f = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == orderNo, cancellationToken);
            if (f is not null) return OrderKey.FromDish(f, f);
            var a = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == orderNo, cancellationToken);
            if (a is not null) return OrderKey.FromActivity(a, a);
            return null;
        }

        var g2 = await _dbContext.CommodityOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == orderNo, cancellationToken);
        if (g2 is not null) return OrderKey.FromCommodity(g2);
        var f2 = await _dbContext.DishOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == orderNo, cancellationToken);
        if (f2 is not null) return OrderKey.FromDish(f2);
        var a2 = await _dbContext.ActivityOrders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNo == orderNo, cancellationToken);
        if (a2 is not null) return OrderKey.FromActivity(a2);

        return null;
    }

    private async Task<(bool ok, string message)> ApplyStatusTransitionAsync(OrderKey order, string targetStatus, UpdateOrderStatusRequest? request, CancellationToken cancellationToken)
    {
        if (order.Type == "goods")
        {
            var entity = (CommodityOrder)order.TrackingEntity!;
            if (targetStatus == "cancelled")
            {
                if (entity.OrderStatusId != 1) return (false, "当前状态不可取消");

                // 恢复商品库存
                var details = await _dbContext.CommodityOrderDetails
                    .Where(x => x.OrderId == entity.OrderId)
                    .ToListAsync(cancellationToken);
                foreach (var d in details)
                {
                    await _inventoryService.RestoreAsync(ProductType.Commodity, d.CommodityId, d.Quantity);
                }

                entity.OrderStatusId = 5;
            }
            else if (targetStatus == "shipped")
            {
                if (entity.OrderStatusId != 2) return (false, "当前状态不可发货");

                entity.OrderStatusId = 3;

                // 快照收货人手机号（发货时的地址手机号，后续地址变更不影响物流查询）
                if (string.IsNullOrWhiteSpace(entity.ReceiverPhone))
                {
                    var shipAddress = await _dbContext.ShippingAddresses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.AddressId == entity.AddressId, cancellationToken);
                    if (shipAddress is not null)
                    {
                        entity.ReceiverPhone = shipAddress.ContactPhone?.Trim() ?? string.Empty;
                    }
                }
            }
            else if (targetStatus == "completed")
            {
                if (entity.OrderStatusId != 3 && entity.OrderStatusId != 2) return (false, "当前状态不可确认收货");
                entity.OrderStatusId = 4;

                // 订单完成时发放积分
                await _pointsService.EarnPointsAsync(order.UserId, order.OrderNo, order.TotalAmount, cancellationToken);
            }
            else
            {
                return (false, "不支持的状态更新");
            }

            await SyncDetailStatusAsync(entity.OrderId, entity.OrderStatusId, cancellationToken);
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

                // 恢复菜品库存
                var dishDetails = await _dbContext.DishOrderDetails
                    .Where(x => x.DishOrderId == entity.OrderId)
                    .ToListAsync(cancellationToken);
                foreach (var d in dishDetails)
                {
                    await _inventoryService.RestoreAsync(ProductType.Dish, d.DishId, d.Quantity);
                }

                entity.OrderStatusId = 4;
            }
            else if (targetStatus == "completed")
            {
                if (entity.OrderStatusId != 2) return (false, "当前状态不可完成");
                entity.OrderStatusId = 3;

                // 订单完成时发放积分
                await _pointsService.EarnPointsAsync(order.UserId, order.OrderNo, order.TotalAmount, cancellationToken);
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
                var paidStatusId = entity.DeliveryMethod == "pickup" ? 8 : 2;
                entity.OrderStatusId = paidStatusId;
                entity.WxPayNo = wxPayNo;
                await SyncDetailStatusAsync(entity.OrderId, paidStatusId, cancellationToken);
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
        public string? TrackingNumber { get; set; }
        public long? TrackingTypeId { get; set; }
        public string? TrackingTypeName { get; set; }
        public string? DeliveryId { get; set; }
    }

    public sealed class MockPayRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
    }

    private sealed class OrderKey
    {
        public string Type { get; init; } = string.Empty;
        public string TypeText { get; init; } = string.Empty;
        public int UserId { get; init; }
        public long OrderId { get; init; }
        public string OrderNo { get; init; } = string.Empty;
        public DateTime CreateTime { get; init; }
        public decimal TotalAmount { get; init; }
        public int TotalQuantity { get; init; }
        public int RawStatusId { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public string StatusText { get; private set; } = string.Empty;
        public string? WxPayNo { get; private set; }
        public long? AddressId { get; private set; }
        public object? TrackingEntity { get; private set; }
        public bool? IsCancelled { get; private set; }
        public long DiningTableId { get; private set; }
        public string? Remark { get; private set; }
        public string? DeliveryMethod { get; private set; }
        public string? VerifyCode { get; private set; }

        public static OrderKey FromCommodity(CommodityOrder order) => FromCommodity(order, null);

        public static OrderKey FromCommodity(CommodityOrder order, CommodityOrder? trackingEntity)
        {
            var (status, text, cancelled) = MapCommodityStatus(order.OrderStatusId);
            return new OrderKey
            {
                Type = "goods",
                TypeText = "商品订单",
                UserId = order.UserId,
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
                IsCancelled = cancelled,
                DeliveryMethod = order.DeliveryMethod,
                VerifyCode = order.VerifyCode
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
                UserId = order.UserId,
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
                UserId = order.UserId,
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
                8 => ("verify_pending", text, false),
                9 => ("verified", text, false),
                _ => ("unknown", text, false)
            };
        }

        private static (string status, string text, bool cancelled) MapDishStatus(int id)
        {
            var text = _dishStatusCache?.TryGetValue(id, out var t) == true ? t : "未知";
            var status = id switch
            {
                1 => "pending", 2 => "ordered", 3 => "completed", 4 => "cancelled",
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

    private static string NormalizeMediaUrl(string? raw) => MediaUrlHelper.Normalize(raw);

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

        var allRecords = await _dbContext.RefundRecords
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId) && x.UserId == userId)
            .ToListAsync(ct);

        var latestPerOrder = allRecords
            .GroupBy(x => x.OrderId)
            .Select(g => g.OrderByDescending(r => r.CreateTime).First());

        var map = new Dictionary<long, string>();
        foreach (var r in latestPerOrder)
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

    private static void ApplySorting(List<OrderKey> slice, string sortBy, string sortOrder)
    {
        var desc = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var key = (sortBy ?? "createTime").Trim().ToLowerInvariant();

        if (key == "totalprice" || key == "total_amount" || key == "amount")
        {
            if (desc)
                slice.Sort((a, b) => b.TotalAmount.CompareTo(a.TotalAmount));
            else
                slice.Sort((a, b) => a.TotalAmount.CompareTo(b.TotalAmount));
        }
        else
        {
            // 默认按 createTime 排序
            if (desc)
                slice.Sort((a, b) => b.CreateTime.CompareTo(a.CreateTime));
            else
                slice.Sort((a, b) => a.CreateTime.CompareTo(b.CreateTime));
        }
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
        public int StatusId { get; init; }
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

    /// <summary>
    /// 生成 QR 码 PNG 图片字节
    /// </summary>
    private static byte[] GenerateQrPngBytes(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(20);
    }
}
