using Microsoft.AspNetCore.Mvc;
using WebAdminApi.DTOs;
using WebAdminApi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace WebAdminApi.Controllers
{
    [ApiController]
    [Route("api/back-user")]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly IUserService _userService;

        public UserController(ILogger<UserController> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        /// <summary>
        /// 接口1：获取用户列表（分页）
        /// </summary>
        /// <param name="keyword">搜索关键词，匹配昵称/手机号</param>
        /// <param name="pageNum">页码（从1开始，默认为1）</param>
        /// <param name="pageSize">每页记录数（默认为10）</param>
        [HttpGet("list")]
        public IActionResult GetUserList([FromQuery] string? keyword, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (pageNum < 1) pageNum = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                _logger.LogInformation($"获取用户列表|关键词: {keyword}, 页码: {pageNum}, 每页: {pageSize}");
                var result = _userService.GetUserListPage(keyword, pageNum, pageSize);

                return Ok(new ApiResponse<UserListPageDto>
                {
                    Code = 200,
                    Message = "获取成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取用户列表失败: {ex.Message}");
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = "获取用户列表失败"
                });
            }
        }

        /// <summary>
        /// 接口2：添加新用户
        /// </summary>
        [HttpPost("add")]
        public async Task<IActionResult> AddUser([FromBody] AddUserDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "请求参数有误"
                    });
                }

                // 验证手机号格式
                if (!ValidatePhoneNumber(dto.Phone))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "手机号格式不正确"
                    });
                }

                _logger.LogInformation($"添加用户|手机号: {dto.Phone}|昵称: {dto.Nickname}");
                await _userService.AddUser(dto);

                return Ok(new ApiResponse
                {
                    Code = 200,
                    Message = "添加成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"添加用户失败: {ex.Message}");
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// 接口3：编辑用户
        /// </summary>
        [HttpPost("edit")]
        public async Task<IActionResult> EditUser([FromBody] EditUserDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.id))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                _logger.LogInformation($"编辑用户|用户ID: {dto.id}");
                await _userService.EditUser(dto);

                return Ok(new ApiResponse
                {
                    Code = 200,
                    Message = "编辑成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"编辑用户失败: {ex.Message}");
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = ex.Message == "用户不存在" ? "用户不存在，请刷新列表" : "编辑用户失败"
                });
            }
        }

        /// <summary>
        /// 接口4：修改用户状态（启用/禁用）
        /// </summary>
        [HttpPost("changeStatus")]
        public async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.id))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                if (dto.status != "启用" && dto.status != "禁用")
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "状态值不正确，只能是'启用'或'禁用'"
                    });
                }

                _logger.LogInformation($"修改用户状态|用户ID: {dto.id}|目标状态: {dto.status}");
                await _userService.ChangeUserStatus(dto.id, dto.status);

                return Ok(new ApiResponse
                {
                    Code = 200,
                    Message = "状态修改成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"修改用户状态失败: {ex.Message}");
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = ex.Message == "用户不存在" ? "用户不存在，请刷新列表" : "修改用户状态失败"
                });
            }
        }

        /// <summary>
        /// 接口5：删除用户
        /// </summary>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.id))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                _logger.LogInformation($"删除用户|用户ID: {dto.id}");
                await _userService.DeleteUser(dto.id);

                return Ok(new ApiResponse
                {
                    Code = 200,
                    Message = "删除成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"删除用户失败: {ex.Message}");
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = ex.Message == "用户不存在" ? "用户不存在，请刷新列表" : "删除用户失败"
                });
            }
        }

        /// <summary>
        /// 接口6：用户登录（仅管理员可登录）
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.phone) || string.IsNullOrWhiteSpace(dto.password))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "手机号和密码不能为空"
                    });
                }

                if (!ValidatePhoneNumber(dto.phone))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "手机号格式不正确，11位数字"
                    });
                }

                _logger.LogInformation($"用户登录|手机号: {dto.phone}");
                var result = await _userService.Login(dto.phone, dto.password);

                return Ok(new ApiResponse<LoginResponseDto>
                {
                    Code = 200,
                    Message = "登录成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"登录失败: {ex.Message}");

                if (ex.Message.Contains("未注册"))
                    return Unauthorized(new ApiResponse { Code = 401, Message = "该手机号未注册" });

                if (ex.Message.Contains("禁用"))
                    return StatusCode(403, new ApiResponse { Code = 403, Message = "账号已禁用，请联系管理员" });

                if (ex.Message.Contains("密码"))
                    return Unauthorized(new ApiResponse { Code = 401, Message = "密码错误，请重新输入" });

                if (ex.Message.Contains("权限不足") || ex.Message.Contains("管理员"))
                    return Unauthorized(new ApiResponse { Code = 401, Message = "权限不足，仅管理员可登录" });

                return BadRequest(new ApiResponse { Code = 400, Message = "登录失败" });
            }
        }

        /// <summary>
        /// 接口7：用户注销
        /// 客户端删除本地存储的 Token 即可注销
        /// </summary>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            try
            {
                // 从请求头中获取 Token（可选，用于日志记录）
                var token = Request.Headers["Authorization"].ToString();

                _logger.LogInformation($"? 用户已注销 | Token: {(string.IsNullOrWhiteSpace(token) ? "未提供" : token.Substring(0, Math.Min(20, token.Length)) + "...")}");

                return Ok(new ApiResponse
                {
                    Code = 200,
                    Message = "注销成功，请删除本地 Token"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"注销失败: {ex.Message}");
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = "注销失败"
                });
            }
        }

        #region 辅助方法

        /// <summary>
        /// 验证手机号格式（11位数字）
        /// </summary>
        private bool ValidatePhoneNumber(string phone)
        {
            return !string.IsNullOrWhiteSpace(phone) &&
                   phone.Length == 11 &&
                   phone.All(c => char.IsDigit(c));
        }

        #endregion
    }
}