using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

public class ActivityOrderService : IActivityOrderService
{
    private readonly ManageAppDbContext _dbContext;
    private readonly IWeChatPayService _weChatPayService;
    private readonly ILogger<ActivityOrderService> _logger;

    public ActivityOrderService(ManageAppDbContext dbContext, IWeChatPayService weChatPayService, ILogger<ActivityOrderService> logger)
    {
        _dbContext = dbContext;
        _weChatPayService = weChatPayService;
        _logger = logger;
    }

    public async Task<(List<ActivityOrderListItemDto> Records, int Total)> GetOrderListAsync(
        int pageNum, int pageSize, string? keyword, int? statusId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<ActivityOrder>()
            .AsNoTracking()
            .Include(o => o.OrderStatus)
            .Include(o => o.ActivityOrderDetails)
                .ThenInclude(d => d.Activity)
            .AsQueryable();

        if (statusId.HasValue)
        {
            query = query.Where(o => o.OrderStatusId == statusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(o =>
                o.OrderNo.Contains(kw) ||
                o.ActivityOrderDetails.Any(d => d.Activity.Title.Contains(kw)));
        }

        var total = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(o => o.CreateTime)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = orders.Select(o => o.UserId).Distinct().ToList();
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, cancellationToken);

        var records = orders.Select(o =>
        {
            var firstDetail = o.ActivityOrderDetails.FirstOrDefault();
            return new ActivityOrderListItemDto
            {
                OrderId = o.OrderId,
                OrderNo = o.OrderNo,
                TotalAmount = o.TotalAmount,
                TotalQuantity = o.TotalQuantity,
                OrderStatusId = o.OrderStatusId,
                StatusName = o.OrderStatus?.StatusName ?? string.Empty,
                UserId = o.UserId,
                UserName = users.GetValueOrDefault(o.UserId)?.WxName ?? users.GetValueOrDefault(o.UserId)?.RealName,
                ActivityTitle = firstDetail?.Activity?.Title,
                CreateTime = o.CreateTime.ToString("yyyy-MM-dd HH:mm"),
            };
        }).ToList();

        return (records, total);
    }

    public async Task<ActivityOrderFullDetailDto?> GetOrderDetailAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Set<ActivityOrder>()
            .AsNoTracking()
            .Include(o => o.OrderStatus)
            .Include(o => o.ActivityOrderDetails)
                .ThenInclude(d => d.Activity)
            .Include(o => o.ActivityOrderDetails)
                .ThenInclude(d => d.ActivityVerificationRecords)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

        if (order is null)
            return null;

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == order.UserId, cancellationToken);

        var items = order.ActivityOrderDetails.Select(d => new ActivityOrderItemDto
        {
            ActivityOrderDetailsId = d.ActivityOrderDetailsId,
            ActivityId = d.ActivityId,
            ActivityTitle = d.Activity?.Title ?? string.Empty,
            ActivityImage = d.Activity?.ImageUrl,
            ActivityDescription = d.Activity?.Description,
            ActivityLocation = d.Activity?.Location,
            UnitPrice = d.UnitPrice,
            Quantity = d.Quantity,
            SubtotalAmount = d.SubtotalAmount,
            ActivityQrcode = d.ActivityQrcode,
            IsVerified = d.ActivityVerificationRecords.Count > 0,
            VerificationTime = d.ActivityVerificationRecords
                .OrderByDescending(v => v.VerificationTime)
                .FirstOrDefault()?.VerificationTime.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();

        return new ActivityOrderFullDetailDto
        {
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            WxPayNo = order.WxPayNo,
            TotalAmount = order.TotalAmount,
            TotalQuantity = order.TotalQuantity,
            OrderStatusId = order.OrderStatusId,
            StatusName = order.OrderStatus?.StatusName ?? string.Empty,
            UserId = order.UserId,
            UserName = user?.WxName ?? user?.RealName,
            UserPhone = user?.PhoneNumber,
            CreateTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm"),
            Items = items,
        };
    }

    public async Task<bool> VerifyOrderDetailAsync(long activityOrderDetailsId, CancellationToken cancellationToken = default)
    {
        var detail = await _dbContext.ActivityOrderDetails
            .Include(d => d.ActivityOrder)
            .Include(d => d.ActivityVerificationRecords)
            .FirstOrDefaultAsync(d => d.ActivityOrderDetailsId == activityOrderDetailsId, cancellationToken);

        if (detail is null)
            return false;

        if (detail.ActivityOrder.OrderStatusId != 2)
            throw new InvalidOperationException($"当前订单状态不允许核销（状态ID: {detail.ActivityOrder.OrderStatusId}，仅待核销状态可操作）");

        if (detail.ActivityVerificationRecords.Count > 0)
            throw new InvalidOperationException("该明细已核销，不可重复核销");

        var record = new ActivityVerificationRecord
        {
            ActivityOrderDetailsId = detail.ActivityOrderDetailsId,
            VerificationTime = DateTime.Now,
        };

        _dbContext.Set<ActivityVerificationRecord>().Add(record);

        var order = detail.ActivityOrder;
        var allDetailsForOrder = await _dbContext.ActivityOrderDetails
            .Include(d => d.ActivityVerificationRecords)
            .Where(d => d.ActivityOrderId == order.OrderId)
            .ToListAsync(cancellationToken);

        var allVerified = allDetailsForOrder.All(d =>
            d.ActivityOrderDetailsId == activityOrderDetailsId
                ? true
                : d.ActivityVerificationRecords.Count > 0);

        if (allVerified)
        {
            order.OrderStatusId = 3;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("核销成功 - DetailId: {DetailId}, OrderId: {OrderId}, AllVerified: {AllVerified}",
            activityOrderDetailsId, order.OrderId, allVerified);

        return true;
    }

    public async Task<(bool Success, string Message)> VerifyByOrderIdAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Set<ActivityOrder>()
            .Include(o => o.ActivityOrderDetails)
                .ThenInclude(d => d.ActivityVerificationRecords)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

        if (order is null)
            return (false, "订单不存在");

        if (order.OrderStatusId != 2)
            return (false, order.OrderStatusId switch
            {
                1 => "该订单未支付，无法核销",
                3 => "该订单已全部核销",
                4 => "该订单已退款，无法核销",
                _ => $"当前订单状态不允许核销（状态ID: {order.OrderStatusId}）",
            });

        // 找第一个未核销的明细
        var unverifiedDetail = order.ActivityOrderDetails
            .FirstOrDefault(d => d.ActivityVerificationRecords.Count == 0);

        if (unverifiedDetail is null)
            return (false, "该订单所有明细已核销");

        // 复用现有的核销逻辑
        var result = await VerifyOrderDetailAsync(unverifiedDetail.ActivityOrderDetailsId, cancellationToken);
        return (result, result ? "核销成功" : "核销失败");
    }

    public async Task<ActivityOrderRefundResponse> RefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNo) && request.OrderId <= 0)
            throw new BusinessException("请求参数不完整：orderNo 或 orderId 必须提供", 400);

        ActivityOrder? order;
        if (!string.IsNullOrWhiteSpace(request.OrderNo))
            order = await _dbContext.Set<ActivityOrder>()
                .Include(o => o.OrderStatus)
                .FirstOrDefaultAsync(o => o.OrderNo == request.OrderNo, cancellationToken);
        else
            order = await _dbContext.Set<ActivityOrder>()
                .Include(o => o.OrderStatus)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

        if (order is null)
            throw new BusinessException("订单不存在或已被删除", 404);

        // 动态加载订单状态映射
        var statusMap = await OrderStatusHelper.LoadActivityOrderStatusMapAsync(_dbContext, cancellationToken);
        var statusCancelled = statusMap.Require("已取消", "activity_order_status");
        var statusVerified = statusMap.Require("已核销", "activity_order_status");
        var statusPendingVerify = statusMap.Require("待核销", "activity_order_status");
        var statusRefunding = statusMap.Require("退款中", "activity_order_status");
        var statusRefunded = statusMap.Require("已退款", "activity_order_status");

        // 幂等性检查：是否已有待处理或已完成的退款
        var existingRefund = await _dbContext.RefundRecords
            .AsNoTracking()
            .Where(r => r.OrderNo == order.OrderNo && r.OrderType == "activity" && r.Status == "completed")
            .FirstOrDefaultAsync(cancellationToken);

        if (existingRefund is not null)
            throw new BusinessException("该订单已完成退款，请勿重复操作", 422);

        // 检查订单状态
        if (order.OrderStatusId == statusCancelled)
            throw new BusinessException("该订单已取消，无法退款", 422);

        // 允许退款的状态：已核销、待核销、退款中
        if (order.OrderStatusId != statusPendingVerify &&
            order.OrderStatusId != statusVerified &&
            order.OrderStatusId != statusRefunding)
            throw new BusinessException("当前订单状态不允许退款", 422);

        // 保存退款前状态
        var prevStatusId = order.OrderStatusId;
        order.OrderStatusId = statusRefunding;

        // 调用微信退款
        if (!string.IsNullOrWhiteSpace(order.WxPayNo) &&
            !order.WxPayNo.StartsWith("MOCK_", StringComparison.Ordinal) &&
            !order.WxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal) &&
            order.WxPayNo.All(char.IsDigit))
        {
            try
            {
                var totalFeeFen = (int)(order.TotalAmount * 100);
                var weChatRequest = new WeChatRefundRequest
                {
                    OutTradeNo = order.OrderNo,
                    TotalFeeFen = totalFeeFen,
                    RefundFeeFen = totalFeeFen,
                    RefundDesc = request.RefundReason,
                };

                await _weChatPayService.ProcessRefundAsync(weChatRequest, cancellationToken);
                _logger.LogInformation("微信退款成功 - OrderNo: {OrderNo}", order.OrderNo, order.WxPayNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信退款失败 - OrderNo: {OrderNo}, WxPayNo: {WxPayNo}", order.OrderNo, order.WxPayNo);
                throw new BusinessException($"微信退款失败：{ex.Message}", 500);
            }
        }

        var now = DateTime.Now;
        var refundNo = GenerateRefundNo();

        var refund = new RefundRecord
        {
            RefundNo = refundNo,
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            OrderType = "activity",
            UserId = order.UserId,
            Reason = "管理员退款",
            Description = $"prev_status_id:{prevStatusId}|{request.RefundReason}",
            RefundAmount = order.TotalAmount,
            Status = "pending",
            AdminReply = operatorName,
            ProcessTime = now,
            CreateTime = now,
        };

        _dbContext.RefundRecords.Add(refund);

        // 订单已改为退款中状态（在状态检查时已改）
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("退款申请已提交 - OrderNo: {OrderNo}, Operator: {Operator}",
            order.OrderNo, operatorName);

        return new ActivityOrderRefundResponse
        {
            RefundId = refundNo,
            OrderId = order.OrderNo,
            RefundAmount = order.TotalAmount.ToString("F2"),
            RefundTime = now.ToString("yyyy-MM-dd HH:mm"),
            Operator = operatorName,
        };
    }

    public async Task<ActivityOrderRejectResponse> RejectRefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefundId))
            throw new BusinessException("refundId 不能为空", 400);

        // 查找退款记录（支持 refundId 主键或 refundNo 退款编号）
        RefundRecord? refund = null;
        if (long.TryParse(request.RefundId, out var refundId))
        {
            refund = await _dbContext.RefundRecords
                .FirstOrDefaultAsync(r => r.RefundId == refundId, cancellationToken);
        }

        refund ??= await _dbContext.RefundRecords
            .FirstOrDefaultAsync(r => r.RefundNo == request.RefundId, cancellationToken);

        if (refund is null)
            throw new BusinessException("退款记录不存在", 404);

        if (refund.Status == "completed")
            throw new BusinessException("该退款已处理完成，无法驳回", 422);

        if (refund.Status == "rejected")
            throw new BusinessException("该退款已被驳回，请勿重复操作", 422);

        // 动态加载状态映射
        var rejectStatusMap = await OrderStatusHelper.LoadActivityOrderStatusMapAsync(_dbContext, cancellationToken);
        var rejectPendingVerify = rejectStatusMap.Require("待核销", "activity_order_status");

        // 查找对应订单并恢复状态
        var order = await _dbContext.Set<ActivityOrder>()
            .FirstOrDefaultAsync(o => o.OrderId == refund.OrderId, cancellationToken);

        if (order is not null)
        {
            // 从 Description 中提取退款前状态ID恢复
            var restoredId = ParsePreviousStatusId(refund.Description);
            order.OrderStatusId = restoredId ?? rejectPendingVerify;
        }

        // 更新退款记录
        refund.Status = "rejected";
        refund.AdminReply = request.AdminReply;
        refund.ProcessNote = request.ProcessNote;
        refund.ProcessTime = DateTime.Now;
        refund.UpdateTime = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("退款已驳回 - RefundId: {RefundId}, OrderNo: {OrderNo}, AdminReply: {AdminReply}, Operator: {Operator}",
            refund.RefundId, refund.OrderNo, request.AdminReply, operatorName);

        return new ActivityOrderRejectResponse
        {
            RefundId = refund.RefundId.ToString(),
            Action = "rejected",
            AdminReply = request.AdminReply,
        };
    }

    private static string GenerateRefundNo()
    {
        return $"RF{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    /// <summary>
    /// 从 Description 中提取退款前状态ID（格式: "prev_status_id:3|退款原因"）
    /// </summary>
    private static int? ParsePreviousStatusId(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var prefix = "prev_status_id:";
        var idx = description.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0)
            return null;

        var afterPrefix = description[(idx + prefix.Length)..];
        var pipeIdx = afterPrefix.IndexOf('|');
        var idStr = pipeIdx >= 0 ? afterPrefix[..pipeIdx] : afterPrefix;
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
