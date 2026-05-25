using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[Route("api/dish/order")]
public class DishOrderController : ControllerBase
{
    private readonly ILogger<DishOrderController> _logger;
    private readonly IDishOrderService _dishOrderService;
    private readonly ITokenService _tokenService;
    private readonly IWeChatPayService _weChatPayService;
    private readonly ManageAppDbContext _dbContext;

    public DishOrderController(
        ILogger<DishOrderController> logger,
        IDishOrderService dishOrderService,
        ITokenService tokenService,
        IWeChatPayService weChatPayService,
        ManageAppDbContext dbContext)
    {
        _logger = logger;
        _dishOrderService = dishOrderService;
        _tokenService = tokenService;
        _weChatPayService = weChatPayService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取菜品订单列表
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<ApiResult>> GetOrderList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _dishOrderService.GetOrderListAsync(pageNum, pageSize, keyword, cancellationToken);
            return Ok(ApiResult.Success(result, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取订单列表失败: {ex.Message}");
            return Ok(ApiResult.Fail("获取订单列表失败"));
        }
    }

    /// <summary>
    /// 获取菜品订单详情
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<ApiResult>> GetOrderDetail(
        [FromQuery] string orderNo,
        [FromQuery] string? phone = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return Ok(ApiResult.Fail("订单号不能为空"));
            }

            var result = await _dishOrderService.GetOrderDetailAsync(orderNo, cancellationToken);
            return Ok(ApiResult.Success(result, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取订单详情失败: {ex.Message}");

            if (ex.Message.Contains("不存在"))
                return Ok(ApiResult.Fail("订单不存在", 404));

            return Ok(ApiResult.Fail("获取订单详情失败"));
        }
    }

    /// <summary>
    /// 菜品订单退款（后台管理员操作）
    /// </summary>
    [HttpPost("refund")]
    public async Task<IActionResult> Refund(
        [FromBody] DishOrderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var operatorName = GetAdminUserNo();
        if (operatorName is null)
            return Unauthorized(new { code = 401, message = "登录已过期，请重新登录", data = (object?)null });

        try
        {
            if (request is null || (request.OrderId <= 0 && string.IsNullOrWhiteSpace(request.OrderNo)))
                return Ok(ApiResult.Fail("请求参数不完整：orderId 或 orderNo 不能为空", 400));

            var order = request.OrderId > 0
                ? await _dbContext.DishOrders.FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken)
                : await _dbContext.DishOrders.FirstOrDefaultAsync(o => o.OrderNo == request.OrderNo, cancellationToken);

            if (order is null)
                return Ok(ApiResult.Fail("订单不存在或已被删除", 404));

            // 幂等性检查
            var existingRefund = await _dbContext.RefundRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.OrderNo == order.OrderNo && r.OrderType == "food", cancellationToken);

            if (existingRefund is not null)
                return Ok(ApiResult.Fail("该订单已完成退款，请勿重复操作", 422));

            if (order.OrderStatusId is 3 or 4)
                return Ok(ApiResult.Fail("该订单已完成或已取消，无法退款", 422));

            if (order.OrderStatusId is not (1 or 2))
                return Ok(ApiResult.Fail("当前订单状态不允许退款", 422));

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
                    return Ok(ApiResult.Fail($"微信退款失败：{ex.Message}", 500));
                }
            }

            var now = DateTime.Now;
            var refundNo = $"RF{now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";

            var refund = new WebAPI.Entities.Manage.RefundRecord
            {
                RefundNo = refundNo,
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                OrderType = "food",
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

            // 更新订单状态为已取消/已退款
            order.OrderStatusId = 4;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("菜品退款成功 - RefundNo: {RefundNo}, OrderNo: {OrderNo}, Amount: {Amount}, Operator: {Operator}",
                refundNo, order.OrderNo, order.TotalAmount, operatorName);

            return Ok(ApiResult.Success(new
            {
                refundId = refundNo,
                orderId = order.OrderNo,
                refundAmount = order.TotalAmount.ToString("F2"),
                refundTime = now.ToString("yyyy-MM-dd HH:mm"),
                operatorName,
            }, "退款成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "菜品退款失败 - OrderId: {OrderId}", request?.OrderId);
            return Ok(ApiResult.Fail("服务器异常，请稍后重试", 500));
        }
    }

    /// <summary>
    /// 更新菜品订单状态
    /// </summary>
    [HttpPut("updateStatus")]
    public async Task<ActionResult<ApiResult>> UpdateOrderStatus(
        [FromBody] UpdateDishOrderStatusDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.OrderNo))
                return Ok(ApiResult.Fail("订单号不能为空"));
            if (string.IsNullOrWhiteSpace(dto.Action))
                return Ok(ApiResult.Fail("操作类型不能为空"));

            var order = await _dbContext.DishOrders
                .FirstOrDefaultAsync(o => o.OrderNo == dto.OrderNo, cancellationToken)
                ?? throw new Exception("订单不存在");

            switch (dto.Action)
            {
                case "cancel":
                    if (order.OrderStatusId is 3 or 4)
                        throw new Exception("已完成或已取消的订单无法取消");
                    order.OrderStatusId = 4;
                    break;

                case "complete":
                    if (order.OrderStatusId is 3 or 4)
                        throw new Exception("订单已完成或已取消，无法重复操作");
                    order.OrderStatusId = 3;
                    break;

                default:
                    throw new Exception($"不支持的操作: {dto.Action}");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("菜品订单 {OrderNo} 执行 {Action} 成功", dto.OrderNo, dto.Action);

            return Ok(ApiResult.Success(null, "操作成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("更新菜品订单状态失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 从请求头中提取 Bearer token，验证并获取管理员账号
    /// </summary>
    private string? GetAdminUserNo()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
            return null;

        var token = authHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            if (!_tokenService.ValidateToken(token))
                return null;

            return _tokenService.GetUserIdFromToken(token);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public class DishOrderRefundRequest
{
    public long OrderId { get; set; }
    public string? OrderNo { get; set; }
    public string? RefundReason { get; set; }
}

public class UpdateDishOrderStatusDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
