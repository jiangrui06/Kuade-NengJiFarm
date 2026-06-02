using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;
using WebAPI.Filters;
using WebAPI.Services;

using static WebAPI.Common.ApiResult;

namespace WebAPI.Controllers;

[Route("api/[controller]")]
[RequireTokenType("kitchen")]
public class KitchenController : ControllerBase
{
    private readonly ILogger<KitchenController> _logger;
    private readonly IKitchenService _kitchenService;
    private readonly JwtHelper _jwtHelper;

    public KitchenController(
        ILogger<KitchenController> logger,
        IKitchenService kitchenService,
        JwtHelper jwtHelper)
    {
        _logger = logger;
        _kitchenService = kitchenService;
        _jwtHelper = jwtHelper;
    }

    /// <summary>
    /// 厨房登录
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult>> Login(
        [FromBody] KitchenLoginDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return Ok(ApiResult.Fail("账号或密码不能为空"));
            }

            var result = await _kitchenService.LoginAsync(dto.PhoneNumber, dto.Password, cancellationToken);

            if (result == null)
            {
                // 如果 Service 内部没有异常只是返回 null，说明没找到账号或密码错误
                return Ok(ApiResult.Fail("账号或密码错误"));
            }

            // 生成 Token
            var token = _jwtHelper.GenerateToken(new Entities.User
            {
                UserId = result.UserId,
                WxName = result.UserName,
                PhoneNumber = result.PhoneNumber
            }, "kitchen");

            _logger.LogInformation($"厨房登录成功 - 手机号: {dto.PhoneNumber}, UserId: {result.UserId}");

            return Ok(ApiResult.Success(new
            {
                token,
                userId = result.UserId,
                userName = result.UserName,
                phoneNumber = result.PhoneNumber
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError($"厨房登录失败: {ex.Message}");

            if (ex.Message.Contains("未注册"))
                return Ok(ApiResult.Fail("该手机号未注册"));

            if (ex.Message.Contains("禁用"))
                return Ok(ApiResult.Fail("账号或密码错误"));

            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 获取厨房订单列表
    /// </summary>
    [HttpGet("order/list")]
    public async Task<ActionResult<ApiResult>> GetOrderList(
        [FromQuery] int type = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (type != 2 && type != 3)
            {
                return Ok(ApiResult.Fail("type 参数值不正确，仅支持 2 (待出餐) 或 3 (已完成)"));
            }

            var result = await _kitchenService.GetTodayOrderListAsync(type, cancellationToken);

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取订单列表失败: {ex.Message}");
            return Ok(ApiResult.Fail("获取订单列表失败"));
        }
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    [HttpGet("order/detail")]
    //[Authorize]
    public async Task<ActionResult<ApiResult>> GetOrderDetail(
        [FromQuery] long orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (orderId <= 0)
            {
                return Ok(ApiResult.Fail("orderId 不能为空"));
            }

            var result = await _kitchenService.GetOrderDetailAsync(orderId, cancellationToken);

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取订单详情失败: {ex.Message}");

            if (ex.Message.Contains("不存在"))
                return Ok(ApiResult.Fail("订单不存在", 404));

            return Ok(ApiResult.Fail("获取订单详情失败"));
        }
    }

    /// <summary>
    /// 标记产品为已出餐（后厨接口）
    /// </summary>
    [HttpPost("dish/finish")]
    public async Task<ActionResult<ApiResult>> MarkDishFinish(
        [FromBody] MarkDishFinishDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            if (dto.DishOrderDetailsId <= 0)
            {
                return Ok(ApiResult.Fail("dishOrderDetailsId 不能为空"));
            }

            var result = await _kitchenService.MarkDishFinishAsync(dto.DishOrderDetailsId, cancellationToken);

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError($"产品出餐标记失败: {ex.Message}");

            if (ex.Message.Contains("不存在"))
                return Ok(ApiResult.Fail(ex.Message, 400));

            if (ex.Message.Contains("订单状态为"))
                return Ok(ApiResult.Fail(ex.Message));

            return Ok(ApiResult.Fail("产品出餐标记失败"));
        }
    }

    [HttpPost("dish/cancel")]
    public async Task<IActionResult> CancelDish([FromBody] CancelDishRequest request, CancellationToken ct)
    {
        var (success, message, data) = await _kitchenService.CancelDishAsync(request.DishOrderDetailsId, ct);

        if (!success)
        {
            // 失败时返回 400 或 404
            return Ok(new ApiResponse<object>
            {
                Code = 400,
                Message = message
            });
        }

        // 成功时按文档要求的格式
        return Ok(new ApiResponse<object>
        {
            Data = data
        });
    }

    /// <summary>
    /// 获取厨房统计数据
    /// </summary>
    [HttpGet("today-statistics")]
    public async Task<ActionResult<ApiResult>> GetTodayStatistics(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _kitchenService.GetTodayStatisticsAsync(cancellationToken);

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取统计数据失败: {ex.Message}");
            return Ok(ApiResult.Fail("获取统计数据失败"));
        }
    }

    /// <summary>
    /// 用户登出
    /// </summary>
    [HttpPost("logout")]
    public ActionResult<ApiResult> Logout()
    {
        try
        {
            _logger.LogInformation("厨房用户已登出");
            return Ok(ApiResult.Success("退出成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"登出失败: {ex.Message}");
            return Ok(ApiResult.Fail("登出失败"));
        }
    }

    /// <summary>
    /// 验证 token 有效性，返回当前用户信息
    /// </summary>
    [HttpGet("auth/verify")]
    public async Task<ActionResult<ApiResult>> VerifyToken(CancellationToken cancellationToken)
    {
        try
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("userId");
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
            {
                return Unauthorized(ApiResult.Fail("token已过期，请重新登录", 401));
            }

            var user = await _kitchenService.GetUserByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return Unauthorized(ApiResult.Fail("token已过期，请重新登录", 401));
            }

            var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            var expiresAt = DateTime.UtcNow.AddMinutes(120); // fallback

            if (!string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);
                    var expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
                    if (expClaim != null && long.TryParse(expClaim, out var expUnix))
                    {
                        expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    }
                }
                catch { }
            }

            return Ok(ApiResult.Success(new
            {
                userId = user.UserId,
                userName = user.UserName,
                expiresAt = expiresAt.ToString("o")
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Token 验证失败: {ex.Message}");
            return Unauthorized(ApiResult.Fail("token已过期，请重新登录", 401));
        }
    }
}
