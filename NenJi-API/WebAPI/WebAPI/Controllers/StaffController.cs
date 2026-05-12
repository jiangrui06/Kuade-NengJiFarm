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
[Route("api/staff")]
public class StaffController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public StaffController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("today-stats")]
    public async Task<IActionResult> TodayStats(CancellationToken cancellationToken)
    {
        var staff = await GetCurrentStaffAsync(cancellationToken);
        if (staff is null)
        {
            return Ok(ApiResult.Fail("无权限，仅员工可访问", 403));
        }

        var start = DateTime.Today;
        var end = start.AddDays(1);

        var activityVerified = await _dbContext.ActivityVerificationRecords
            .AsNoTracking()
            .CountAsync(x => x.VerificationTime >= start && x.VerificationTime < end, cancellationToken);

        var pendingCount = await _dbContext.ActivityOrders
            .AsNoTracking()
            .CountAsync(x => x.OrderStatusId == 2, cancellationToken);

        var lastVerifyTime = await _dbContext.ActivityVerificationRecords
            .AsNoTracking()
            .Where(x => x.VerificationTime >= start && x.VerificationTime < end)
            .OrderByDescending(x => x.VerificationTime)
            .Select(x => (DateTime?)x.VerificationTime)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            todayVerified = activityVerified,
            pendingCount,
            activityVerified,
            pickingVerified = activityVerified,
            today_verify_count = activityVerified,
            last_verify_time = lastVerifyTime?.ToString("yyyy-MM-dd HH:mm:ss"),
            staff_real_name = staff.RealName ?? staff.WxName ?? "员工"
        }));
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyVoucherRequest? request, CancellationToken cancellationToken)
    {
        var staff = await GetCurrentStaffAsync(cancellationToken);
        if (staff is null)
        {
            return Ok(ApiResult.Fail("无权限，仅员工可执行核销", 403));
        }

        var code = NormalizeVoucherCode(request?.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return Ok(ApiResult.Fail("券码不能为空", 400));
        }

        var order = await FindVoucherOrderAsync(code, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Fail("未找到该券码", 404));
        }

        // 先加载活动详情信息（用于已核销和待核销的展示）
        var detailForVerify = await _dbContext.ActivityOrderDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ActivityOrderId == order.OrderId, cancellationToken);
        var activity = detailForVerify is not null
            ? await _dbContext.Activities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ActivityId == detailForVerify.ActivityId, cancellationToken)
            : null;

        // 已核销：返回核销信息和已核销状态
        if (order.OrderStatusId == 3)
        {
            var existingUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);
            var existingTitle = activity?.Title ?? "活动券";

            return Ok(ApiResult.Success(new
            {
                verified = true,
                alreadyVerified = true,
                voucherId = order.OrderId.ToString(),
                voucherType = activity?.TypeId == 2 ? "activity" : "pick",
                userName = ResolveUserName(existingUser),
                userPhone = MaskPhone(existingUser?.PhoneNumber),
                content = existingTitle,
                title = existingTitle,
                participantCount = detailForVerify?.Quantity ?? 1,
                order_id = order.OrderNo,
                message = "该券已核销"
            }));
        }

        if (order.OrderStatusId == 4 || order.OrderStatusId == 1)
        {
            return Ok(ApiResult.Fail("该券未支付或已取消，无法核销", 403));
        }

        if (order.OrderStatusId != 2)
        {
            return Ok(ApiResult.Fail("该券状态不支持核销", 409));
        }

order.OrderStatusId = 3;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 写入核销记录表
        _dbContext.ActivityVerificationRecords.Add(new ActivityVerificationRecord
        {
            ActivityOrderDetailsId = detailForVerify?.ActivityOrderDetailsId ?? 0,
            VerificationTime = DateTime.Now
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);

        var voucherType = activity?.TypeId == 2 ? "activity" : "pick";
        var title = activity?.Title ?? "活动券";
        var verifyTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return Ok(ApiResult.Success(new
        {
            verified = true,
            alreadyVerified = false,
            success = true,
            voucherId = order.OrderId.ToString(),
            voucherType,
            userName = ResolveUserName(user),
            userPhone = MaskPhone(user?.PhoneNumber),
            content = title,
            verifyTime,
            participantCount = detailForVerify?.Quantity ?? 1,
            voucher_id = order.OrderId.ToString(),
            voucher_type = voucherType,
            title,
            user_name = ResolveUserName(user),
            user_phone = MaskPhone(user?.PhoneNumber),
            order_id = order.OrderNo,
            verify_time = verifyTime
        }, "核销成功"));
    }

    [HttpGet("vouchers")]
    public async Task<IActionResult> Vouchers(
        [FromQuery] string? type,
        [FromQuery] string? status = "unused",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var staff = await GetCurrentStaffAsync(cancellationToken);
        if (staff is null)
        {
            return Ok(ApiResult.Fail("无权限，仅员工可访问", 403));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normalizedType = NormalizeVoucherType(type);
        if (normalizedType == "picking")
        {
            return Ok(ApiResult.Success(new { total = 0, page, pageSize, list = new List<object>() }));
        }

        var normalizedStatus = (status ?? "unused").Trim().ToLowerInvariant();

        // "expired" 需要从活动表读取实际 Duration，在内存中过滤
        if (normalizedStatus == "expired")
        {
            var allQuery = _dbContext.ActivityOrders.AsNoTracking();
            var allOrders = await allQuery.OrderByDescending(x => x.CreateTime).ToListAsync(cancellationToken);
            var allOrderIds = allOrders.Select(x => x.OrderId).Distinct().ToList();
            var allDurationMap = await LoadVoucherDurationMapAsync(allOrderIds, cancellationToken);
            var allActivityTypeMap = await LoadVoucherActivityTypeMapAsync(allOrderIds, cancellationToken);

            var expiredOrders = allOrders
                .Where(o => o.OrderStatusId != 3
                    && GetExpireTime(o, allDurationMap.GetValueOrDefault(o.OrderId, 30)) < DateTime.Now)
                .ToList();

            var total = expiredOrders.Count;
            var pageOrders = expiredOrders.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var userMap = await LoadUserMapAsync(pageOrders, cancellationToken);

            var list = pageOrders.Select(order => BuildVoucherListItem(
                order, userMap, allActivityTypeMap.GetValueOrDefault(order.OrderId),
                allDurationMap.GetValueOrDefault(order.OrderId))).ToList();

            return Ok(ApiResult.Success(new { total, page, pageSize, list }));
        }

        // 其他状态走原 IQueryable 过滤
        var query = _dbContext.ActivityOrders.AsNoTracking();
        query = ApplyVoucherStatusFilter(query, normalizedStatus);

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userMapNormal = await LoadUserMapAsync(orders, cancellationToken);
        var orderIds = orders.Select(x => x.OrderId).Distinct().ToList();
        var activityTypeMap = await LoadVoucherActivityTypeMapAsync(orderIds, cancellationToken);
        var durationMap = await LoadVoucherDurationMapAsync(orderIds, cancellationToken);
        var listNormal = orders.Select(order => BuildVoucherListItem(
            order, userMapNormal, activityTypeMap.GetValueOrDefault(order.OrderId),
            durationMap.GetValueOrDefault(order.OrderId))).ToList();

        return Ok(ApiResult.Success(new
        {
            total = totalCount,
            page,
            pageSize,
            list = listNormal
        }));
    }

    [HttpGet("verify-history")]
    public async Task<IActionResult> VerifyHistory(
        [FromQuery] bool today = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var staff = await GetCurrentStaffAsync(cancellationToken);
        if (staff is null)
        {
            return Ok(ApiResult.Fail("无权限，仅员工可访问", 403));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // 从核销记录表关联查询，以 VerificationTime 为时间基准
        var query = from vr in _dbContext.ActivityVerificationRecords
                    join detail in _dbContext.ActivityOrderDetails on vr.ActivityOrderDetailsId equals detail.ActivityOrderDetailsId
                    join o in _dbContext.ActivityOrders on detail.ActivityOrderId equals o.OrderId
                    where o.OrderStatusId == 3
                    select new { Record = vr, Detail = detail, Order = o };

        if (today)
        {
            var start = DateTime.Today;
            query = query.Where(x => x.Record.VerificationTime >= start && x.Record.VerificationTime < start.AddDays(1));
        }
        else
        {
            if (DateTime.TryParse(startDate, out var start))
            {
                query = query.Where(x => x.Record.VerificationTime >= start.Date);
            }
            if (DateTime.TryParse(endDate, out var end))
            {
                query = query.Where(x => x.Record.VerificationTime < end.Date.AddDays(1));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Record.VerificationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userMap = await LoadUserMapAsync(items.Select(x => x.Order), cancellationToken);
        var orderIds = items.Select(x => x.Order.OrderId).Distinct().ToList();
        var activityTypeMap = await LoadVoucherActivityTypeMapAsync(orderIds, cancellationToken);
        var detailQuantityMap = items.ToDictionary(x => x.Order.OrderId, x => x.Detail.Quantity);
        var list = items.Select(item => BuildHistoryItem(item.Order, userMap, staff, item.Record, activityTypeMap.GetValueOrDefault(item.Order.OrderId), detailQuantityMap.GetValueOrDefault(item.Order.OrderId))).ToList();

        return Ok(ApiResult.Success(new
        {
            total,
            page,
            pageSize,
            list
        }));
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

    private async Task<ActivityOrder?> FindVoucherOrderAsync(string code, CancellationToken cancellationToken)
    {
        if (long.TryParse(code, out var orderId) && orderId > 0)
        {
            return await _dbContext.ActivityOrders
                .FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        }

        return await _dbContext.ActivityOrders
            .FirstOrDefaultAsync(x => x.OrderNo == code, cancellationToken);
    }

    private static IQueryable<ActivityOrder> ApplyVoucherStatusFilter(IQueryable<ActivityOrder> query, string? status)
    {
        return (status ?? "unused").Trim().ToLowerInvariant() switch
        {
            "used" => query.Where(x => x.OrderStatusId == 3),
            "all" => query,
            _ => query.Where(x => x.OrderStatusId == 2)
        };
    }

    private async Task<Dictionary<int, User>> LoadUserMapAsync(IEnumerable<ActivityOrder> orders, CancellationToken cancellationToken)
    {
        var userIds = orders.Select(x => x.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return new Dictionary<int, User>();
        }

        return await _dbContext.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
    }

    private static object BuildVoucherListItem(ActivityOrder order, IReadOnlyDictionary<int, User> userMap, string? activityTypeName = null, int durationDays = 30)
    {
        userMap.TryGetValue(order.UserId, out var user);
        var status = MapVoucherStatus(order, durationDays);
        var vt = activityTypeName is not null && activityTypeName.Contains("活动") ? "activity" : "pick";
        var title = activityTypeName ?? "活动券";

        return new
        {
            voucherId = order.OrderId.ToString(),
            voucherType = vt,
            title,
            userName = ResolveUserName(user),
            userPhone = MaskPhone(user?.PhoneNumber),
            orderId = order.OrderId.ToString(),
            status,
            expireTime = GetExpireTime(order, durationDays).ToString("yyyy-MM-dd"),
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            voucher_id = order.OrderId.ToString(),
            voucher_type = vt,
            user_name = ResolveUserName(user),
            user_phone = MaskPhone(user?.PhoneNumber),
            order_id = order.OrderId.ToString(),
            expire_time = GetExpireTime(order, durationDays).ToString("yyyy-MM-dd"),
            create_time = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    private static object BuildHistoryItem(ActivityOrder order, IReadOnlyDictionary<int, User> userMap, User staff, ActivityVerificationRecord? record = null, string? activityTypeName = null, int participantCount = 1)
    {
        userMap.TryGetValue(order.UserId, out var user);
        var verifyTime = (record?.VerificationTime ?? order.CreateTime).ToString("yyyy-MM-dd HH:mm:ss");
        var vt = activityTypeName is not null && activityTypeName.Contains("活动") ? "activity" : "pick";
        var title = activityTypeName ?? "活动券";

        return new
        {
            id = order.OrderId.ToString(),
            verifyId = order.OrderId.ToString(),
            voucherType = vt,
            title,
            userName = ResolveUserName(user),
            verifyTime,
            verifyStaff = ResolveUserName(staff),
            participantCount,
            verify_id = order.OrderId.ToString(),
            voucher_type = vt,
            user_name = ResolveUserName(user),
            verify_time = verifyTime,
            verify_staff = ResolveUserName(staff)
        };
    }

    private async Task<Dictionary<long, string>> LoadVoucherActivityTypeMapAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0) return [];

        var details = await _dbContext.ActivityOrderDetails
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.ActivityOrderId))
            .ToListAsync(cancellationToken);

        var activityIds = details.Select(x => x.ActivityId).Distinct().ToList();
        if (activityIds.Count == 0) return [];

        var activityTypes = await (
            from a in _dbContext.Activities.AsNoTracking()
            join t in _dbContext.ActivityTypes.AsNoTracking() on a.TypeId equals t.ActivityTypeId
            where activityIds.Contains(a.ActivityId)
            select new { a.ActivityId, t.TypeName }
        ).ToListAsync(cancellationToken);

        var activityTypeMap = activityTypes.ToDictionary(x => x.ActivityId, x => x.TypeName);

        return details
            .GroupBy(x => x.ActivityOrderId)
            .ToDictionary(g => g.Key, g =>
            {
                var firstActivity = g.FirstOrDefault();
                return firstActivity is not null && activityTypeMap.TryGetValue(firstActivity.ActivityId, out var name)
                    ? name
                    : "活动券";
            });
    }

    private async Task<Dictionary<long, int>> LoadVoucherDurationMapAsync(
        IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0) return [];

        var details = await _dbContext.ActivityOrderDetails
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.ActivityOrderId))
            .ToListAsync(cancellationToken);

        var activityIds = details.Select(x => (int)x.ActivityId).Distinct().ToList();
        if (activityIds.Count == 0) return [];

        var activityDurations = await _dbContext.Activities
            .AsNoTracking()
            .Where(x => activityIds.Contains((int)x.ActivityId))
            .ToDictionaryAsync(x => (int)x.ActivityId, x => x.Duration, cancellationToken);

        return details
            .GroupBy(x => x.ActivityOrderId)
            .ToDictionary(g => g.Key, g =>
            {
                var first = g.FirstOrDefault();
                return first is not null && activityDurations.TryGetValue((int)first.ActivityId, out var dur) && dur > 0
                    ? dur
                    : 30;
            });
    }

    private static string NormalizeVoucherCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = Uri.UnescapeDataString(raw.Trim());
        if (value.Contains("verifyCode=", StringComparison.OrdinalIgnoreCase))
        {
            var query = value.Split('?', 2).Last();
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0].Equals("verifyCode", StringComparison.OrdinalIgnoreCase))
                {
                    value = Uri.UnescapeDataString(parts[1]);
                    break;
                }
            }
        }

        foreach (var prefix in new[] { "ACT-", "PICK-" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var withoutPrefix = value[prefix.Length..];
                var lastDash = withoutPrefix.LastIndexOf('-');
                return lastDash > 0 ? withoutPrefix[..lastDash] : withoutPrefix;
            }
        }

        return value;
    }

    private static string NormalizeVoucherType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return string.Empty;
        return type.Trim().ToLowerInvariant() switch
        {
            "activity" => "activity",
            "picking" or "pick" or "acre" => "picking",
            _ => type.Trim().ToLowerInvariant()
        };
    }

    private static string MapVoucherStatus(ActivityOrder order, int durationDays = 30)
    {
        if (order.OrderStatusId == 3) return "used";
        if (GetExpireTime(order, durationDays) < DateTime.Now) return "expired";
        return "unused";
    }

    private static DateTime GetExpireTime(ActivityOrder order, int durationDays = 30)
    {
        return order.CreateTime.AddDays(durationDays);
    }

    private static bool IsStaffRole(string? roleName)
    {
        return !string.IsNullOrWhiteSpace(roleName) &&
               (roleName.Trim().Equals("staff", StringComparison.OrdinalIgnoreCase) ||
                roleName.Contains("员工", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveUserName(User? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.RealName)) return user.RealName;
        if (!string.IsNullOrWhiteSpace(user?.WxName)) return user.WxName;
        return "未知用户";
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7) return phone ?? string.Empty;
        return $"{phone[..3]}****{phone[^4..]}";
    }

    public sealed class VerifyVoucherRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
