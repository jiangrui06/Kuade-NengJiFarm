using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/activity-order")]
public class ActivityOrderController : ControllerBase
{
    private readonly IActivityOrderService _orderService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ActivityOrderController> _logger;

    public ActivityOrderController(
        IActivityOrderService orderService,
        ITokenService tokenService,
        ILogger<ActivityOrderController> logger)
    {
        _orderService = orderService;
        _tokenService = tokenService;
        _logger = logger;
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
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            return Ok(ApiResult.Fail("参数不正确", 400));

        var order = await _orderService.GetOrderDetailAsync(orderId, cancellationToken);

        if (order is null)
            return Ok(ApiResult.Fail("订单不存在或已被删除", 404));

        return Ok(ApiResult.Success(order));
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyActivityOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 支持通过 orderId 核销（前端使用）
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
    /// 券类订单退款/驳回退款（后台管理员操作）
    /// 当 action = "reject" 时为驳回退款
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

            // 正常退款
            if (request is null || request.OrderId <= 0)
                return Ok(ApiResult.Fail("请求参数不完整：orderId 不能为空", 400));

            var result = await _orderService.RefundAsync(request, operatorName, cancellationToken);

            return Ok(new
            {
                code = 200,
                message = "退款成功",
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
