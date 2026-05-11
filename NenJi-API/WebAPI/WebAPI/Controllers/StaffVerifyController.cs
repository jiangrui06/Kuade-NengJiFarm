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
[Route("api/staff-verify")]
public class StaffVerifyController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public StaffVerifyController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
                .FirstOrDefaultAsync(x => x.ActivityId == detail.ActivityId, cancellationToken);

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

            // 检查有效期：使用活动的核销时长（默认 30 天）
            var validDays = activity?.Duration > 0 ? activity.Duration : 30;
            var expireTime = order.CreateTime.AddDays(validDays);
            if (expireTime < DateTime.Now)
            {
                return Ok(ApiResult.Fail($"该券已过期，有效期至 {expireTime:yyyy-MM-dd HH:mm:ss}", 403));
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

            // 加载活动类型字典
            var activityTypes = await _dbContext.ActivityTypes
                .AsNoTracking()
                .ToDictionaryAsync(x => x.ActivityTypeId, x => x.TypeName, cancellationToken);

            // 构建基础查询：关联核销记录、订单详情、活动订单、活动
            var query = from vr in _dbContext.ActivityVerificationRecords
                        join detail in _dbContext.ActivityOrderDetails on vr.ActivityOrderDetailsId equals detail.ActivityOrderDetailsId
                        join o in _dbContext.ActivityOrders on detail.ActivityOrderId equals o.OrderId
                        join a in _dbContext.Activities on detail.ActivityId equals a.ActivityId
                        where o.OrderStatusId == 3
                        select new { vr, detail, o, a };

            // 券类型筛选
            var normalizedType = (voucherType ?? "all").Trim().ToLowerInvariant();
            if (normalizedType == "pick")
            {
                query = query.Where(x => x.a.TypeId != 2);
            }
            else if (normalizedType == "activity")
            {
                query = query.Where(x => x.a.TypeId == 2);
            }

            // 活动分类筛选
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var matchedTypeIds = activityTypes
                    .Where(t => t.Value == categoryName)
                    .Select(t => t.Key)
                    .ToList();
                if (matchedTypeIds.Count > 0)
                {
                    query = query.Where(x => matchedTypeIds.Contains(x.a.TypeId));
                }
            }

            // 日期范围筛选
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

            // 关键词搜索（支持核销码、用户名搜索）
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

            // 统计总数
            var total = await query.CountAsync(cancellationToken);

            // 获取分页数据
            var records = await query
                .OrderByDescending(x => x.vr.VerificationTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // 批量加载关联数据
            var userIds = records.Select(x => x.o.UserId).Distinct().ToList();
            var userMap = userIds.Count > 0
                ? await _dbContext.Users.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, cancellationToken)
                : new Dictionary<int, User>();

            var activityIds = records.Select(x => x.detail.ActivityId).Distinct().ToList();
            var activityMap = activityIds.Count > 0
                ? await _dbContext.Activities.AsNoTracking().Where(x => activityIds.Contains(x.ActivityId)).ToDictionaryAsync(x => x.ActivityId, cancellationToken)
                : new Dictionary<long, ActivityEntity>();

            var list = records.Select(r =>
            {
                userMap.TryGetValue(r.o.UserId, out var u);
                activityMap.TryGetValue(r.detail.ActivityId, out var act);

                var vt = act?.TypeId == 2 ? "activity" : "pick";
                var dbTypeName = act is not null && activityTypes.TryGetValue(act.TypeId, out var resolvedTypeName) ? resolvedTypeName : null;
                var typeName = dbTypeName ?? (vt == "activity" ? "活动券" : "采摘券");
                var content = act?.Title ?? "活动券";

                return new
                {
                    id = r.vr.RecordId.ToString(),
                    voucherType = vt,
                    typeName,
                    categoryName = dbTypeName ?? "未分类",
                    userName = ResolveUserName(u, null, r.o.OrderNo),
                    userPhone = u?.PhoneNumber ?? string.Empty,
                    content,
                    verifyTime = r.vr.VerificationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    verified = true,
                    orderId = r.o.OrderNo,
                    participantCount = r.detail.Quantity
                };
            }).ToList();

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
