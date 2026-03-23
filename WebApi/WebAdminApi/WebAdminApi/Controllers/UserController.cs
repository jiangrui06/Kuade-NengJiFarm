using Microsoft.AspNetCore.Mvc;
using WebAdminApi.DTOs;
using WebAdminApi.Services;

namespace WebAdminApi.Controllers
{
    [ApiController]
    [Route("api/user")]
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
        /// 接口1：获取用户列表（分页/全量）
        /// </summary>
        /// <param name="keyword">搜索关键词，匹配昵称/手机号</param>
        [HttpGet("list")]
        public IActionResult GetUserList([FromQuery] string? keyword)
        {
            try
            {
                _logger.LogInformation($"获取用户列表，关键词: {keyword}");
                var users = _userService.GetUserList(keyword);

                return Ok(new ApiResponse<List<UserListItemDto>>
                {
                    Code = 200,
                    Message = "获取成功",
                    Data = users
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
        /// 接口2：新增用户
        /// </summary>
        [HttpPost("add")]
        public IActionResult AddUser([FromBody] AddUserDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "请求参数不正确"
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

                _logger.LogInformation($"新增用户，手机号: {dto.Phone}，昵称: {dto.Nickname}");
                _userService.AddUser(dto);

                return Ok(new ApiResponse
                {
                    Code = 200,
                    Message = "新增成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"新增用户失败: {ex.Message}");
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
        public IActionResult EditUser([FromBody] EditUserDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                _logger.LogInformation($"编辑用户，用户ID: {dto.Id}");
                _userService.EditUser(dto);

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
        public IActionResult ChangeStatus([FromBody] ChangeStatusDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                if (dto.Status != "启用" && dto.Status != "禁用")
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "状态值不正确，只能是'启用'或'禁用'"
                    });
                }

                _logger.LogInformation($"修改用户状态，用户ID: {dto.Id}，目标状态: {dto.Status}");
                _userService.ChangeUserStatus(dto.Id, dto.Status);

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
        public IActionResult DeleteUser([FromBody] DeleteUserDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    return BadRequest(new ApiResponse
                    {
                        Code = 400,
                        Message = "用户ID不能为空"
                    });
                }

                _logger.LogInformation($"删除用户，用户ID: {dto.Id}");
                _userService.DeleteUser(dto.Id);

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