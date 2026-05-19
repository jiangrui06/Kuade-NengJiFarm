using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Services;

namespace WebAPI.Controllers;

[Route("api/dish/order")]
public class DishOrderController : ControllerBase
{
    private readonly ILogger<DishOrderController> _logger;
    private readonly IDishOrderService _dishOrderService;

    public DishOrderController(
        ILogger<DishOrderController> logger,
        IDishOrderService dishOrderService)
    {
        _logger = logger;
        _dishOrderService = dishOrderService;
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
}
