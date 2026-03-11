using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        // GET /api/cart/list
        [HttpGet("list")]
        public ActionResult<ApiResponse<IEnumerable<CartItemDto>>> List()
        {
            var items = new[] { new CartItemDto { Id = Guid.NewGuid(), Quantity = 1, Goods = new GoodsDto { Id = Guid.NewGuid(), Name = "示例商品", Price = 1.23M } } };
            return ApiResponse<IEnumerable<CartItemDto>>.Ok(items);
        }

        // POST /api/cart/add
        [HttpPost("add")]
        public ActionResult<ApiResponse<object>> Add([FromBody] object body)
        {
            return ApiResponse<object>.Ok(null);
        }

        // PUT /api/cart/update
        [HttpPut("update")]
        public ActionResult<ApiResponse<object>> Update([FromBody] object body)
        {
            return ApiResponse<object>.Ok(null);
        }

        // DELETE /api/cart/delete
        [HttpDelete("delete")]
        public ActionResult<ApiResponse<object>> Delete([FromBody] object body)
        {
            return ApiResponse<object>.Ok(null);
        }

        // DELETE /api/cart/clear
        [HttpDelete("clear")]
        public ActionResult<ApiResponse<object>> Clear()
        {
            return ApiResponse<object>.Ok(null);
        }
    }
}
