using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

[Route("api/product/order")]
public class ProductOrderController : ControllerBase
{
    private readonly ILogger<ProductOrderController> _logger;
    private readonly IProductOrderService _productOrderService;
    private readonly ITokenService _tokenService;

    public ProductOrderController(
        ILogger<ProductOrderController> logger,
        IProductOrderService productOrderService,
        ITokenService tokenService)
    {
        _logger = logger;
        _productOrderService = productOrderService;
        _tokenService = tokenService;
    }

    /// <summary>
    /// 获取产品订单列表
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
            var result = await _productOrderService.GetOrderListAsync(pageNum, pageSize, keyword, cancellationToken);
            return Ok(ApiResult.Success(result, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取产品订单列表失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取订单列表失败"));
        }
    }

    /// <summary>
    /// 获取产品订单详情
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<ApiResult>> GetOrderDetail(
        [FromQuery] string orderNo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return Ok(ApiResult.Fail("订单号不能为空"));

            var result = await _productOrderService.GetOrderDetailAsync(orderNo, cancellationToken);
            return Ok(ApiResult.Success(result, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取产品订单详情失败: {Message}", ex.Message);

            if (ex.Message.Contains("不存在"))
                return Ok(ApiResult.Fail("订单不存在", 404));

            return Ok(ApiResult.Fail("获取订单详情失败"));
        }
    }

    /// <summary>
    /// 更新产品订单状态
    /// </summary>
    [HttpPut("updateStatus")]
    public async Task<ActionResult<ApiResult>> UpdateOrderStatus(
        [FromBody] UpdateProductOrderStatusDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var operatorName = GetAdminUserNo();

            if (string.IsNullOrWhiteSpace(dto.OrderNo))
                return Ok(ApiResult.Fail("订单号不能为空"));
            if (string.IsNullOrWhiteSpace(dto.Action))
                return Ok(ApiResult.Fail("操作类型不能为空"));

            await _productOrderService.UpdateOrderStatusAsync(dto, operatorName, cancellationToken);
            return Ok(ApiResult.Success(null, "操作成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("更新产品订单状态失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 产品订单退款（后台管理员操作）
    /// </summary>
    [HttpPost("refund")]
    public async Task<ActionResult<ApiResult>> Refund(
        [FromBody] ProductOrderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var operatorName = GetAdminUserNo();
        if (operatorName is null)
            return Unauthorized(new { code = 401, message = "登录已过期，请重新登录", data = (object?)null });

        try
        {
            // 正常退款
            if (request is null || (string.IsNullOrWhiteSpace(request.OrderNo) && request.OrderId <= 0))
                return Ok(ApiResult.Fail("请求参数不完整：orderNo 或 orderId 不能为空", 400));

            _logger.LogInformation("产品订单退款 - OrderNo: {OrderNo}, OrderId: {OrderId}, Operator: {Operator}", request.OrderNo, request.OrderId, operatorName);
            var result = await _productOrderService.RefundAsync(request, operatorName, cancellationToken);

            return Ok(ApiResult.Success(result, "退款成功"));
        }
        catch (BusinessException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, ex.Code));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "产品订单退款失败 - OrderNo: {OrderNo}", request?.OrderNo);
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
