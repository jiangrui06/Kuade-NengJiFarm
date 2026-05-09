using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Dtos.Kitchen;
using WebAPI.Services;

namespace WebAPI.Controllers;

[Route("api/[controller]")]
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
    /// 后厨登录
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
                // 这里根据你的 Service 层逻辑来决定提示语
                // 如果 Service 内部没抛异常只是返回 null，说明没找到人或密码错
                return Ok(ApiResult.Fail("账号或密码错误"));
            }

            // 生成 Token
            var token = _jwtHelper.GenerateToken(new Entities.User
            {
                UserId = result.UserId,
                WxName = result.UserName,
                PhoneNumber = result.PhoneNumber
            });

            _logger.LogInformation($"后厨登录成功 - 手机号: {dto.PhoneNumber}, UserId: {result.UserId}");

            return Ok(ApiResult.Success(new
            {
                token,
                user_id = result.UserId,
                user_name = result.UserName,
                phone_number = result.PhoneNumber
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError($"后厨登录失败: {ex.Message}");

            if (ex.Message.Contains("未注册"))
                return Ok(ApiResult.Fail("该手机号未注册"));

            if (ex.Message.Contains("密码"))
                return Ok(ApiResult.Fail("账号或密码错误"));

            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 获取今日订单列表
    /// </summary>
    [HttpGet("order/list")]
    public async Task<ActionResult<ApiResult>> GetOrderList(
        [FromQuery] int type = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (type != 0 && type != 1)
            {
                return Ok(ApiResult.Fail("type 参数值不正确，仅支持 0 或 1"));
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
    /// 标记菜品为已出餐（核心接口）
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
            _logger.LogError($"菜品出餐标记失败: {ex.Message}");

            if (ex.Message.Contains("不存在"))
                return Ok(ApiResult.Fail(ex.Message, 400));

            return Ok(ApiResult.Fail("菜品出餐标记失败"));
        }
    }

    /// <summary>
    /// 获取今日统计数据
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
    /// 后厨登出
    /// </summary>
    [HttpPost("logout")]
    public ActionResult<ApiResult> Logout()
    {
        try
        {
            _logger.LogInformation("后厨用户已登出");
            return Ok(ApiResult.Success("退出成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"登出失败: {ex.Message}");
            return Ok(ApiResult.Fail("登出失败"));
        }
    }
}