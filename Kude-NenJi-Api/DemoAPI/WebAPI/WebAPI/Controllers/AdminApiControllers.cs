using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using WebAPI.Entities;

namespace WebAdminApi.Controllers
{
    public class AdminApiControllers
    {
        [ApiController]
        [Authorize]
        [Route("api/back-user/roles/list")]
        public class UserListController : ControllerBase
        {
            [HttpGet]
            public IActionResult GetUserList(string ContentType,string token)
            {
                // 这里可以添加获取用户列表的逻辑
                return Ok(new { Message = "获取用户列表成功" });
            }
        }

        /// <summary>
        /// 角色管理控制器
        /// </summary>
        [ApiController]
        [Route("api/back-user/roles/list")]
        public class RoleController : ControllerBase
        {
            /// <summary>
            /// 添加角色
            /// </summary>
            [HttpPost("add")]
            public IActionResult AddRole([FromBody] Role role)
            {
                if (role == null)
                {
                    return BadRequest(new { Message = "角色数据不能为空" });
                }

                // 这里可以添加添加角色的逻辑

                return Ok(new { Message = "角色添加成功" });
            }

            /// <summary>
            /// 获取角色列表
            /// </summary>
            [HttpGet("list")]
            public IActionResult GetRoleList()
            {
                // 这里可以添加获取角色列表的逻辑
                return Ok(new { Message = "获取角色列表成功" });
            }
        }
    }
}
