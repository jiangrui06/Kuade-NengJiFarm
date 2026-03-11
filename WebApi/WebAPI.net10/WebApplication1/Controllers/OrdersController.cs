using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        // Used by: demo/pages/cart/cart.js (生成订单) and demo/pages/order/order.js (下单)
        #region Create - POST /api/order/create
        [HttpPost("create")]
        public ActionResult<ApiResponse<OrderDto>> Create([FromBody] object body)
        {
            var order = new OrderDto { Id = Guid.NewGuid(), TotalAmount = 0, Status = "created" };
            return ApiResponse<OrderDto>.Ok(order);
        }
        #endregion

        // Used by: demo/pages/order/order.js (订单列表)
        #region GetList - GET /api/order/list
        [HttpGet("list")]
        public ActionResult<ApiResponse<PagedResult<OrderDto>>> GetList([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var items = new[] { new OrderDto { Id = Guid.NewGuid(), TotalAmount = 0, Status = status ?? "all" } };
            var paged = new PagedResult<OrderDto> { PageIndex = page, PageSize = pageSize, Total = 1, Items = items };
            return ApiResponse<PagedResult<OrderDto>>.Ok(paged);
        }
        #endregion

        // Used by: demo/pages/order/order.js (订单详情)
        #region Get - GET /api/order/detail
        [HttpGet("detail")]
        public ActionResult<ApiResponse<OrderDto>> Detail([FromQuery] Guid orderId)
        {
            var o = new OrderDto { Id = orderId, TotalAmount = 0, Status = "created" };
            return ApiResponse<OrderDto>.Ok(o);
        }
        #endregion

        // Used by: demo/pages/order/order.js (取消订单)
        #region Cancel - PUT /api/order/cancel
        [HttpPut("cancel")]
        public ActionResult<ApiResponse<object>> Cancel([FromBody] object body)
        {
            return ApiResponse<object>.Ok(null);
        }
        #endregion

        // Used by: demo/pages/order/order.js (确认收货)
        #region Confirm - demo/pages/order/order.js确认收货
        [HttpPost("{id}/confirm")]
        public ActionResult<ApiResponse<object>> Confirm(Guid id)
        {
            return ApiResponse<object>.Ok(null);
        }
        #endregion
    }
}
