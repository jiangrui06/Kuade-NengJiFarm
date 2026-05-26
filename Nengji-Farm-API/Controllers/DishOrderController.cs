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
    private readonly ManageAppDbContext _dbContext;

    public DishOrderController(
        ILogger<DishOrderController> logger,
        IDishOrderService dishOrderService,
        ITokenService tokenService,
        ManageAppDbContext dbContext)
    {
        _logger = logger;
        _dishOrderService = dishOrderService;
        _tokenService = tokenService;
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
    /// 获取菜品订单状态列表
    /// </summary>
    [HttpGet("statuses")]
    public async Task<IActionResult> GetOrderStatuses(CancellationToken cancellationToken = default)
    {
        try
        {
            var statuses = await _dbContext.Set<DishOrderStatus>()
                .OrderBy(s => s.OrderStatusId)
                .Select(s => new { statusId = s.OrderStatusId, statusName = s.StatusName })
                .ToListAsync(cancellationToken);
            return Ok(ApiResult.Success(statuses));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取菜品订单状态列表失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取菜品订单状态列表失败"));
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
            if (request is null || (string.IsNullOrWhiteSpace(request.OrderNo) && request.OrderId <= 0))
                return Ok(ApiResult.Fail("请求参数不完整：orderNo 或 orderId 不能为空", 400));

            _logger.LogInformation("菜品订单退款 - OrderNo: {OrderNo}, Operator: {Operator}", request.OrderNo, operatorName);
            var result = await _dishOrderService.RefundAsync(request, operatorName, cancellationToken);

            return Ok(ApiResult.Success(result, "退款成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "菜品订单退款失败 - OrderNo: {OrderNo}", request?.OrderNo);
            return Ok(ApiResult.Fail(ex.Message));
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

            // 动态加载状态映射
            var dishStatusMap = await OrderStatusHelper.LoadDishOrderStatusMapAsync(_dbContext, cancellationToken);
            var dsCompleted = dishStatusMap.Require("已完成", "dish_order_status");
            var dsCancelled = dishStatusMap.Require("已取消", "dish_order_status");

            var detailStatusMap = await OrderStatusHelper.LoadDishOrderDetailStatusMapAsync(_dbContext, cancellationToken);
            var ddsCancelled = detailStatusMap.Require("已取消", "dish_order_detail_status");

            switch (dto.Action)
            {
                case "cancel":
                    if (order.OrderStatusId == dsCompleted || order.OrderStatusId == dsCancelled)
                        throw new Exception("已完成或已取消的订单无法取消");
                    order.OrderStatusId = dsCancelled;

                    // 同步更新后厨明细状态为已取消
                    var cancelDetails = await _dbContext.DishOrderDetails
                        .Where(d => d.DishOrderId == order.OrderId)
                        .ToListAsync(cancellationToken);
                    foreach (var d in cancelDetails)
                        d.StatusId = ddsCancelled;
                    break;

                case "complete":
                    if (order.OrderStatusId == dsCompleted || order.OrderStatusId == dsCancelled)
                        throw new Exception("订单已完成或已取消，无法重复操作");
                    order.OrderStatusId = dsCompleted;
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


public class UpdateDishOrderStatusDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
