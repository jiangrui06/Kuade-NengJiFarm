using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/activity-order")]
public class ActivityOrderController : ControllerBase
{
    private readonly IActivityOrderService _orderService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ActivityOrderController> _logger;
    private readonly ManageAppDbContext _dbContext;

    public ActivityOrderController(
        IActivityOrderService orderService,
        ITokenService tokenService,
        ILogger<ActivityOrderController> logger,
        ManageAppDbContext dbContext)
    {
        _orderService = orderService;
        _tokenService = tokenService;
        _logger = logger;
        _dbContext = dbContext;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        [FromQuery] int? statusId = null,
        CancellationToken cancellationToken = default)
    {
        var (records, total) = await _orderService.GetOrderListAsync(pageNum, pageSize, keyword, statusId, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            records,
            total,
            pageNum,
            pageSize,
            pages = (total + pageSize - 1) / pageSize
        }));
    }

    [HttpGet("detail")]
    public async Task<IActionResult> GetDetail(
        [FromQuery] long orderId,
        [FromQuery] string? orderNo,
        CancellationToken cancellationToken = default)
    {
        if (orderId > 0)
        {
            var order = await _orderService.GetOrderDetailAsync(orderId, cancellationToken);
            if (order is null)
                return Ok(ApiResult.Fail("订单不存在或已被删除", 404));
            return Ok(ApiResult.Success(order));
        }

        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            var order = await _orderService.GetOrderDetailAsync(orderNo, cancellationToken);
            if (order is null)
                return Ok(ApiResult.Fail("订单不存在或已被删除", 404));
            return Ok(ApiResult.Success(order));
        }

        return Ok(ApiResult.Fail("参数不正确", 400));
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyActivityOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 支持通过 orderNo 核销（前端使用，优先）
            if (!string.IsNullOrWhiteSpace(request?.OrderNo))
            {
                var (success, message) = await _orderService.VerifyByOrderNoAsync(request.OrderNo, cancellationToken);
                if (!success)
                    return Ok(ApiResult.Fail(message, 400));
                return Ok(ApiResult.Success(message));
            }

            // 支持通过 orderId 核销（兼容旧版）
            if (request?.OrderId > 0)
            {
                var (success, message) = await _orderService.VerifyByOrderIdAsync(request.OrderId, cancellationToken);
                if (!success)
                    return Ok(ApiResult.Fail(message, 400));
                return Ok(ApiResult.Success(message));
            }

            // 支持通过 activityOrderDetailsId 核销（管理端使用）
            if (request?.ActivityOrderDetailsId <= 0)
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var result = await _orderService.VerifyOrderDetailAsync(request.ActivityOrderDetailsId, cancellationToken);

            if (!result)
                return Ok(ApiResult.Fail("核销明细不存在或已被删除", 404));

            return Ok(ApiResult.Success("核销成功"));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, 400));
        }
    }

    /// <summary>
    /// 获取活动券订单状态列表
    /// </summary>
    [HttpGet("statuses")]
    public async Task<IActionResult> GetOrderStatuses(CancellationToken cancellationToken = default)
    {
        try
        {
            var statuses = await _dbContext.Set<ActivityOrderStatus>()
                .OrderBy(s => s.ActivityOrderStatusId)
                .Select(s => new { statusId = s.ActivityOrderStatusId, statusName = s.StatusName })
                .ToListAsync(cancellationToken);
            return Ok(ApiResult.Success(statuses));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取活动券订单状态列表失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取活动券订单状态列表失败"));
        }
    }

    /// <summary>
    /// 券类订单退款/驳回退款（后台管理员操作）
    /// 不带 action：一键退款（待核销/已核销 → 已退款）
    /// action = "refund-process"：兼容旧版，幂等处理，已退款的直接返回成功
    /// action = "reject"：驳回退款，恢复订单状态
    /// </summary>
    [HttpPost("refund")]
    public async Task<IActionResult> Refund(
        [FromBody] ActivityOrderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        // 验证 token 并获取操作人
        var operatorName = GetAdminUserNo();
        if (operatorName is null)
            return Unauthorized(new { code = 401, message = "登录已过期，请重新登录", data = (object?)null });

        try
        {
            // 处理退款（确认退款）
            if (string.Equals(request?.Action, "refund-process", StringComparison.OrdinalIgnoreCase))
            {
                var processResult = await _orderService.ProcessRefundAsync(request!, operatorName, cancellationToken);

                return Ok(new
                {
                    code = 200,
                    message = "退款已确认完成",
                    data = processResult
                });
            }

            // 驳回退款
            if (string.Equals(request?.Action, "reject", StringComparison.OrdinalIgnoreCase))
            {
                var rejectResult = await _orderService.RejectRefundAsync(request!, operatorName, cancellationToken);

                return Ok(new
                {
                    code = 200,
                    message = "退款已驳回",
                    data = rejectResult
                });
            }

            // 正常退款（一键完成，不经过"退款中"中间状态）
            if (request is null || (string.IsNullOrWhiteSpace(request.OrderNo) && request.OrderId <= 0))
                return Ok(ApiResult.Fail("请求参数不完整：orderNo 或 orderId 不能为空", 400));

            var result = await _orderService.RefundAsync(request, operatorName, cancellationToken);

            return Ok(new
            {
                code = 200,
                message = "退款已完成",
                data = result
            });
        }
        catch (BusinessException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, ex.Code));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "退款操作失败 - OrderId: {OrderId}, Action: {Action}", request?.OrderId, request?.Action);
            return Ok(ApiResult.Fail("服务器异常，请稍后重试", 500));
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
