using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        [HttpGet("index")]
        public ActionResult<ApiResponse<object>> Index()
        {
            var data = new
            {
                swiperList = new[] { new { id = 1, image = "" } },
                functionButtons = new[] { new { id = 1, name = "功能", color = "#000", path = "/pages/index/index" } },
                farmGoods = new[] { new { id = 1, name = "示例", image = "", price = 0, originalPrice = 0, tags = new string[0], stock = 0 } },
                hotDishes = new[] { new { id = 1, name = "热销", image = "", price = 0, tags = new string[0] } }
            };
            return ApiResponse<object>.Ok(data);
        }
    }
}