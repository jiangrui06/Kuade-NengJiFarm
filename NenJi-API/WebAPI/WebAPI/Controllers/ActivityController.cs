using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IContentService _contentService;
    private readonly IInventoryStatsService _inventoryStatsService;

    private static Dictionary<int, string>? _activityTypeCache;
    private static readonly object _activityTypeCacheLock = new();

    public ActivityController(AppDbContext dbContext, IContentService contentService, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _contentService = contentService;
        _inventoryStatsService = inventoryStatsService;
    }

    private async Task EnsureActivityTypeCacheAsync(CancellationToken ct)
    {
        if (_activityTypeCache is not null) return;
        lock (_activityTypeCacheLock)
        {
            if (_activityTypeCache is not null) return;
        }
        var types = await _dbContext.ActivityTypes
            .AsNoTracking()
            .ToDictionaryAsync(x => x.ActivityTypeId, x => x.TypeName, ct);
        lock (_activityTypeCacheLock)
        {
            _activityTypeCache = types;
        }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult>> GetPageList(CancellationToken cancellationToken)
    {
        await EnsureActivityTypeCacheAsync(cancellationToken);
        var allActivities = await LoadActivitySummariesAsync(cancellationToken, _activityTypeCache);
        return Ok(ApiResult.Success(allActivities));
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResult>> List(CancellationToken cancellationToken)
    {
        await EnsureActivityTypeCacheAsync(cancellationToken);

        var categories = _activityTypeCache!
            .OrderBy(x => x.Key)
            .Select(x => new ActivityCategoryDto { Id = x.Key, Name = x.Value })
            .ToList();

        var allActivities = await LoadActivitySummariesAsync(cancellationToken, _activityTypeCache);

        var activities = new Dictionary<string, List<ActivitySummaryDto>>
        {
            ["all"] = allActivities
        };

        foreach (var category in categories)
        {
            var grouped = allActivities
                .Where(a => a.CategoryName == category.Name)
                .ToList();
            if (grouped.Count > 0)
            {
                activities[category.Name] = grouped;
            }
        }

        var data = new ActivityListDto
        {
            Categories = categories,
            Activities = activities
        };

        return Ok(ApiResult.Success(data));
    }

    [HttpGet("detail")]
    public async Task<ActionResult<ApiResult>> Detail([FromQuery] int id, CancellationToken cancellationToken)
    {
        await EnsureActivityTypeCacheAsync(cancellationToken);

        var activity = await _dbContext.Activities
            .AsNoTracking()
            .Where(x => x.StatusId == 1 && x.ActivityId == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (activity is null)
        {
            return Ok(ApiResult.Fail("活动不存在", 404));
        }

        var activityStats = (await _inventoryStatsService.GetActivityStatsAsync([id], cancellationToken))
            .GetValueOrDefault(id);

        var categoryName = _activityTypeCache?.GetValueOrDefault(activity.TypeId);

        var activitySummary = new ActivityDetailSummary
        {
            Id = (int)activity.ActivityId,
            Title = activity.Title,
            Price = $"¥{activity.Price:0.##}",
            Date = $"{activity.StartDate:MM.dd}-{activity.EndDate:MM.dd}",
            StartDate = $"{activity.StartDate:yyyy-MM-dd HH:mm}",
            EndDate = $"{activity.EndDate:yyyy-MM-dd HH:mm}",
            Image = activity.ImageUrl,
            CategoryName = categoryName,
            Participants = activityStats?.Participants ?? 0,
            RemainingSlots = activityStats?.RemainingSlots ?? activity.People,
            Video = activity.VideoUrl ?? string.Empty
        };

        var detail = await _contentService.GetActivityDetailAsync(id, cancellationToken);
        var data = detail is null
            ? BuildDetailFallback(activitySummary, activity)
            : MergeDetail(detail, activitySummary);

        // 处理详情中的所有媒体链接
        if (data != null)
        {
            data.Image = NormalizeMediaUrl(data.Image) ?? string.Empty;
            data.Images = data.Images?.Select(x => NormalizeMediaUrl(x) ?? string.Empty).ToList() ?? [];
            data.Video = NormalizeMediaUrl(data.Video) ?? string.Empty;
        }

        return Ok(ApiResult.Success(data));
    }

    [HttpPost("{id:int}/register")]
    [Authorize]
    public async Task<IActionResult> Register(
        int id,
        [FromBody] RegisterActivityRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Ok(ApiResult.Fail("活动 id 参数不正确", 400));
        }

        var tickets = request?.Tickets ?? 0;
        if (tickets <= 0)
        {
            return Ok(ApiResult.Fail("tickets 参数不正确", 400));
        }

        var userId = ResolveCurrentUserId();

        var activity = await _dbContext.Activities
            .FirstOrDefaultAsync(x => x.StatusId == 1 && x.ActivityId == id, cancellationToken);

        if (activity is null)
        {
            return Ok(ApiResult.Fail("活动不存在", 404));
        }

        var stats = (await _inventoryStatsService.GetActivityStatsAsync([id], cancellationToken))
            .GetValueOrDefault(id);
        var remainingSlots = stats?.RemainingSlots ?? activity.People;

        if (tickets > remainingSlots)
        {
            return Ok(ApiResult.Fail($"购票数量不能超过剩余 {remainingSlots} 个名额", 409));
        }

        var now = DateTime.Now;
        var order = new ActivityOrder
        {
            OrderNo = GenerateActivityOrderNo(),
            WxPayNo = null,
            TotalAmount = activity.Price * tickets,
            TotalQuantity = tickets,
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now
        };

        _dbContext.ActivityOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.ActivityOrderDetails.Add(new ActivityOrderDetail
        {
            ActivityOrderId = order.OrderId,
            ActivityId = activity.ActivityId,
            UnitPrice = activity.Price,
            Quantity = tickets,
            SubtotalAmount = activity.Price * tickets,
            ActivityQrcode = null
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            id = order.OrderNo,
            orderId = order.OrderNo,
            orderNumber = order.OrderNo,
            activityId = id,
            paymentStatus = "pending"
        }));
    }

    private async Task<List<ActivitySummaryDto>> LoadActivitySummariesAsync(CancellationToken cancellationToken, Dictionary<int, string>? typeMap = null)
    {
        var rows = await _dbContext.Activities
            .AsNoTracking()
            .Where(x => x.StatusId == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ActivityId)
            .Select(x => new
            {
                x.ActivityId,
                x.Title,
                x.Price,
                x.StartDate,
                x.EndDate,
                x.ImageUrl,
                x.TypeId
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new ActivitySummaryDto
        {
            Id = (int)x.ActivityId,
            Title = x.Title,
            Price = $"¥{x.Price:0.##}",
            Date = $"{x.StartDate:MM.dd}-{x.EndDate:MM.dd}",
            StartDate = $"{x.StartDate:yyyy-MM-dd HH:mm}",
            EndDate = $"{x.EndDate:yyyy-MM-dd HH:mm}",
            Image = NormalizeMediaUrl(x.ImageUrl) ?? string.Empty,
            CategoryName = typeMap?.GetValueOrDefault(x.TypeId)
        }).ToList();
    }

    private string? NormalizeMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();

        // 如果已经是完整的 URL，直接处理可能的重复前缀并返回
        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var duplicateHttps = trimmed.IndexOf("https://", 8, StringComparison.OrdinalIgnoreCase);
            if (duplicateHttps > 0) trimmed = trimmed[..duplicateHttps];

            var duplicateHttp = trimmed.IndexOf("http://", 7, StringComparison.OrdinalIgnoreCase);
            if (duplicateHttp > 0) trimmed = trimmed[..duplicateHttp];

            return trimmed.Trim();
        }

        // 处理本地文件名，拼接完整的 API 访问路径
        trimmed = trimmed.TrimStart('/', '\\');
        if (trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            var fileOnly = Path.GetFileName(trimmed);
            if (!string.IsNullOrWhiteSpace(fileOnly))
            {
                trimmed = fileOnly;
            }
        }
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var ext = Path.GetExtension(trimmed).ToLowerInvariant();

        // 视频文件
        if (ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".mkv" || ext == ".wmv")
        {
            return $"{baseUrl}/api/file/video/{trimmed}";
        }

        // 默认作为图片处理
        return $"{baseUrl}/api/file/image/{trimmed}";
    }

    private static ActivityDetailDto MergeDetail(ActivityDetailDto detail, ActivityDetailSummary summary)
    {
        detail.Id = summary.Id;
        detail.Title = summary.Title;
        detail.Price = summary.Price;
        detail.Date = summary.Date;
        detail.StartDate = summary.StartDate;
        detail.EndDate = summary.EndDate;
        detail.Image = summary.Image;
        detail.CategoryName = summary.CategoryName;
        detail.Images = EnsureFourImages(detail.Images, summary.Image);
        detail.Participants = summary.Participants;
        detail.RemainingSlots = summary.RemainingSlots;
        detail.Video = summary.Video;
        return detail;
    }

    private static ActivityDetailDto BuildDetailFallback(ActivityDetailSummary summary, ActivityEntity activity)
    {
        return new ActivityDetailDto
        {
            Id = summary.Id,
            Title = summary.Title,
            Price = summary.Price,
            Date = summary.Date,
            StartDate = summary.StartDate,
            EndDate = summary.EndDate,
            Image = summary.Image,
            Images = EnsureFourImages([], summary.Image),
            CategoryName = summary.CategoryName,
            Description = activity.Description,
            Location = activity.Location,
            People = activity.People > 0 ? $"{activity.People}人" : string.Empty,
            Content = activity.Content ?? string.Empty,
            Participants = summary.Participants,
            RemainingSlots = summary.RemainingSlots,
            Video = summary.Video
        };
    }

    private static List<string> EnsureFourImages(IEnumerable<string>? images, string? fallbackImage)
    {
        var result = (images ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => !IsPlaceholderImage(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        var fallback = string.IsNullOrWhiteSpace(fallbackImage) ? string.Empty : fallbackImage.Trim();
        if (result.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
        {
            result.Add(fallback);
        }

        while (result.Count > 0 && result.Count < 4)
        {
            result.Add(result[result.Count - 1]);
        }

        return result;
    }

    private static bool IsPlaceholderImage(string value)
    {
        return value.Contains("text_to_image", StringComparison.OrdinalIgnoreCase)
               || value.Equals("null", StringComparison.OrdinalIgnoreCase)
               || value.Equals("undefined", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ActivityDetailSummary : ActivitySummaryDto
    {
        public int Participants { get; set; }
        public int RemainingSlots { get; set; }
        public string Video { get; set; } = string.Empty;
    }

    public sealed class RegisterActivityRequest
    {
        public int Tickets { get; set; }
    }

    private int ResolveCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static string GenerateActivityOrderNo()
    {
        return $"ACT{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }
}
