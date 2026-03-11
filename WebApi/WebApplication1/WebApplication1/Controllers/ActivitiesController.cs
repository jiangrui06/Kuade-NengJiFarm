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
            var all = new[] {
                new ActivityDto { Id = Guid.NewGuid(), Title = "农家研学活动报名中", Price = "门票: 10-20 ¥", Date="2025.2.25-2025.3.6", ImageUrl="", Category="picking", Content = "" },
                new ActivityDto { Id = Guid.NewGuid(), Title = "采摘活动报名中", Price = "门票: 10-50 ¥", Date="2025.2.25-2025.3.6", ImageUrl="", Category="picking", Content = "" },
                new ActivityDto { Id = Guid.NewGuid(), Title = "草莓采摘体验", Price = "门票: 30 ¥/人", Date="2025.3.1-2025.4.30", ImageUrl="", Category="picking", Content = "" },
                new ActivityDto { Id = Guid.NewGuid(), Title = "葡萄采摘节", Price = "门票: 50 ¥/人", Date="2025.7.1-2025.8.31", ImageUrl="", Category="picking", Content = "" },
                new ActivityDto { Id = Guid.NewGuid(), Title = "农场露营体验", Price = "费用: 120 ¥/晚", Date="2025.4.1-2025.10.31", ImageUrl="", Category="camping", Content = "" },
                new ActivityDto { Id = Guid.NewGuid(), Title = "篝火露营晚会", Price = "费用: 180 ¥/人", Date="2025.5.1-2025.9.30", ImageUrl="", Category="camping", Content = "" }
            };
            IEnumerable<ActivityDto> items = all;
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                items = all.Where(a => a.Category == status);
            }
            var paged = new PagedResult<ActivityDto> { PageIndex = pageIndex, PageSize = pageSize, Total = items.Count(), Items = items };
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
