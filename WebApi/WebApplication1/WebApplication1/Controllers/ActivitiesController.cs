using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivitiesController : ControllerBase
    {
        // Used by: demo/pages/activity/activity.js (活动列表)
        #region GetList - demo/pages/activity/activity.js
        [HttpGet]
        public ActionResult<ApiResponse<PagedResult<ActivityDto>>> GetList([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null)
        {
            var items = new[] { new ActivityDto { Id = Guid.NewGuid(), Title = "示例活动", Content = "内容" } };
            var paged = new PagedResult<ActivityDto> { PageIndex = pageIndex, PageSize = pageSize, Total = 1, Items = items };
            return ApiResponse<PagedResult<ActivityDto>>.Ok(paged);
        }
        #endregion

        // Used by: demo/pages/activity/activity.js (活动详情)
        #region Get - demo/pages/activity/activity.js
        [HttpGet("{id}")]
        public ActionResult<ApiResponse<ActivityDto>> Get(Guid id)
        {
            var a = new ActivityDto { Id = id, Title = "示例活动", Content = "内容" };
            return ApiResponse<ActivityDto>.Ok(a);
        }
        #endregion
    }
}
