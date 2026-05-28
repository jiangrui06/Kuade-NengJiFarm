using Microsoft.AspNetCore.Mvc;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/back-user")]
    public class BackUserController : ControllerBase
    {
        private readonly ILogger<BackUserController> _logger;
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;
        private readonly ManageAppDbContext _dbContext;

        public BackUserController(ILogger<BackUserController> logger, IUserService userService, ITokenService tokenService, ManageAppDbContext dbContext)
        {
            _logger = logger;
            _userService = userService;
            _tokenService = tokenService;
            _dbContext = dbContext;
        }

        /// <summary>
        /// 接口1：获取用户列表（分页）
        /// </summary>
        /// <param name="keyword">搜索关键字（匹配昵称/手机号）</param>
        /// <param name="pageNum">页码（从1开始，默认为1）</param>
        /// <param name="pageSize">每页记录数（默认为10）</param>
        [HttpGet("list")]
        [HttpGet("/api/user/list")]
        public IActionResult GetUserList([FromQuery] string? keyword, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                if (pageNum < 1) pageNum = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;



                _logger.LogInformation($"获取用户列表|关键字: {keyword}, 页码: {pageNum}, 每页: {pageSize}");
                var result = _userService.GetUserListPage(keyword, pageNum, pageSize);

                return Ok(new ApiResponses<UserListPageDto>
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
        /// 接口2：添加用户
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
                        Message = "请求参数错误"
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

                _logger.LogInformation($"添加用户|手机号: {dto.Phone}|昵称: {dto.RealName}");
                var (success, isDefaultPassword) = await _userService.AddUser(dto);

                return Ok(new ApiResponses<object>
                {
                    Code = 200,
                    Message = "添加成功",
                    Data = new { isDefaultPassword }
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
                if (string.IsNullOrWhiteSpace(dto.Guid))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                _logger.LogInformation($"编辑用户|用户ID: {dto.Guid}");
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

        [HttpGet("detail")]
        public async Task<IActionResult> GetUserDetail([FromQuery] string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { code = 400, message = "ID 不能为空" });
            }

            try
            {
                var result = await _userService.GetUserDetailAsync(id);

                if (result == null)
                {
                    return NotFound(new { code = 404, message = "未找到该用户" });
                }

                return Ok(new
                {
                    code = 200,
                    message = "获取成功",
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取用户详情异常: {ex.Message}");
                return StatusCode(500, new { code = 500, message = "服务器内部错误" });
            }
        }

        ///// <summary>
        ///// 接口4：修改用户状态（启用/禁用）
        ///// </summary>
        //[HttpPost("changeStatus")]
        //public async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusDto dto)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(dto.id))
        //        {
        //            return BadRequest(new ApiResponse
        //            {
        //                Code = 400,
        //                Message = "用户ID不能为空"
        //            });
        //        }

        //        if (dto.status != "启用" && dto.status != "禁用")
        //        {
        //            return BadRequest(new ApiResponse
        //            {
        //                Code = 400,
        //                Message = "状态值不正确，只能为'启用'或'禁用'"
        //            });
        //        }

        //        _logger.LogInformation($"修改用户状态|用户ID: {dto.id}|目标状态: {dto.status}");
        //        //await _userService.ChangeUserStatus(dto.id, dto.status);

        //        return Ok(new ApiResponse
        //        {
        //            Code = 200,
        //            Message = "状态修改成功"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError($"修改用户状态失败: {ex.Message}");
        //        return BadRequest(new ApiResponse
        //        {
        //            Code = 400,
        //            Message = ex.Message == "用户不存在" ? "用户不存在，请刷新列表" : "修改用户状态失败"
        //        });
        //    }
        //}

        /// <summary>
        /// 接口5：删除用户
        /// </summary>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Guid))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                _logger.LogInformation($"删除用户|用户GUID: {dto.Guid}");
                await _userService.DeleteUser(dto.Guid);

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
        /// 获取角色列表
        /// </summary>
        [HttpGet("roles")]
        public IActionResult GetRoles()
        {
            try
            {
                var roles = _dbContext.Roles
                    .OrderBy(x => x.RoleId)
                    .Select(x => new
                    {
                        roleId = x.RoleId,
                        roleName = x.RoleName
                    })
                    .ToList();

                return Ok(new ApiResponses<object>
                {
                    Code = 200,
                    Message = "获取成功",
                    Data = new { records = roles }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取角色列表失败: {ex.Message}");
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = "获取角色列表失败"
                });
            }
        }

        /// <summary>
        /// 接口6：用户登录（只有管理员可登录）
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.user_no) || string.IsNullOrWhiteSpace(dto.password))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "账号和密码不能为空"
                    });
                }

                _logger.LogInformation($"用户登录|用户账号后4位: {dto.user_no}");
                var result = await _userService.Login(dto.user_no, dto.password);

                return Ok(new ApiResponses<LoginResponseDto>
                {
                    Code = 200,
                    Message = "登录成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                //_logger.LogError($"登录失败: {ex.Message}");

                if (ex.Message.Contains("未注册"))
                    return Unauthorized(new ApiResponse { Code = 401, Message = "该账号未注册" });

                if (ex.Message.Contains("禁用"))
                    return StatusCode(403, new ApiResponse { Code = 403, Message = "账号已禁用，请联系管理员" });

                if (ex.Message.Contains("密码"))
                    return Unauthorized(new ApiResponse { Code = 401, Message = "账号或密码不正确" });

                return BadRequest(new ApiResponse { Code = 400, Message = "登录失败:" });


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
                // 从请求头中获取 Token（可选，仅日志记录）
                var token = Request.Headers["Authorization"].ToString();

                _logger.LogInformation($"? 用户请求注销 | Token: {(string.IsNullOrWhiteSpace(token) ? "未提供" : token.Substring(0, Math.Min(20, token.Length)) + "...")}");

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

        /// <summary>
        /// 获取用户详情（根据用户ID）
        /// GET: /api/back-user/{userId}
        /// </summary>
        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetUserDetailById(int userId)
        {
            if (userId <= 0)
            {
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = "用户ID无效"
                });
            }

            try
            {
                _logger.LogInformation($"获取用户详情|用户ID: {userId}");
                var result = await _userService.GetUserDetailByIdAsync(userId);

                if (result == null)
                {
                    return NotFound(new ApiResponse
                    {
                        Code = 404,
                        Message = "用户不存在"
                    });
                }

                return Ok(new ApiResponses<UserDetailDto>
                {
                    Code = 200,
                    Message = "获取成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取用户详情失败: {ex.Message}");
                return StatusCode(500, new ApiResponse
                {
                    Code = 500,
                    Message = "获取用户详情失败"
                });
            }
        }

        /// <summary>
        /// 获取用户详情（根据UserGuid）
        /// GET: /api/back-user/detail/guid?guid=xxx
        /// </summary>
        [HttpGet("detail/guid")]
        public async Task<IActionResult> GetUserDetailByGuid([FromQuery] string? guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return BadRequest(new ApiResponse
                {
                    Code = 400,
                    Message = "用户Guid不能为空"
                });
            }

            try
            {
                _logger.LogInformation($"获取用户详情|用户Guid: {guid}");
                var result = await _userService.GetUserDetailByGuidAsync(guid);

                if (result == null)
                {
                    return NotFound(new ApiResponse
                    {
                        Code = 404,
                        Message = "用户不存在"
                    });
                }

                return Ok(new ApiResponses<UserDetailDto>
                {
                    Code = 200,
                    Message = "获取成功",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取用户详情失败: {ex.Message}");
                return StatusCode(500, new ApiResponse
                {
                    Code = 500,
                    Message = "获取用户详情失败"
                });
            }
        }

        /// <summary>
        /// 修改当前登录管理员的密码
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest? request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return Ok(new ApiResponse { Code = 400, Message = "参数不能为空" });

            var userNo = GetAdminUserNo();
            if (userNo is null)
                return Unauthorized(new ApiResponse { Code = 401, Message = "登录已过期，请重新登录" });

            try
            {
                await _userService.ChangePasswordAsync(userNo, request.OldPassword, request.NewPassword);
                return Ok(new ApiResponse { Code = 200, Message = "密码修改成功" });
            }
            catch (Exception ex)
            {
                return Ok(new ApiResponse { Code = 400, Message = ex.Message });
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

        /// <summary>
        /// 从请求头中提取 Bearer token，验证并获取管理员账号
        /// </summary>
        private string? GetAdminUserNo()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader))
                return null;

            var token = authHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(token))
                return null;

            try
            {
                if (!_tokenService.ValidateToken(token))
                    return null;

                return _tokenService.GetUserIdFromToken(token);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion
    }
}
