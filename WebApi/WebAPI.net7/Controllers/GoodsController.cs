using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoodsController : ControllerBase
    {
        // Used by: demo/pages/farm-goods/farm-goods.js (商品列表)
        #region GetList - demo/pages/farm-goods/farm-goods.js商品列表
        [HttpGet]
        public ActionResult<ApiResponse<PagedResult<GoodsDto>>> GetList([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10, [FromQuery] Guid? categoryId = null, [FromQuery] string? keyword = null)
        {
            var items = new[] { new GoodsDto { Id = Guid.NewGuid(), Name = "示例商品", Price = 12.34M } };
            var paged = new PagedResult<GoodsDto> { PageIndex = pageIndex, PageSize = pageSize, Total = 1, Items = items };
            return ApiResponse<PagedResult<GoodsDto>>.Ok(paged);
        }
        #endregion

        // Used by: demo/pages/goods-detail/goods-detail.js (商品详情)
        #region Get - demo/pages/goods-detail/goods-detail.js
        [HttpGet("{id}")]
        public ActionResult<ApiResponse<GoodsDto>> Get(Guid id)
        {
            var g = new GoodsDto { Id = id, Name = "示例商品", Price = 12.34M };
            return ApiResponse<GoodsDto>.Ok(g);
        }
        #endregion

        // GET /api/goods/detail?goodsId=xxx
        [HttpGet("detail")]
        public ActionResult<ApiResponse<GoodsDto>> Detail([FromQuery] Guid goodsId)
        {
            var g = new GoodsDto { Id = goodsId, Name = "示例商品", Price = 12.34M };
            return ApiResponse<GoodsDto>.Ok(g);
        }

        // Used by: demo/pages/index/index.js (首页推荐)
        #region Recommend - demo/pages/index/index.js
        [HttpGet("recommend")]
        public ActionResult<ApiResponse<IEnumerable<GoodsDto>>> Recommend()
        {
            var list = new[] { new GoodsDto { Id = Guid.NewGuid(), Name = "推荐商品", Price = 9.99M } };
            return ApiResponse<IEnumerable<GoodsDto>>.Ok(list);
        }
        #endregion
    }
}
