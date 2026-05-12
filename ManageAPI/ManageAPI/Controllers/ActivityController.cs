using Microsoft.AspNetCore.Mvc;

using ManageAPI.Common;
using ManageAPI.Dtos;
using ManageAPI.Services;

namespace ManageAPI.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activityService;

    public ActivityController(IActivityService activityService)
    {
        _activityService = activityService;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var (records, total) = await _activityService.GetActivityListAsync(pageNum, pageSize, keyword, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            records,
            total,
            pageNum,
            pageSize,
            pages = (total + pageSize - 1) / pageSize
        }));
    }

    [HttpGet("detail")]
    public async Task<IActionResult> GetDetail(
        [FromQuery] long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Ok(ApiResult.Fail("参数不正确", 400));

        var activity = await _activityService.GetActivityDetailAsync(id, cancellationToken);

        if (activity is null)
            return Ok(ApiResult.Fail("活动不存在或已被删除", 404));

        return Ok(ApiResult.Success(activity));
    }

    [HttpPost("add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateActivityDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Ok(ApiResult.Fail("活动名称不能为空", 400));

        var id = await _activityService.CreateActivityAsync(dto, cancellationToken);
        return Ok(ApiResult.Success(new { id }));
    }

    [HttpPut("edit")]
    public async Task<IActionResult> Update(
        [FromBody] UpdateActivityDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.Name))
            return Ok(ApiResult.Fail("参数不能为空", 400));

        var success = await _activityService.UpdateActivityAsync(dto.Id, dto, cancellationToken);

        if (!success)
            return Ok(ApiResult.Fail("活动不存在或已被删除", 404));

        return Ok(ApiResult.Success("编辑成功"));
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Id <= 0)
            return Ok(ApiResult.Fail("参数不能为空", 400));

        var success = await _activityService.DeleteActivityAsync(request.Id, cancellationToken);

        if (!success)
            return Ok(ApiResult.Fail("活动不存在或已被删除", 404));

        return Ok(ApiResult.Success("删除成功"));
    }

    [HttpPost("deleteBatch")]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] DeleteBatchActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Ids == null || request.Ids.Length == 0)
            return Ok(ApiResult.Fail("参数不能为空", 400));

        var success = await _activityService.DeleteActivityBatchAsync(request.Ids, cancellationToken);

        if (!success)
            return Ok(ApiResult.Fail("删除失败", 404));

        return Ok(ApiResult.Success("删除成功"));
    }
}

public class DeleteActivityRequest
{
    public long Id { get; set; }
}

public class DeleteBatchActivityRequest
{
    public long[]? Ids { get; set; }
}
