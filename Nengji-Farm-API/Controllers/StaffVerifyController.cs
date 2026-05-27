using System.Security.Claims;
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
[Route("api/staff-verify")]
public class StaffVerifyController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IPointsService _pointsService;

    public StaffVerifyController(AppDbContext dbContext, IPointsService pointsService)
    {
        _dbContext = dbContext;
        _pointsService = pointsService;
    }

    /// <summary>
    /// 验证员工权限
    /// </summary>
    [HttpGet("permission")]
    public async Task<IActionResult> VerifyPermission(CancellationToken cancellationToken)
    {
        try
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
            {
                return Ok(ApiResult.Success(new
                {
                    hasPermission = false,
                    role = (string?)null,
                    staffId = (string?)null
                }));
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return Ok(ApiResult.Success(new
                {
                    hasPermission = false,
                    role = (string?)null,
                    staffId = (string?)null
                }));
            }

            var roleName = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.RoleId == user.RoleId)
                .Select(x => x.RoleName)
                .FirstOrDefaultAsync(cancellationToken);

            var hasPermission = IsStaffRole(roleName);

            return Ok(ApiResult.Success(new
            {
                hasPermission,
                role = hasPermission ? roleName : null,
                staffId = hasPermission ? user.UserId.ToString() : null
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"验证失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 查询券信息（仅查询，不核销）
    /// </summary>
    [HttpPost("voucher-info")]
    public async Task<IActionResult> GetVoucherInfo([FromBody] VerifyVoucherRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var staff = await GetCurrentStaffAsync(cancellationToken);
            if (staff is null)
                return Ok(ApiResult.Fail("无权限访问", 403));

            var rawCode = request?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(rawCode))
                return Ok(ApiResult.Fail("请输入核销码", 400));

            var (prefix, code) = ParseQrCodePrefix(rawCode);
            return Ok(await BuildVoucherInfoAsync(prefix, code, cancellationToken));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 核销券类（采摘券/活动券）
    /// </summary>
    [HttpPost("voucher")]
    public async Task<IActionResult> VerifyVoucher([FromBody] VerifyVoucherRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var staff = await GetCurrentStaffAsync(cancellationToken);
            if (staff is null)
            {
                return Ok(ApiResult.Fail("无权限访问", 403));
            }

            var rawCode = request?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(rawCode))
            {
                return Ok(ApiResult.Fail("请输入核销码", 400));
            }

            var (prefix, code) = ParseQrCodePrefix(rawCode);

            // 根据前缀确定类型
            if (prefix == "PK")
            {
                var commodityOrder = await _dbContext.CommodityOrders
                    .FirstOrDefaultAsync(x => x.VerifyCode == code && x.DeliveryMethod == "pickup", cancellationToken);

                if (commodityOrder is not null)
                    return await VerifyCommodityPickupAsync(commodityOrder, staff, cancellationToken);
            }
            else if (prefix == "ACT")
            {
                var detail = await _dbContext.ActivityOrderDetails
                    .FirstOrDefaultAsync(x => x.ActivityQrcode == code, cancellationToken);

                if (detail is not null)
                    return await VerifyActivityVoucherAsync(detail, staff, cancellationToken);
            }

            // 前缀不匹配或无前缀 → 兼容旧数据，按原顺序查找
            // 先尝试商品自取
            var commodityFallback = await _dbContext.CommodityOrders
                .FirstOrDefaultAsync(x => x.VerifyCode == rawCode && x.DeliveryMethod == "pickup", cancellationToken);
            if (commodityFallback is not null)
                return await VerifyCommodityPickupAsync(commodityFallback, staff, cancellationToken);

            // 再尝试活动券
            var activityFallback = await _dbContext.ActivityOrderDetails
                .FirstOrDefaultAsync(x => x.ActivityQrcode == rawCode, cancellationToken);
            if (activityFallback is not null)
                return await VerifyActivityVoucherAsync(activityFallback, staff, cancellationToken);

            return Ok(ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"核销失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 核销活动券
    /// </summary>
    private async Task<IActionResult> VerifyActivityVoucherAsync(ActivityOrderDetail detail, User staff, CancellationToken cancellationToken)
    {
        var order = await _dbContext.ActivityOrders
            .FirstOrDefaultAsync(x => x.OrderId == detail.ActivityOrderId, cancellationToken);

        if (order is null)
            return Ok(ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404));

        var activity = await _dbContext.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsDelete == 0 && x.ActivityId == detail.ActivityId, cancellationToken);

        var activityTypeName = activity is not null
            ? await _dbContext.ActivityTypes
                .AsNoTracking()
                .Where(x => x.ActivityTypeId == activity.TypeId)
                .Select(x => x.TypeName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var voucherType = activity?.TypeId == 2 ? "activity" : "pick";
        var typeName = activityTypeName ?? (voucherType == "activity" ? "活动券" : "采摘券");
        var content = activity?.Title ?? "活动券";

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);

        // activity_order_status: 1=待付款, 2=待核销, 3=已核销, 4=已取消
        if (order.OrderStatusId == 3)
        {
            var lastRecord = await _dbContext.ActivityVerificationRecords
                .AsNoTracking()
                .Where(x => x.ActivityOrderDetailsId == detail.ActivityOrderDetailsId)
                .OrderByDescending(x => x.VerificationTime)
                .FirstOrDefaultAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                voucherType,
                typeName,
                userName = ResolveUserName(user, null, order.OrderNo),
                userPhone = user?.PhoneNumber ?? string.Empty,
                content,
                participantCount = detail.Quantity,
                verifyTime = lastRecord?.VerificationTime.ToString("yyyy-MM-dd HH:mm:ss") ?? order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                verified = true,
                alreadyVerified = true
            }, "券已核销"));
        }

        if (order.OrderStatusId == 1)
            return Ok(ApiResult.Fail("该券未支付，无法核销", 403));

        if (order.OrderStatusId == 4)
            return Ok(ApiResult.Fail("该券已取消，无法核销", 403));

        // 执行核销
        order.OrderStatusId = 3;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.Now;
        var verificationRecord = new ActivityVerificationRecord
        {
            ActivityOrderDetailsId = detail.ActivityOrderDetailsId,
            VerificationTime = now
        };
        _dbContext.ActivityVerificationRecords.Add(verificationRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _pointsService.EarnPointsAsync(order.UserId, order.OrderNo, order.TotalAmount, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            voucherType,
            typeName,
            userName = ResolveUserName(user, null, order.OrderNo),
            userPhone = user?.PhoneNumber ?? string.Empty,
            content,
            participantCount = detail.Quantity,
            verifyTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
            verified = true,
            alreadyVerified = false
        }, "核销成功"));
    }

    /// <summary>
    /// 核销商品自取订单
    /// </summary>
    private async Task<IActionResult> VerifyCommodityPickupAsync(CommodityOrder order, User staff, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);

        // 已核销
        if (order.OrderStatusId == 9)
        {
            return Ok(ApiResult.Success(new
            {
                verified = true,
                alreadyVerified = true,
                voucherType = "goods_pickup",
                typeName = "商品自取",
                userName = ResolveUserName(user, null, order.OrderNo),
                userPhone = user?.PhoneNumber ?? string.Empty,
                content = "到店自取商品",
                title = "商品自取",
                orderNo = order.OrderNo,
                participantCount = order.TotalQuantity,
                message = "该订单已核销"
            }));
        }

        if (order.OrderStatusId == 1)
        {
            return Ok(ApiResult.Fail("该订单未支付，无法核销", 403));
        }

        if (order.OrderStatusId == 5)
        {
            return Ok(ApiResult.Fail("该订单已取消，无法核销", 403));
        }

        if (order.OrderStatusId != 8)
        {
            return Ok(ApiResult.Fail("该订单状态不支持核销", 409));
        }

        // 执行核销
        order.OrderStatusId = 9;
        order.VerifiedTime = DateTime.Now;

        _dbContext.CommodityVerifyRecords.Add(new CommodityVerifyRecord
        {
            OrderId = order.OrderId,
            StaffId = staff.UserId,
            VerifyTime = DateTime.Now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 订单完成时发放积分
        await _pointsService.EarnPointsAsync(order.UserId, order.OrderNo, order.TotalAmount, cancellationToken);

        var verifyTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return Ok(ApiResult.Success(new
        {
            verified = true,
            alreadyVerified = false,
            voucherType = "goods_pickup",
            typeName = "商品自取",
            userName = ResolveUserName(user, null, order.OrderNo),
            userPhone = user?.PhoneNumber ?? string.Empty,
            content = "到店自取商品",
            title = "商品自取",
            orderNo = order.OrderNo,
            verifyTime,
            participantCount = order.TotalQuantity,
            message = "核销成功"
        }, "核销成功"));
    }

    /// <summary>
    /// 核销积分兑换
    /// </summary>
    [HttpPost("points-exchange")]
    public async Task<IActionResult> VerifyPointsExchange([FromBody] VerifyVoucherRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var staff = await GetCurrentStaffAsync(cancellationToken);
            if (staff is null)
            {
                return Ok(ApiResult.Fail("无权限访问", 403));
            }

            var code = request?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return Ok(ApiResult.Fail("请输入核销码", 400));
            }

            // 按 order_no 查找兑换记录（points_commodity_order 表）
            var exchange = await _dbContext.PointsExchanges
                .FirstOrDefaultAsync(x => x.OrderNo == code, cancellationToken);

            if (exchange is null)
            {
                return Ok(ApiResult.Fail("未找到该兑换记录，请确认二维码是否正确", 404));
            }

            // 从 points_commodity_order_status 表获取状态名
            var statusName = await GetPointsOrderStatusNameAsync(exchange.StatusId, cancellationToken);

            if (statusName == "verified")
            {
                return Ok(ApiResult.Fail("该兑换已核销，不能重复核销", 409));
            }

            if (statusName == "cancelled")
            {
                return Ok(ApiResult.Fail("该兑换已取消，无法核销", 403));
            }

            if (statusName != "pending")
            {
                return Ok(ApiResult.Fail("该兑换状态异常，无法核销", 400));
            }

            // 获取商品名称（从 points_commodity 表）
            var commodity = await _dbContext.PointsCommodities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == exchange.CommodityId && x.IsDelete == 0, cancellationToken);
            var goodsName = commodity?.Name ?? "积分商品";

            // 获取持券人信息
            var exchangeUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == exchange.UserId, cancellationToken);

            // 获取员工真实姓名
            var operatorName = ResolveUserName(staff, null, string.Empty);

            var now = DateTime.Now;

            // 获取 verified 状态 ID
            var verifiedStatusId = await GetVerifiedStatusIdAsync(cancellationToken);

            // 执行核销
            exchange.StatusId = verifiedStatusId;
            exchange.VerifyTime = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderNo = exchange.OrderNo,
                goodsName,
                userName = ResolveUserName(exchangeUser, null, exchange.OrderNo),
                userPhone = exchangeUser?.PhoneNumber ?? string.Empty,
                verifyTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
                operatorName
            }, "核销成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"核销失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取核销历史记录
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetVerifyHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? voucherType = "all",
        [FromQuery] string? keyword = null,
        [FromQuery] string? activityName = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var staff = await GetCurrentStaffAsync(cancellationToken);
            if (staff is null)
            {
                return Ok(ApiResult.Fail("无权限访问", 403));
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var normalizedType = (voucherType ?? "all").Trim().ToLowerInvariant();
            var activityTypeMap = await _dbContext.ActivityTypes
                .AsNoTracking()
                .ToDictionaryAsync(x => x.ActivityTypeId, x => x.TypeName, cancellationToken);

            // Collect all records
            var allRecords = new List<VerifyHistoryItem>();

            // 积分兑换
            if (normalizedType is "all" or "points_exchange")
            {
                var records = await GetPointsExchangeVerifyHistoryAsync(keyword, startDate, endDate, cancellationToken);
                allRecords.AddRange(records);
            }

            // 商品自取
            if (normalizedType is "all" or "goods_pickup")
            {
                var records = await GetCommodityVerifyHistoryAsync(keyword, startDate, endDate, cancellationToken);
                allRecords.AddRange(records);
            }

            // 活动类（亲子研学 + 采摘体验）
            if (normalizedType is "all" or "parent_child_study" or "pick_experience")
            {
                var records = await GetActivityVerifyHistoryAsync(normalizedType, keyword, activityName, startDate, endDate, activityTypeMap, cancellationToken);
                allRecords.AddRange(records);
            }

            // 核销时间倒序
            allRecords = allRecords
                .OrderByDescending(r => r.VerifyTimeTicks)
                .ToList();

            var total = allRecords.Count;
            var list = allRecords
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => r.ToResponse())
                .ToList();

            return Ok(ApiResult.Success(new
            {
                list,
                total,
                page,
                pageSize
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"服务器错误: {ex.Message}"));
        }
    }

    private async Task<List<VerifyHistoryItem>> GetPointsExchangeVerifyHistoryAsync(
        string? keyword, string? startDate, string? endDate, CancellationToken ct)
    {
        var verifiedStatusId = await GetVerifiedStatusIdAsync(ct);
        var statusName = await GetPointsOrderStatusNameAsync(verifiedStatusId, ct);

        var query = _dbContext.PointsExchanges
            .AsNoTracking()
            .Where(x => x.StatusId == verifiedStatusId);

        if (DateTime.TryParse(startDate, out var start))
        {
            var startDay = start.Date;
            query = query.Where(x => x.VerifyTime >= startDay);
        }
        if (DateTime.TryParse(endDate, out var end))
        {
            var endDay = end.Date.AddDays(1);
            query = query.Where(x => x.VerifyTime < endDay);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = from q in query
                    join u in _dbContext.Users on q.UserId equals u.UserId into uj
                    from u in uj.DefaultIfEmpty()
                    join c in _dbContext.PointsCommodities on q.CommodityId equals c.Id into cj
                    from c in cj.Where(x => x.IsDelete == 0).DefaultIfEmpty()
                    where q.OrderNo.Contains(kw)
                       || (u != null && u.RealName.Contains(kw))
                       || (u != null && u.WxName.Contains(kw))
                       || (c != null && c.Name.Contains(kw))
                    select q;
        }

        var records = await query
            .OrderByDescending(x => x.VerifyTime)
            .ToListAsync(ct);

        var userIds = records.Select(x => x.UserId).Distinct().ToList();
        var userMap = userIds.Count > 0
            ? await _dbContext.Users.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct)
            : new Dictionary<int, User>();

        var commodityIds = records.Select(x => x.CommodityId).Distinct().ToList();
        var commodityMap = commodityIds.Count > 0
            ? await _dbContext.PointsCommodities.AsNoTracking().Where(x => commodityIds.Contains(x.Id) && x.IsDelete == 0).ToDictionaryAsync(x => x.Id, ct)
            : new Dictionary<int, PointsCommodity>();

        return records.Select(r =>
        {
            userMap.TryGetValue(r.UserId, out var u);
            commodityMap.TryGetValue(r.CommodityId, out var c);
            var goodsName = c?.Name ?? "积分商品";
            var verifyTime = r.VerifyTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            return new VerifyHistoryItem
            {
                Id = $"pex_{r.Id}",
                VoucherType = "points_exchange",
                TypeName = "积分兑换",
                OrderNo = r.OrderNo,
                GoodsName = goodsName,
                UserName = ResolveUserName(u, null, r.OrderNo),
                UserPhone = u?.PhoneNumber ?? string.Empty,
                Content = null,
                Description = null,
                ParticipantCount = 0,
                IsPickupOrder = false,
                DeliveryMethod = null,
                VerifyTime = verifyTime,
                Time = verifyTime,
                CreateTime = r.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Status = statusName,
                OrderId = r.OrderNo,
                VerifyTimeTicks = r.VerifyTime?.Ticks ?? 0
            };
        }).ToList();
    }

    private async Task<List<VerifyHistoryItem>> GetCommodityVerifyHistoryAsync(
        string? keyword, string? startDate, string? endDate, CancellationToken ct)
    {
        var query = from vr in _dbContext.CommodityVerifyRecords
                    join o in _dbContext.CommodityOrders on vr.OrderId equals o.OrderId
                    join d in _dbContext.CommodityOrderDetails on vr.OrderId equals d.OrderId into dj
                    from d in dj.DefaultIfEmpty()
                    where o.DeliveryMethod == "pickup"
                    select new { vr, o, d };

        if (DateTime.TryParse(startDate, out var start))
        {
            var startDay = start.Date;
            query = query.Where(x => x.vr.VerifyTime >= startDay);
        }
        if (DateTime.TryParse(endDate, out var end))
        {
            var endDay = end.Date.AddDays(1);
            query = query.Where(x => x.vr.VerifyTime < endDay);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = from q in query
                    join u in _dbContext.Users on q.o.UserId equals u.UserId into uj
                    from u in uj.DefaultIfEmpty()
                    where q.o.OrderNo.Contains(kw)
                       || (u != null && u.RealName.Contains(kw))
                       || (u != null && u.WxName.Contains(kw))
                    select q;
        }

        var records = await query
            .OrderByDescending(x => x.vr.VerifyTime)
            .ToListAsync(ct);

        var userIds = records.Select(x => x.o.UserId).Distinct().ToList();
        var userMap = userIds.Count > 0
            ? await _dbContext.Users.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct)
            : new Dictionary<int, User>();

        // Group by order and collect goods names, descriptions
        var orderDetails = records
            .GroupBy(x => x.o.OrderId)
            .ToDictionary(g => g.Key, g =>
            {
                var details = g.Where(x => x.d != null).Select(x => x.d!).DistinctBy(x => x.CommodityOrderDetailsId).ToList();
                var names = details.Select(x => x.GoodsName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
                var goodsName = names.Count > 0 ? string.Join("、", names) : null;
                var description = details.Select(x => x.GoodsName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                return (goodsName, description);
            });

        return records.GroupBy(x => x.vr.Id).Select(g =>
        {
            var r = g.First();
            userMap.TryGetValue(r.o.UserId, out var u);
            var (goodsName, description) = orderDetails.GetValueOrDefault(r.o.OrderId);
            var verifyTime = r.vr.VerifyTime.ToString("yyyy-MM-dd HH:mm:ss");
            return new VerifyHistoryItem
            {
                Id = $"gpu_{r.vr.Id}",
                VoucherType = "goods_pickup",
                TypeName = "商品自取",
                OrderNo = r.o.OrderNo,
                GoodsName = goodsName,
                UserName = ResolveUserName(u, null, r.o.OrderNo),
                UserPhone = u?.PhoneNumber ?? string.Empty,
                Content = "到店自取商品",
                Description = description,
                ParticipantCount = 0,
                IsPickupOrder = true,
                DeliveryMethod = "pickup",
                VerifyTime = verifyTime,
                Time = verifyTime,
                CreateTime = r.o.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Status = "verified",
                OrderId = r.o.OrderNo,
                VerifyTimeTicks = r.vr.VerifyTime.Ticks
            };
        }).ToList();
    }

    private async Task<List<VerifyHistoryItem>> GetActivityVerifyHistoryAsync(
        string normalizedType, string? keyword, string? activityName,
        string? startDate, string? endDate,
        Dictionary<int, string> activityTypeMap, CancellationToken ct)
    {
        // Filter by activity type name (亲子研学 / 采摘体验)
        List<int>? filterTypeIds = null;
        if (normalizedType == "parent_child_study")
        {
            filterTypeIds = activityTypeMap
                .Where(t => t.Value == "亲子研学")
                .Select(t => t.Key)
                .ToList();
        }
        else if (normalizedType == "pick_experience")
        {
            filterTypeIds = activityTypeMap
                .Where(t => t.Value == "采摘体验")
                .Select(t => t.Key)
                .ToList();
        }

        // Build query with explicit joins
        var query = from vr in _dbContext.ActivityVerificationRecords
                    join detail in _dbContext.ActivityOrderDetails on vr.ActivityOrderDetailsId equals detail.ActivityOrderDetailsId
                    join o in _dbContext.ActivityOrders on detail.ActivityOrderId equals o.OrderId
                    join a in _dbContext.Activities on detail.ActivityId equals a.ActivityId
                    where a.IsDelete == 0 && o.OrderStatusId == 3
                    select new { vr, detail, o, a };

        // Filter by activity type
        if (filterTypeIds is { Count: > 0 })
        {
            query = query.Where(x => filterTypeIds.Contains(x.a.TypeId));
        }

        // Filter by activity title
        if (!string.IsNullOrWhiteSpace(activityName))
        {
            var name = activityName.Trim();
            query = query.Where(x => x.a.Title.Contains(name));
        }

        // Date range filter
        if (DateTime.TryParse(startDate, out var start))
        {
            var startDay = start.Date;
            query = query.Where(x => x.vr.VerificationTime >= startDay);
        }
        if (DateTime.TryParse(endDate, out var end))
        {
            var endDay = end.Date.AddDays(1);
            query = query.Where(x => x.vr.VerificationTime < endDay);
        }

        // Keyword search
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = from q in query
                    join u in _dbContext.Users on q.o.UserId equals u.UserId into uj
                    from u in uj.DefaultIfEmpty()
                    where q.o.OrderNo.Contains(kw)
                       || q.detail.ActivityQrcode!.Contains(kw)
                       || (u != null && u.RealName.Contains(kw))
                       || (u != null && u.WxName.Contains(kw))
                       || q.a.Title.Contains(kw)
                       || (q.a.Content ?? string.Empty).Contains(kw)
                    select q;
        }

        var records = await query
            .OrderByDescending(x => x.vr.VerificationTime)
            .ToListAsync(ct);

        var userIds = records.Select(x => x.o.UserId).Distinct().ToList();
        var userMap = userIds.Count > 0
            ? await _dbContext.Users.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct)
            : new Dictionary<int, User>();

        return records.Select(r =>
        {
            userMap.TryGetValue(r.o.UserId, out var u);
            activityTypeMap.TryGetValue(r.a.TypeId, out var dbTypeName);
            var content = r.a.Title ?? "活动券";
            var description = r.a.Description;

            // Determine voucher type by activity type name
            var vt = dbTypeName switch
            {
                "亲子研学" => "parent_child_study",
                "采摘体验" => "pick_experience",
                _ => dbTypeName ?? "activity"
            };

            var typeName = dbTypeName ?? "活动券";
            var verifyTime = r.vr.VerificationTime.ToString("yyyy-MM-dd HH:mm:ss");

            return new VerifyHistoryItem
            {
                Id = $"pcs_{r.vr.RecordId}",
                VoucherType = vt,
                TypeName = typeName,
                CategoryName = dbTypeName,
                OrderNo = r.o.OrderNo,
                UserName = ResolveUserName(u, null, r.o.OrderNo),
                UserPhone = u?.PhoneNumber ?? string.Empty,
                Content = content,
                Description = description,
                ParticipantCount = r.detail.Quantity,
                IsPickupOrder = false,
                DeliveryMethod = null,
                VerifyTime = verifyTime,
                Time = verifyTime,
                CreateTime = r.o.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Status = "verified",
                OrderId = r.o.OrderNo,
                VerifyTimeTicks = r.vr.VerificationTime.Ticks
            };
        }).ToList();
    }

    /// <summary>
    /// 统一核销历史记录数据类
    /// </summary>
    private sealed class VerifyHistoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? OrderNo { get; set; }
        public string? GoodsName { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserPhone { get; set; }
        public string? Content { get; set; }
        public string? Description { get; set; }
        public int ParticipantCount { get; set; }
        public bool IsPickupOrder { get; set; }
        public string? DeliveryMethod { get; set; }
        public string VerifyTime { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string? CreateTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? OrderId { get; set; }
        public long VerifyTimeTicks { get; set; }

        public object ToResponse()
        {
            return new
            {
                id = Id,
                voucherType = VoucherType,
                typeName = TypeName,
                categoryName = CategoryName,
                orderNo = OrderNo,
                goodsName = GoodsName,
                userName = UserName,
                userPhone = UserPhone,
                content = Content,
                description = Description,
                participantCount = ParticipantCount,
                isPickupOrder = IsPickupOrder,
                deliveryMethod = DeliveryMethod,
                verifyTime = VerifyTime,
                time = Time,
                createTime = CreateTime,
                status = Status,
                orderId = OrderId
            };
        }
    }

    /// <summary>
    /// 从 points_commodity_order_status 表查询 status_id 对应的状态名（数据库驱动）
    /// </summary>
    private async Task<string> GetPointsOrderStatusNameAsync(int statusId, CancellationToken ct = default)
    {
        try
        {
            var name = await _dbContext.PointsCommodityOrderStatuses
                .AsNoTracking()
                .Where(s => s.Id == statusId)
                .Select(s => s.StatusName)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch { }

        // 兜底默认映射
        return statusId switch
        {
            1 => "pending",
            2 => "verified",
            3 => "cancelled",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 获取 points_commodity_order_status 表中 "verified" 对应的 ID
    /// </summary>
    private async Task<int> GetVerifiedStatusIdAsync(CancellationToken ct = default)
    {
        try
        {
            var id = await _dbContext.PointsCommodityOrderStatuses
                .AsNoTracking()
                .Where(s => s.StatusName == "verified")
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);

            if (id.HasValue)
                return id.Value;
        }
        catch { }

        return 2; // 默认 verified = 2
    }

    private async Task<User?> GetCurrentStaffAsync(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roleName = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.RoleId == user.RoleId)
            .Select(x => x.RoleName)
            .FirstOrDefaultAsync(cancellationToken);

        return IsStaffRole(roleName) ? user : null;
    }

    /// <summary>
    /// 解析 QR 码前缀，提取纯核销码
    /// "PK_4EQLFVQRV4TQ" → ("PK", "4EQLFVQRV4TQ")
    /// "ACT_QGUC7ZCQFVZM" → ("ACT", "QGUC7ZCQFVZM")
    /// "EXC202605010001" → ("EXC", "EXC202605010001")
    /// </summary>
    private static (string prefix, string code) ParseQrCodePrefix(string raw)
    {
        if (raw.Length > 4 && raw[3] == '_')
        {
            var p = raw[..3];
            if (p == "ACT")
                return (p, raw[4..]);
        }
        if (raw.Length > 3 && raw[2] == '_')
        {
            var p = raw[..2];
            if (p is "PK")
                return (p, raw[3..]);
        }
        if (raw.StartsWith("EXC"))
            return ("EXC", raw);
        return ("", raw);
    }

    private static bool IsStaffRole(string? roleName)
    {
        return !string.IsNullOrWhiteSpace(roleName) &&
               (roleName.Trim().Equals("staff", StringComparison.OrdinalIgnoreCase) ||
                roleName.Contains("员工", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveUserName(User? user, string? contactPerson, string? orderNo)
    {
        if (!string.IsNullOrWhiteSpace(user?.RealName))
        {
            return user.RealName;
        }

        if (!string.IsNullOrWhiteSpace(user?.WxName))
        {
            return user.WxName;
        }

        if (!string.IsNullOrWhiteSpace(contactPerson))
        {
            return contactPerson;
        }

        return "未知用户";
    }

    /// <summary>
    /// 查询券信息（不执行核销），按前缀区分类型
    /// </summary>
    private async Task<ApiResult> BuildVoucherInfoAsync(string prefix, string code, CancellationToken cancellationToken)
    {
        switch (prefix)
        {
            case "PK":
                return await BuildCommodityVoucherInfoAsync(code, cancellationToken);
            case "ACT":
                return await BuildActivityVoucherInfoAsync(code, cancellationToken);
            case "EXC":
                return ApiResult.Success(new { type = "exchange", typeName = "积分兑换", message = "积分兑换无需核销" });
            default:
                // 降级兼容：无前缀旧数据，先试商品再试活动
                var commodity = await BuildCommodityVoucherInfoAsync(code, cancellationToken);
                if (commodity.Code == 0)
                    return commodity;
                return await BuildActivityVoucherInfoAsync(code, cancellationToken);
        }
    }

    /// <summary>
    /// 查询商品自取券信息
    /// </summary>
    private async Task<ApiResult> BuildCommodityVoucherInfoAsync(string code, CancellationToken cancellationToken)
    {
        var commodityOrder = await _dbContext.CommodityOrders
            .FirstOrDefaultAsync(x => x.VerifyCode == code && x.DeliveryMethod == "pickup", cancellationToken);

        if (commodityOrder is null)
            return ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404);

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == commodityOrder.UserId, cancellationToken);

        var canVerify = commodityOrder.OrderStatusId == 8;
        var isVerified = commodityOrder.OrderStatusId == 9;

        return ApiResult.Success(new
        {
            type = "goods_pickup",
            typeName = "商品自取",
            canVerify,
            verified = isVerified,
            alreadyVerified = isVerified,
            userName = ResolveUserName(user, null, commodityOrder.OrderNo),
            userPhone = user?.PhoneNumber ?? commodityOrder.ReceiverPhone ?? string.Empty,
            content = "到店自取商品",
            title = "商品自取",
            orderNo = commodityOrder.OrderNo,
            participantCount = commodityOrder.TotalQuantity
        });
    }

    /// <summary>
    /// 查询活动券信息
    /// </summary>
    private async Task<ApiResult> BuildActivityVoucherInfoAsync(string code, CancellationToken cancellationToken)
    {
        var detail = await _dbContext.ActivityOrderDetails
            .FirstOrDefaultAsync(x => x.ActivityQrcode == code, cancellationToken);

        if (detail is null)
            return ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404);

        var order = await _dbContext.ActivityOrders
            .FirstOrDefaultAsync(x => x.OrderId == detail.ActivityOrderId, cancellationToken);

        if (order is null)
            return ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404);

        var activity = await _dbContext.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsDelete == 0 && x.ActivityId == detail.ActivityId, cancellationToken);

        var activityTypeName = activity is not null
            ? await _dbContext.ActivityTypes
                .AsNoTracking()
                .Where(x => x.ActivityTypeId == activity.TypeId)
                .Select(x => x.TypeName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var voucherType = activity?.TypeId == 2 ? "activity" : "pick";
        var typeName = activityTypeName ?? (voucherType == "activity" ? "活动券" : "采摘券");
        var content = activity?.Title ?? "活动券";

        var voucherUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);

        var canVerifyVoucher = order.OrderStatusId == 2;
        var alreadyVerifiedVoucher = order.OrderStatusId == 3;

        return ApiResult.Success(new
        {
            type = voucherType,
            typeName,
            canVerify = canVerifyVoucher,
            verified = alreadyVerifiedVoucher,
            alreadyVerified = alreadyVerifiedVoucher,
            userName = ResolveUserName(voucherUser, null, order.OrderNo),
            userPhone = voucherUser?.PhoneNumber ?? string.Empty,
            content,
            participantCount = detail.Quantity,
            orderNo = order.OrderNo
        });
    }

    public sealed class VerifyVoucherRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
