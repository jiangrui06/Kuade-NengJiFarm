using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        // Used by: demo/pages/profile/profile.js (个人中心)
        #region GetCurrent - demo/pages/profile/profile.js个人中心
        [HttpGet]
        public ActionResult<ApiResponse<UserDto>> GetCurrent()
        {
            var user = new UserDto { Id = Guid.NewGuid(), NickName = "游客", AvatarUrl = "", PhoneNumber = "" };
            return ApiResponse<UserDto>.Ok(user);
        }
        #endregion

        // Used by: demo/pages/profile/profile.js (更新用户信息)
        #region Update - demo/pages/profile/profile.js更新用户信息
        [HttpPut]
        public ActionResult<ApiResponse<object>> Update([FromBody] UserDto dto)
        {
            // Placeholder: accept update
            return ApiResponse<object>.Ok(null);
        }
        #endregion

        // Used by: demo/pages/profile/profile.js (用户统计)
        #region Statistics - demo/pages/profile/profile.js用户统计
        [HttpGet("statistics")]
        public ActionResult<ApiResponse<object>> Statistics()
        {
            var stat = new { OrderCount = 0, AdoptedAcres = 0, Points = 0 };
            return ApiResponse<object>.Ok(stat);
        }
        #endregion
    }
}
