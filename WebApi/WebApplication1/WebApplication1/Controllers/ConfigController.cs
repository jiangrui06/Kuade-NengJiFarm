using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        // Used by: demo/pages/index/index.js (首页配置：轮播、推荐、客服电话)
        #region Get - demo/pages/index/index.js
        [HttpGet]
        public ActionResult<ApiResponse<object>> Get()
        {
            var data = new {
                Banners = new[]{ new { ImageUrl = "", Link = "" } },
                Recommendations = new[]{ new { Title = "推荐商品" } },
                CustomerServicePhone = ""
            };
            return ApiResponse<object>.Ok(data);
        }
        #endregion
    }
}
