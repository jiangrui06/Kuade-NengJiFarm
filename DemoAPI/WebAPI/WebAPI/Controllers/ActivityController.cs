using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IContentService _contentService;

    public ActivityController(AppDbContext dbContext, IContentService contentService)
    {
        _dbContext = dbContext;
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult>> GetPageList(CancellationToken cancellationToken)
    {
        var allActivities = await LoadActivitySummariesAsync(cancellationToken);
        return Ok(ApiResult.Success(allActivities));
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResult>> List(CancellationToken cancellationToken)
    {
        var allActivities = await LoadActivitySummariesAsync(cancellationToken);
        var data = new ActivityListDto
        {
            Activities = new Dictionary<string, List<ActivitySummaryDto>>
            {
                ["all"] = allActivities
            }
        };

        return Ok(ApiResult.Success(data));
    }

    [HttpGet("detail")]
    public async Task<ActionResult<ApiResult>> Detail([FromQuery] int id, CancellationToken cancellationToken)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.ActivityId == id)
            .Select(x => new ActivitySummaryDto
            {
                Id = (int)x.ActivityId,
                Title = x.Title,
                Price = x.PriceText,
                Date = x.DateText,
                Image = x.ImageUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (activity is null)
        {
            return Ok(ApiResult.Fail("活动不存在", 404));
        }

        var detail = await _contentService.GetActivityDetailAsync(id, cancellationToken);
        var data = detail is null
            ? BuildDetailFallback(activity)
            : MergeDetail(detail, activity);

        return Ok(ApiResult.Success(data));
    }

    private async Task<List<ActivitySummaryDto>> LoadActivitySummariesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Activities
            .AsNoTracking()
            .Where(x => x.Status == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ActivityId)
            .Select(x => new ActivitySummaryDto
            {
                Id = (int)x.ActivityId,
                Title = x.Title,
                Price = x.PriceText,
                Date = x.DateText,
                Image = x.ImageUrl
            })
            .ToListAsync(cancellationToken);
    }

    private static ActivityDetailDto MergeDetail(ActivityDetailDto detail, ActivitySummaryDto summary)
    {
        detail.Id = summary.Id;
        detail.Title = summary.Title;
        detail.Price = summary.Price;
        detail.Date = summary.Date;
        detail.Image = summary.Image;
        detail.Images = string.IsNullOrWhiteSpace(summary.Image) ? [] : [summary.Image];
        return detail;
    }

    private static ActivityDetailDto BuildDetailFallback(ActivitySummaryDto summary)
    {
        return new ActivityDetailDto
        {
            Id = summary.Id,
            Title = summary.Title,
            Price = summary.Price,
            Date = summary.Date,
            Image = summary.Image,
            Images = string.IsNullOrWhiteSpace(summary.Image) ? [] : [summary.Image],
            Description = summary.Title,
            Location = string.Empty,
            People = string.Empty,
            Content = string.Empty
        };
    }
}
