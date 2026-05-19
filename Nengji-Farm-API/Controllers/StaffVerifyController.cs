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

            var code = request?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return Ok(ApiResult.Fail("请输入核销码", 400));
            }

            // 先尝试查找商品自取订单（通过 verify_code）
            var commodityOrder = await _dbContext.CommodityOrders
                .FirstOrDefaultAsync(x => x.VerifyCode == code && x.DeliveryMethod == "pickup", cancellationToken);

            if (commodityOrder is not null)
            {
                return await VerifyCommodityPickupAsync(commodityOrder, staff, cancellationToken);
            }

            // 通过 activity_qrcode 查找订单详情
            var detail = await _dbContext.ActivityOrderDetails
                .FirstOrDefaultAsync(x => x.ActivityQrcode == code, cancellationToken);

            if (detail is null)
            {
                return Ok(ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404));
            }

            var order = await _dbContext.ActivityOrders
                .FirstOrDefaultAsync(x => x.OrderId == detail.ActivityOrderId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("未找到该券信息，请确认二维码是否正确", 404));
            }

            var activity = await _dbContext.Activities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsDelete == 0 && x.ActivityId == detail.ActivityId, cancellationToken);

            // 从 activity_type 表动态获取类型名称
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

            // 获取持券人信息
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);

            // activity_order_status: 1=待付款, 2=待核销, 3=已核销, 4=已取消
            if (order.OrderStatusId == 3)
            {
                // 获取上一次的核销时间
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
            {
                return Ok(ApiResult.Fail("该券未支付，无法核销", 403));
            }

            if (order.OrderStatusId == 4)
            {
                return Ok(ApiResult.Fail("该券已取消，无法核销", 403));
            }

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

            // 活动核销完成时发放积分
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
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"核销失败: {ex.Message}"));
        }
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
    /// 获取核销历史
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetVerifyHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? voucherType = "all",
        [FromQuery] string? keyword = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string? categoryName = null,
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

            // 活动核销历史
            var activityRecords = await GetActivityVerifyHistoryAsync(normalizedType, keyword, startDate, endDate, categoryName, page, pageSize, cancellationToken);

            // 商品自取核销历史
            List<object> commodityRecords = [];
            if (normalizedType is "all" or "goods_pickup")
            {
                commodityRecords = await GetCommodityVerifyHistoryAsync(keyword, startDate, endDate, cancellationToken);
            }

            // 积分兑换核销历史
            List<object> pointsExchangeRecords = [];
            if (normalizedType is "all" or "points_exchange")
            {
                pointsExchangeRecords = await GetPointsExchangeVerifyHistoryAsync(keyword, startDate, endDate, cancellationToken);
            }

            // 合并 & 排序
            var allRecords = activityRecords
                .Concat(commodityRecords)
                .Concat(pointsExchangeRecords)
                .OrderByDescending(r => GetVerifyTime(r))
                .ToList();

            var total = allRecords.Count;
            var list = allRecords.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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

    private async Task<List<object>> GetPointsExchangeVerifyHistoryAsync(string? keyword, string? startDate, string? endDate, CancellationToken ct)
    {
        // 获取 verified 状态 ID 及对应的状态名称（从数据库动态获取）
        var verifiedStatusId = await GetVerifiedStatusIdAsync(ct);
        var verifiedStatusName = await GetPointsOrderStatusNameAsync(verifiedStatusId, ct);

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
            return (object)new
            {
                id = $"pex_{r.Id}",
                type = "points_exchange",
                orderNo = r.OrderNo,
                goodsName = c?.Name ?? "积分商品",
                userName = ResolveUserName(u, null, r.OrderNo),
                userPhone = u?.PhoneNumber ?? string.Empty,
                verifyTime = r.VerifyTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                _verifySort = r.VerifyTime?.Ticks ?? 0,
                status = PointsService.StatusToText(verifiedStatusName)
            };
        }).ToList();
    }

    private async Task<List<object>> GetActivityVerifyHistoryAsync(string normalizedType, string? keyword, string? startDate, string? endDate, string? categoryName, int page, int pageSize, CancellationToken ct)
    {
        var activityTypes = await _dbContext.ActivityTypes
            .AsNoTracking()
            .ToDictionaryAsync(x => x.ActivityTypeId, x => x.TypeName, ct);

        var query = from vr in _dbContext.ActivityVerificationRecords
                    join detail in _dbContext.ActivityOrderDetails on vr.ActivityOrderDetailsId equals detail.ActivityOrderDetailsId
                    join o in _dbContext.ActivityOrders on detail.ActivityOrderId equals o.OrderId
                    join a in _dbContext.Activities on detail.ActivityId equals a.ActivityId
                    where a.IsDelete == 0 && o.OrderStatusId == 3
                    select new { vr, detail, o, a };

        if (normalizedType == "pick")
            query = query.Where(x => x.a.TypeId != 2);
        else if (normalizedType == "activity")
            query = query.Where(x => x.a.TypeId == 2);

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var matchedTypeIds = activityTypes
                .Where(t => t.Value == categoryName)
                .Select(t => t.Key)
                .ToList();
            if (matchedTypeIds.Count > 0)
                query = query.Where(x => matchedTypeIds.Contains(x.a.TypeId));
        }

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
                    select q;
        }

        var records = await query
            .OrderByDescending(x => x.vr.VerificationTime)
            .ToListAsync(ct);

        var userIds = records.Select(x => x.o.UserId).Distinct().ToList();
        var userMap = userIds.Count > 0
            ? await _dbContext.Users.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct)
            : new Dictionary<int, User>();

        var activityIds = records.Select(x => x.detail.ActivityId).Distinct().ToList();
        var activityMap = activityIds.Count > 0
            ? await _dbContext.Activities.AsNoTracking().Where(x => x.IsDelete == 0 && activityIds.Contains(x.ActivityId)).ToDictionaryAsync(x => x.ActivityId, ct)
            : new Dictionary<long, ActivityEntity>();

        return records.Select(r =>
        {
            userMap.TryGetValue(r.o.UserId, out var u);
            activityMap.TryGetValue(r.detail.ActivityId, out var act);
            var vt = act?.TypeId == 2 ? "activity" : "pick";
            var dbTypeName = act is not null && activityTypes.TryGetValue(act.TypeId, out var resolvedTypeName) ? resolvedTypeName : null;
            var typeName = dbTypeName ?? (vt == "activity" ? "活动券" : "采摘券");
            var content = act?.Title ?? "活动券";

            return (object)new
            {
                id = $"{r.vr.RecordId}",
                voucherType = vt,
                typeName,
                categoryName = dbTypeName ?? "未分类",
                userName = ResolveUserName(u, null, r.o.OrderNo),
                userPhone = u?.PhoneNumber ?? string.Empty,
                content,
                verifyTime = r.vr.VerificationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                _verifySort = r.vr.VerificationTime.Ticks,
                verified = true,
                orderId = r.o.OrderNo,
                participantCount = r.detail.Quantity
            };
        }).ToList();
    }

    private async Task<List<object>> GetCommodityVerifyHistoryAsync(string? keyword, string? startDate, string? endDate, CancellationToken ct)
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

        // 按 order 分组取商品名称（一个订单可能有多个明细）
        var orderGoodsNames = records
            .GroupBy(x => x.o.OrderId)
            .ToDictionary(g => g.Key, g =>
            {
                var names = g.Where(x => x.d != null && !string.IsNullOrWhiteSpace(x.d.GoodsName))
                             .Select(x => x.d!.GoodsName).Distinct().ToList();
                return names.Count > 0 ? string.Join("、", names) : null;
            });

        return records.GroupBy(x => x.vr.Id).Select(g =>
        {
            var r = g.First();
            userMap.TryGetValue(r.o.UserId, out var u);
            var goodsName = orderGoodsNames.GetValueOrDefault(r.o.OrderId);
            return (object)new
            {
                id = $"cvr_{r.vr.Id}",
                voucherType = "goods_pickup",
                typeName = "商品自取",
                categoryName = "商品自取",
                userName = ResolveUserName(u, null, r.o.OrderNo),
                userPhone = u?.PhoneNumber ?? string.Empty,
                goodsName = goodsName ?? "到店自取商品",
                content = "到店自取商品",
                verifyTime = r.vr.VerifyTime.ToString("yyyy-MM-dd HH:mm:ss"),
                _verifySort = r.vr.VerifyTime.Ticks,
                verified = true,
                orderId = r.o.OrderNo,
                participantCount = r.o.TotalQuantity
            };
        }).ToList();
    }

    private static long GetVerifyTime(object record)
    {
        var prop = record.GetType().GetProperty("_verifySort");
        return prop?.GetValue(record) is long ticks ? ticks : 0;
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

    public sealed class VerifyVoucherRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
