using Microsoft.AspNetCore.Mvc;
using WebAPI.Common;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

/// <summary>
/// 活动/券品控制器
/// </summary>
[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(IActivityService activityService, ILogger<ActivityController> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// 获取活动列表（分页）
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResult>> GetPageList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (records, total) = await _activityService.GetActivityListAsync(
                pageNum, pageSize, keyword, cancellationToken);

            return Ok(ApiResult.Success(new
            {
                records,
                total,
                pageNum,
                pageSize,
                pages = (total + pageSize - 1) / pageSize
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取活动列表失败: {ex.Message}");
            return Ok(ApiResult.Fail("获取失败", 500));
        }
    }

    /// <summary>
    /// 获取所有活动（按分类）
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<ApiResult>> List(CancellationToken cancellationToken = default)
    {
        try
        {
            var allActivities = await _activityService.GetAllActivitiesAsync(cancellationToken);

            var data = new ActivityListDto
            {
                Activities = allActivities
            };

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取活动列表失败: {ex.Message}");
            return Ok(ApiResult.Fail("获取失败", 500));
        }
    }

    /// <summary>
    /// 获取活动详情
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<ApiResult>> Detail(
        [FromQuery] long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                return Ok(ApiResult.Fail("活动ID不能为空", 400));

            var detail = await _activityService.GetActivityDetailAsync(id, cancellationToken);
            if (detail is null)
                return Ok(ApiResult.Fail("活动不存在", 404));

            return Ok(ApiResult.Success(detail));
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取活动详情失败: {ex.Message}");
            return Ok(ApiResult.Fail("获取失败", 500));
        }
    }

    /// <summary>
    /// 活动报名
    /// </summary>
    [HttpPost("{id:long}/register")]
    public ActionResult<ApiResult> Register([FromRoute] long id)
    {
        try
        {
            if (id <= 0)
                return Ok(ApiResult.Fail("活动ID参数不正确", 400));

            var response = _activityService.RegisterActivity(id);
            return Ok(ApiResult.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError($"活动报名失败: {ex.Message}");
            return Ok(ApiResult.Fail("报名失败", 500));
        }
    }
}
