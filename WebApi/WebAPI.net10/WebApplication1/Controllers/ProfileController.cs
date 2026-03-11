using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
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

        // 收货地址相关
        [HttpGet("address")]
        public ActionResult<ApiResponse<IEnumerable<AddressDto>>> GetAddressList()
        {
            var list = new[] { new AddressDto { Id = 1, Name = "张三", Phone = "13800138000", Province = "北京市", City = "北京市", District = "朝阳区", Address = "某某街道123号", IsDefault = true } };
            return ApiResponse<IEnumerable<AddressDto>>.Ok(list);
        }

        [HttpPost("address")]
        public ActionResult<ApiResponse<object>> AddAddress([FromBody] AddressDto dto)
        {
            return ApiResponse<object>.Ok(null);
        }

        [HttpPut("address")]
        public ActionResult<ApiResponse<object>> UpdateAddress([FromBody] AddressDto dto)
        {
            return ApiResponse<object>.Ok(null);
        }

        [HttpDelete("address")]
        public ActionResult<ApiResponse<object>> DeleteAddress([FromQuery] int id)
        {
            return ApiResponse<object>.Ok(null);
        }
    }
}
