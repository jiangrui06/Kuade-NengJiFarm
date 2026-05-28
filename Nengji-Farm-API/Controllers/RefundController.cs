using System.Security.Claims;
using System.Text.Json;
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
public class RefundController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IWeChatPayService _weChatPayService;
    private readonly ILogger<RefundController> _logger;

    public RefundController(AppDbContext dbContext, IWeChatPayService weChatPayService, ILogger<RefundController> logger)
    {
        _dbContext = dbContext;
        _weChatPayService = weChatPayService;
        _logger = logger;
    }

    /// <summary>
    /// 申请退款
    /// </summary>
    [HttpPost("api/orders/{id}/refund")]
    public async Task<IActionResult> Apply(string id, [FromBody] ApplyRefundRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return Ok(ApiResult.Fail("退款原因不能为空", 400));
        }

        if (!s_validReasons.Contains(request.Reason))
        {
            return Ok(ApiResult.Fail("无效的退款原因", 400));
        }

        // "其他原因"时 description 必填
        if (request.Reason == "其他原因" && string.IsNullOrWhiteSpace(request.Description))
        {
            return Ok(ApiResult.Fail("\"其他原因\"需填写具体说明", 400));
        }

        if ((request.Images?.Count ?? 0) > 3)
        {
            return Ok(ApiResult.Fail("凭证图片最多 3 张", 400));
        }

        var userId = GetCurrentUserId();

        var order = await FindOrderAsync(id, userId, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        var existing = await _dbContext.RefundRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == order.OrderId && x.UserId == userId
                && (x.Status == "pending" || x.Status == "approved" || x.Status == "processing"), cancellationToken);

        if (existing is not null)
        {
            return Ok(ApiResult.Fail("该订单已有进行中的退款申请", 409));
        }

        if (!CanApplyRefund(order))
        {
            return Ok(ApiResult.Fail("当前订单状态不允许申请退款", 409));
        }

        var now = DateTime.Now;
        var record = new RefundRecord
        {
            RefundNo = GenerateRefundNo(),
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            OrderType = order.Type,
            UserId = userId,
            Reason = request.Reason,
            Description = request.Description,
            Images = request.Images is { Count: > 0 } ? JsonSerializer.Serialize(request.Images) : null,
            RefundAmount = order.TotalAmount,
            Status = "pending",
            CreateTime = now
        };

        _dbContext.RefundRecords.Add(record);

        // 更新原订单状态为退款中，同时获取支付信息用于微信退款
        string? wxPayNo = null;
        object? orderEntity = null;
        if (order.Type == "goods")
        {
            var goodsEntity = await _dbContext.CommodityOrders.FindAsync(new object[] { order.OrderId }, cancellationToken);
            if (goodsEntity is not null)
            {
                goodsEntity.OrderStatusId = 6;
                wxPayNo = goodsEntity.WxPayNo;
                orderEntity = goodsEntity;
            }
        }
        else if (order.Type == "food")
        {
            var foodEntity = await _dbContext.DishOrders.FindAsync(new object[] { order.OrderId }, cancellationToken);
            if (foodEntity is not null)
            {
                foodEntity.OrderStatusId = 5;
                wxPayNo = foodEntity.WxPayNo;
                orderEntity = foodEntity;
            }
        }
        else if (order.Type == "activity")
        {
            var actEntity = await _dbContext.ActivityOrders.FindAsync(new object[] { order.OrderId }, cancellationToken);
            if (actEntity is not null)
            {
                actEntity.OrderStatusId = 5;
                wxPayNo = actEntity.WxPayNo;
                orderEntity = actEntity;
            }
        }

        // 首次保存：创建退款记录 + 更新订单状态为退款中
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 调用微信支付真实退款
        var weChatRefundSuccess = false;
        if (!string.IsNullOrWhiteSpace(wxPayNo) &&
            !wxPayNo.StartsWith("MOCK_", StringComparison.Ordinal) &&
            !wxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal) &&
            wxPayNo.All(char.IsDigit))
        {
            try
            {
                var totalFeeFen = (int)(order.TotalAmount * 100);
                var weChatRequest = new WeChatRefundRequest
                {
                    OutTradeNo = order.OrderNo,
                    TotalFeeFen = totalFeeFen,
                    RefundFeeFen = totalFeeFen,
                    RefundDesc = request.Reason,
                };

                var refundResult = await _weChatPayService.ProcessRefundAsync(weChatRequest, cancellationToken);
                weChatRefundSuccess = refundResult.IsSuccess;
                _logger.LogInformation("微信退款成功(用户发起) - OrderNo: {OrderNo}, OutRefundNo: {OutRefundNo}",
                    order.OrderNo, refundResult.OutRefundNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信退款失败(用户发起) - OrderNo: {OrderNo}", order.OrderNo);
                // 不抛异常：退款申请保留为 pending，等待管理员处理
            }
        }

        // 微信退款成功 → 标记退款完成 + 订单改为已退款
        if (weChatRefundSuccess)
        {
            record.Status = "completed";
            record.ProcessTime = DateTime.Now;

            if (order.Type == "goods" && orderEntity is Entities.CommodityOrder g)
                g.OrderStatusId = 7;
            else if (order.Type == "food" && orderEntity is Entities.DishOrder f)
                f.OrderStatusId = 6;
            else if (order.Type == "activity" && orderEntity is Entities.ActivityOrder a)
                a.OrderStatusId = 6;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResult.Success(new
        {
            refundId = record.RefundNo,
            orderId = order.OrderId,
            status = record.Status,
            reason = record.Reason,
            description = record.Description ?? string.Empty,
            images = request.Images ?? [],
            refundAmount = record.RefundAmount,
            createTime = record.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            weChatRefundSuccess
        }, weChatRefundSuccess ? "退款成功" : "退款申请已提交"));
    }

    /// <summary>
    /// 查询退款详情
    /// </summary>
    [HttpGet("api/orders/{id}/refund")]
    public async Task<IActionResult> GetDetail(string id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var order = await FindOrderAsync(id, userId, cancellationToken);
        if (order is null)
        {
            return Ok(ApiResult.Success(null, "该订单暂无退款申请"));
        }

        var record = await _dbContext.RefundRecords
            .AsNoTracking()
            .Where(x => x.OrderId == order.OrderId && x.UserId == userId)
            .OrderByDescending(x => x.CreateTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return Ok(ApiResult.Success(null, "该订单暂无退款申请"));
        }

        var parsedImages = string.IsNullOrWhiteSpace(record.Images)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(record.Images) ?? [];

        return Ok(ApiResult.Success(new
        {
            refundId = record.RefundNo,
            orderId = record.OrderId,
            status = record.Status,
            reason = record.Reason,
            description = record.Description ?? string.Empty,
            images = parsedImages,
            refundAmount = record.RefundAmount,
            createTime = record.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            processTime = record.ProcessTime?.ToString("yyyy-MM-dd HH:mm:ss"),
            processNote = record.ProcessNote,
            adminReply = record.AdminReply
        }));
    }

    /// <summary>
    /// 用户退款记录列表
    /// </summary>
    [HttpGet("api/orders/refunds")]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var userId = GetCurrentUserId();
        var query = _dbContext.RefundRecords.AsNoTracking().Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && s_validStatuses.Contains(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var records = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            list = records.Select(x => new
            {
                refundId = x.RefundNo,
                orderId = x.OrderId,
                orderNumber = x.OrderNo,
                status = x.Status,
                reason = x.Reason,
                description = x.Description ?? string.Empty,
                refundAmount = x.RefundAmount,
                orderType = x.OrderType,
                createTime = x.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
            }),
            total,
            page,
            pageSize
        }));
    }

    /// <summary>
    /// 取消退款申请
    /// </summary>
    [HttpPut("api/orders/{id}/refund/cancel")]
    public async Task<IActionResult> Cancel(string id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var orderInfo = await FindOrderAsync(id, userId, cancellationToken);
        if (orderInfo is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        var orderId = orderInfo.OrderId;

        var record = await _dbContext.RefundRecords
            .Where(x => x.OrderId == orderId && x.UserId == userId && x.Status == "pending")
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            var any = await _dbContext.RefundRecords
                .AsNoTracking()
                .AnyAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);

            if (!any)
            {
                return Ok(ApiResult.Fail("该订单无退款申请", 404));
            }

            return Ok(ApiResult.Fail("当前退款状态不允许取消", 409));
        }

        record.Status = "cancelled";
        record.UpdateTime = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 恢复原订单状态
        if (orderInfo.Type == "goods")
        {
            var goodsEntity = await _dbContext.CommodityOrders.FindAsync(new object[] { orderId }, cancellationToken);
            if (goodsEntity is not null) goodsEntity.OrderStatusId = 2;
        }
        else if (orderInfo.Type == "food")
        {
            var foodEntity = await _dbContext.DishOrders.FindAsync(new object[] { orderId }, cancellationToken);
            if (foodEntity is not null) foodEntity.OrderStatusId = 2;
        }
        else if (orderInfo.Type == "activity")
        {
            var actEntity = await _dbContext.ActivityOrders.FindAsync(new object[] { orderId }, cancellationToken);
            if (actEntity is not null) actEntity.OrderStatusId = 2;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResult.Success(new { refundId = record.RefundNo }, "退款申请已取消"));
    }

    private static readonly HashSet<string> s_validReasons =
    [
        "商品/菜品与描述不符", "商品破损/质量问题", "与预期不符", "配送延迟", "重复下单", "其他原因"
    ];

    private static readonly HashSet<string> s_validStatuses =
    [
        "pending", "approved", "rejected", "processing", "completed", "failed", "cancelled"
    ];

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static string GenerateRefundNo()
    {
        return $"RF{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private sealed class OrderInfo
    {
        public long OrderId { get; init; }
        public string OrderNo { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public int RawStatusId { get; init; }
        public decimal TotalAmount { get; init; }
    }

    private async Task<OrderInfo?> FindOrderAsync(string rawId, int userId, CancellationToken cancellationToken)
    {
        var raw = (rawId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var isNumeric = long.TryParse(raw, out var orderId) && orderId > 0;

        if (isNumeric)
        {
            var goods = await _dbContext.CommodityOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (goods is not null)
            {
                return new OrderInfo
                {
                    OrderId = goods.OrderId,
                    OrderNo = goods.OrderNo,
                    Type = "goods",
                    RawStatusId = goods.OrderStatusId,
                    TotalAmount = goods.TotalAmount
                };
            }

            var food = await _dbContext.DishOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (food is not null)
            {
                return new OrderInfo
                {
                    OrderId = food.OrderId,
                    OrderNo = food.OrderNo,
                    Type = "food",
                    RawStatusId = food.OrderStatusId,
                    TotalAmount = food.TotalAmount
                };
            }

            var activity = await _dbContext.ActivityOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (activity is not null)
            {
                return new OrderInfo
                {
                    OrderId = activity.OrderId,
                    OrderNo = activity.OrderNo,
                    Type = "activity",
                    RawStatusId = activity.OrderStatusId,
                    TotalAmount = activity.TotalAmount
                };
            }
        }

        // Fallback: search by OrderNo
        {
            var goods = await _dbContext.CommodityOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderNo == raw && x.UserId == userId, cancellationToken);
            if (goods is not null)
            {
                return new OrderInfo
                {
                    OrderId = goods.OrderId,
                    OrderNo = goods.OrderNo,
                    Type = "goods",
                    RawStatusId = goods.OrderStatusId,
                    TotalAmount = goods.TotalAmount
                };
            }

            var food = await _dbContext.DishOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderNo == raw && x.UserId == userId, cancellationToken);
            if (food is not null)
            {
                return new OrderInfo
                {
                    OrderId = food.OrderId,
                    OrderNo = food.OrderNo,
                    Type = "food",
                    RawStatusId = food.OrderStatusId,
                    TotalAmount = food.TotalAmount
                };
            }

            var activity = await _dbContext.ActivityOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderNo == raw && x.UserId == userId, cancellationToken);
            if (activity is not null)
            {
                return new OrderInfo
                {
                    OrderId = activity.OrderId,
                    OrderNo = activity.OrderNo,
                    Type = "activity",
                    RawStatusId = activity.OrderStatusId,
                    TotalAmount = activity.TotalAmount
                };
            }
        }

        return null;
    }

    private static bool CanApplyRefund(OrderInfo order)
    {
        return order.Type switch
        {
            "goods" => order.RawStatusId is 2 or 3,
            "food" => order.RawStatusId is 2,
            "activity" => order.RawStatusId is 2,
            _ => false
        };
    }

    public sealed class ApplyRefundRequest
    {
        public string Reason { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string>? Images { get; set; }
    }
}
