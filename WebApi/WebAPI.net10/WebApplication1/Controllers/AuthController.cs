using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        [HttpPost("login")]
        public ActionResult<ApiResponse<object>> Login([FromBody] LoginRequest req)
        {
            // TODO: perform real authentication, return token and user info
            var data = new
            {
                token = "fake-token-" + System.Guid.NewGuid(),
                userInfo = new
                {
                    id = System.Guid.NewGuid(),
                    nickname = "游客",
                    avatar = "",
                }
            };
            return ApiResponse<object>.Ok(data);
        }

        [HttpPost("wechat")]
        public ActionResult<ApiResponse<object>> Wechat([FromBody] WechatLoginRequest req)
        {
            var data = new
            {
                token = "fake-token-" + System.Guid.NewGuid(),
                userInfo = new
                {
                    id = System.Guid.NewGuid(),
                    nickname = "微信用户",
                    avatar = ""
                }
            };
            return ApiResponse<object>.Ok(data);
        }

        [HttpPost("phone")]
        public ActionResult<ApiResponse<object>> Phone([FromBody] PhoneLoginRequest req)
        {
            var data = new
            {
                token = "fake-token-" + System.Guid.NewGuid(),
                userInfo = new
                {
                    id = System.Guid.NewGuid(),
                    nickname = "手机号用户",
                    avatar = ""
                }
            };
            return ApiResponse<object>.Ok(data);
        }

        [HttpPost("logout")]
        public ActionResult<ApiResponse<object>> Logout()
        {
            // Invalidate token if necessary
            return ApiResponse<object>.Ok(null);
        }

        [HttpGet("check")]
        public ActionResult<ApiResponse<object>> Check()
        {
            // simply always return logged in for demo
            var data = new
            {
                isLoggedIn = true,
                userInfo = new
                {
                    id = System.Guid.NewGuid(),
                    nickname = "游客",
                    avatar = ""
                }
            };
            return ApiResponse<object>.Ok(data);
        }
    }
}