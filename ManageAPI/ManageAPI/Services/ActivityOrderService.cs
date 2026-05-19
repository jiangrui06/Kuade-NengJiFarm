using Microsoft.EntityFrameworkCore;

using ManageAPI.Common;
using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

public class ActivityOrderService : IActivityOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IWeChatPayService _weChatPayService;
    private readonly ILogger<ActivityOrderService> _logger;

    public ActivityOrderService(AppDbContext dbContext, IWeChatPayService weChatPayService, ILogger<ActivityOrderService> logger)
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
        else
        {
            query = query.Where(o => o.OrderStatusId == 2);
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

    public async Task<ActivityOrderRefundResponse> RefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default)
    {
        if (request.OrderId <= 0)
            throw new BusinessException("请求参数不完整：orderId 不能为空", 400);

        var order = await _dbContext.Set<ActivityOrder>()
            .Include(o => o.OrderStatus)
            .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

        if (order is null)
            throw new BusinessException("订单不存在或已被删除", 404);

        // 幂等性检查：是否已退款
        var existingRefund = await _dbContext.RefundRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderNo == order.OrderNo && r.OrderType == "activity", cancellationToken);

        if (existingRefund is not null)
            throw new BusinessException("该订单已完成退款，请勿重复操作", 422);

        // 检查订单状态
        if (order.OrderStatusId == 4)
            throw new BusinessException("该订单已完成退款，请勿重复操作", 422);

        if (order.OrderStatusId is not (2 or 3))
            throw new BusinessException("当前订单状态不允许退款（仅已支付订单可退款）", 422);

        // 调用微信退款
        if (!string.IsNullOrWhiteSpace(order.WxPayNo) &&
            !order.WxPayNo.StartsWith("MOCK_", StringComparison.Ordinal) &&
            !order.WxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal))
        {
            try
            {
                var totalFeeFen = (int)(order.TotalAmount * 100);
                var weChatRequest = new WeChatRefundRequest
                {
                    TransactionId = order.WxPayNo.Trim(),
                    TotalFeeFen = totalFeeFen,
                    RefundFeeFen = totalFeeFen,
                    RefundDesc = request.RefundReason,
                };

                await _weChatPayService.ProcessRefundAsync(weChatRequest, cancellationToken);
                _logger.LogInformation("微信退款成功 - OrderNo: {OrderNo}, TransactionId: {TransactionId}", order.OrderNo, order.WxPayNo);
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
            Reason = "admin_refund",
            Description = request.RefundReason,
            RefundAmount = order.TotalAmount,
            Status = "completed",
            AdminReply = operatorName,
            ProcessTime = now,
            CreateTime = now,
        };

        _dbContext.RefundRecords.Add(refund);

        // 更新订单状态为已退款
        order.OrderStatusId = 4;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("退款成功 - RefundNo: {RefundNo}, OrderNo: {OrderNo}, Amount: {Amount}, Operator: {Operator}",
            refundNo, order.OrderNo, order.TotalAmount, operatorName);

        return new ActivityOrderRefundResponse
        {
            RefundId = refundNo,
            OrderId = order.OrderNo,
            RefundAmount = order.TotalAmount.ToString("F2"),
            RefundTime = now.ToString("yyyy-MM-dd HH:mm"),
            Operator = operatorName,
        };
    }

    private static string GenerateRefundNo()
    {
        return $"RF{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }
}
