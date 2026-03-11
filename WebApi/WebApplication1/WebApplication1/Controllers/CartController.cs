using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        // Used by: demo/pages/cart/cart.js (购物车读取)
        #region Get - demo/pages/cart/cart.js
        [HttpGet]
        public ActionResult<ApiResponse<IEnumerable<CartItemDto>>> Get()
        {
            var items = new[] { new CartItemDto { Id = Guid.NewGuid(), Quantity = 1, Goods = new GoodsDto { Id = Guid.NewGuid(), Name = "示例商品", Price = 1.23M } } };
            return ApiResponse<IEnumerable<CartItemDto>>.Ok(items);
        }
        #endregion

        // Used by: demo/pages/cart/cart.js and demo/pages/order/order.js (加入购物车)
        #region AddItem - demo/pages/cart/cart.js, demo/pages/order/order.js
        [HttpPost("items")]
        public ActionResult<ApiResponse<object>> AddItem([FromBody] object body)
        {
            return ApiResponse<object>.Ok(null);
        }
        #endregion

        // Used by: demo/pages/cart/cart.js (更新购物车项数量)
        #region UpdateItem - demo/pages/cart/cart.js
        [HttpPut("items/{id}")]
        public ActionResult<ApiResponse<object>> UpdateItem(Guid id, [FromBody] object body)
        {
            return ApiResponse<object>.Ok(null);
        }
        #endregion

        // Used by: demo/pages/cart/cart.js (删除购物车项)
        #region DeleteItem - demo/pages/cart/cart.js
        [HttpDelete("items/{id}")]
        public ActionResult<ApiResponse<object>> DeleteItem(Guid id)
        {
            return ApiResponse<object>.Ok(null);
        }
        #endregion

        // Used by: demo/pages/cart/cart.js (清空购物车)
        #region Clear - demo/pages/cart/cart.js
        [HttpDelete]
        public ActionResult<ApiResponse<object>> Clear()
        {
            return ApiResponse<object>.Ok(null);
        }
        #endregion
    }
}
