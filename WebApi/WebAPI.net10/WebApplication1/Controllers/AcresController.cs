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
            var all = new[] {
                new AcreDto { Id = Guid.NewGuid(), Name = "示例地块1", Status = "available", Price="￥99999", ImageUrl="", Description="..." },
                new AcreDto { Id = Guid.NewGuid(), Name = "示例地块2", Status = "adopted", Price="￥88888", ImageUrl="", Description="..." },
                new AcreDto { Id = Guid.NewGuid(), Name = "示例地块3", Status = "available", Price="￥77777", ImageUrl="", Description="..." }
            };
            IEnumerable<AcreDto> items = all;
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                items = all.Where(a => a.Status == status);
            }
            var paged = new PagedResult<AcreDto> { PageIndex = pageIndex, PageSize = pageSize, Total = items.Count(), Items = items };
            return ApiResponse<PagedResult<AcreDto>>.Ok(paged);
        }
        #endregion

        // Used by: demo/pages/acre-detail/acre-detail.js (地块详情)
        #region Get - demo/pages/acre-detail/acre-detail.js
        [HttpGet("{id}")]
        // allow either GUID or numeric identifiers (the demo homepage hardcodes 1/2)
        public ActionResult<ApiResponse<AcreDto>> Get(string id)
        {
            if (!Guid.TryParse(id, out var guid))
            {
                // fallback to something valid so we don't return 400
                guid = Guid.NewGuid();
            }
            var a = new AcreDto { Id = guid, Name = "示例地块", Status = "adopted" };
            return ApiResponse<AcreDto>.Ok(a);
        }
        #endregion

        // Used by: demo/pages/acre-detail/acre-detail.js (认养地块)
        #region Adopt - demo/pages/acre-detail/acre-detail.js
        [HttpPost("{id}/adopt")]
        public ActionResult<ApiResponse<object>> Adopt(string id, [FromBody] object body)
        {
            // body could contain months, remark; parsing kept for completeness
            Guid.TryParse(id, out var guid);
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
