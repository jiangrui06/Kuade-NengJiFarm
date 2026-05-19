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

    public ProductOrderController(
        ILogger<ProductOrderController> logger,
        IProductOrderService productOrderService)
    {
        _logger = logger;
        _productOrderService = productOrderService;
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
            if (string.IsNullOrWhiteSpace(dto.OrderNo))
                return Ok(ApiResult.Fail("订单号不能为空"));
            if (string.IsNullOrWhiteSpace(dto.Action))
                return Ok(ApiResult.Fail("操作类型不能为空"));

            await _productOrderService.UpdateOrderStatusAsync(dto, cancellationToken);
            return Ok(ApiResult.Success(null, "操作成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("更新产品订单状态失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail(ex.Message));
        }
    }
}
