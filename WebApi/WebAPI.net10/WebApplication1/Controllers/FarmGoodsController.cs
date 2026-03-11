using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FarmGoodsController : ControllerBase
    {
        [HttpGet("index")]
        public ActionResult<ApiResponse<object>> Index()
        {
            var data = new
            {
                swiperList = new[] { new { id = 1, image = "" } },
                categories = new[] { new { id = "all", name = "推荐", icon = "", color = "" } },
                todayGoods = new[] { new { id = 1, name = "示例", image = "", price = 0, originalPrice = 0, stock = 0, tags = new string[0] } },
                hotGoods = new[] { new { id = 1, name = "示例", image = "", price = 0, originalPrice = 0, stock = 0, tags = new string[0] } }
            };
            return ApiResponse<object>.Ok(data);
        }

        [HttpGet("category")]
        public ActionResult<ApiResponse<object>> Category([FromQuery] string categoryId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var data = new
            {
                goodsList = new[] { new { id = 1, name = "示例", image = "", price = 0, tags = new string[0] } },
                total = 1,
                page = page,
                pageSize = pageSize
            };
            return ApiResponse<object>.Ok(data);
        }

        [HttpGet("search")]
        public ActionResult<ApiResponse<object>> Search([FromQuery] string keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var data = new
            {
                goodsList = new[] { new { id = 1, name = "示例", image = "", price = 0, tags = new string[0] } },
                total = 1,
                page = page,
                pageSize = pageSize
            };
            return ApiResponse<object>.Ok(data);
        }
    }
}