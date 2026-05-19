using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;
using WebAPI.Services;

using static WebAPI.Common.ApiResult;

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
    /// �����¼
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
                return Ok(ApiResult.Fail("�˺Ż����벻��Ϊ��"));
            }

            var result = await _kitchenService.LoginAsync(dto.PhoneNumber, dto.Password, cancellationToken);

            if (result == null)
            {
                // ���������� Service ���߼���������ʾ��
                // ��� Service �ڲ�û���쳣ֻ�Ƿ��� null��˵��û�ҵ��˻������
                return Ok(ApiResult.Fail("�˺Ż��������"));
            }

            // ���� Token
            var token = _jwtHelper.GenerateToken(new Entities.User
            {
                UserId = result.UserId,
                WxName = result.UserName,
                PhoneNumber = result.PhoneNumber
            });

            _logger.LogInformation($"�����¼�ɹ� - �ֻ���: {dto.PhoneNumber}, UserId: {result.UserId}");

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
            _logger.LogError($"�����¼ʧ��: {ex.Message}");

            if (ex.Message.Contains("δע��"))
                return Ok(ApiResult.Fail("���ֻ���δע��"));

            if (ex.Message.Contains("����"))
                return Ok(ApiResult.Fail("�˺Ż��������"));

            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    /// <summary>
    /// ��ȡ���ն����б�
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
            _logger.LogError($"��ȡ�����б�ʧ��: {ex.Message}");
            return Ok(ApiResult.Fail("��ȡ�����б�ʧ��"));
        }
    }

    /// <summary>
    /// ��ȡ��������
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
                return Ok(ApiResult.Fail("orderId ����Ϊ��"));
            }

            var result = await _kitchenService.GetOrderDetailAsync(orderId, cancellationToken);

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError($"��ȡ��������ʧ��: {ex.Message}");

            if (ex.Message.Contains("������"))
                return Ok(ApiResult.Fail("����������", 404));

            return Ok(ApiResult.Fail("��ȡ��������ʧ��"));
        }
    }

    /// <summary>
    /// ��ǲ�ƷΪ�ѳ��ͣ����Ľӿڣ�
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
                return Ok(ApiResult.Fail("dishOrderDetailsId ����Ϊ��"));
            }

            var result = await _kitchenService.MarkDishFinishAsync(dto.DishOrderDetailsId, cancellationToken);

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError($"��Ʒ���ͱ��ʧ��: {ex.Message}");

            if (ex.Message.Contains("������"))
                return Ok(ApiResult.Fail(ex.Message, 400));

            return Ok(ApiResult.Fail("��Ʒ���ͱ��ʧ��"));
        }
    }

    [HttpPost("dish/cancel")]
    public async Task<IActionResult> CancelDish([FromBody] CancelDishRequest request, CancellationToken ct)
    {
        var (success, message, data) = await _kitchenService.CancelDishAsync(request.DishOrderDetailsId, ct);

        if (!success)
        {
            // ʧ���r���� 400 �� 404
            return Ok(new ApiResponse<object>
            {
                Code = 400,
                Message = message
            });
        }

        // �ɹ��r�����ęnҪ��ĸ�ʽ
        return Ok(new ApiResponse<object>
        {
            Data = data
        });
    }

    /// <summary>
    /// ��ȡ����ͳ������
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
            _logger.LogError($"��ȡͳ������ʧ��: {ex.Message}");
            return Ok(ApiResult.Fail("��ȡͳ������ʧ��"));
        }
    }

    /// <summary>
    /// ����ǳ�
    /// </summary>
    [HttpPost("logout")]
    public ActionResult<ApiResult> Logout()
    {
        try
        {
            _logger.LogInformation("����û��ѵǳ�");
            return Ok(ApiResult.Success("�˳��ɹ�"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"�ǳ�ʧ��: {ex.Message}");
            return Ok(ApiResult.Fail("�ǳ�ʧ��"));
        }
    }
}