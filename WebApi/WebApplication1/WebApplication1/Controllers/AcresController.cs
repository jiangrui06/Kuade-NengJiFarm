using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AcresController : ControllerBase
    {
        // Used by: demo/pages/acre/acre.js (地块列表)
        #region GetList - demo/pages/acre/acre.js
        [HttpGet]
        public ActionResult<ApiResponse<PagedResult<AcreDto>>> GetList([FromQuery] string? status = null, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var items = new[] { new AcreDto { Id = Guid.NewGuid(), Name = "示例地块", Status = status ?? "available" } };
            var paged = new PagedResult<AcreDto> { PageIndex = pageIndex, PageSize = pageSize, Total = 1, Items = items };
            return ApiResponse<PagedResult<AcreDto>>.Ok(paged);
        }
        #endregion

        // Used by: demo/pages/acre-detail/acre-detail.js (地块详情)
        #region Get - demo/pages/acre-detail/acre-detail.js
        [HttpGet("{id}")]
        public ActionResult<ApiResponse<AcreDto>> Get(Guid id)
        {
            var a = new AcreDto { Id = id, Name = "示例地块", Status = "adopted" };
            return ApiResponse<AcreDto>.Ok(a);
        }
        #endregion

        // Used by: demo/pages/acre-detail/acre-detail.js (认养地块)
        #region Adopt - demo/pages/acre-detail/acre-detail.js
        [HttpPost("{id}/adopt")]
        public ActionResult<ApiResponse<object>> Adopt(Guid id, [FromBody] object body)
        {
            // body could contain months, remark
            return ApiResponse<object>.Ok(null);
        }
        #endregion

        // Used by: demo/pages/acre-detail/acre-detail.js (地块操作记录)
        #region Logs - demo/pages/acre-detail/acre-detail.js
        [HttpGet("{id}/logs")]
        public ActionResult<ApiResponse<IEnumerable<object>>> Logs(Guid id)
        {
            var logs = new[] { new { Time = DateTime.Now, Action = "播种" } };
            return ApiResponse<IEnumerable<object>>.Ok(logs);
        }
        #endregion
    }
}
