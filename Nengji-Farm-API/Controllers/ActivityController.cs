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
    private readonly IInventoryStatsService _inventoryStatsService;

    private static Dictionary<int, string>? _activityTypeCache;
    private static readonly object _activityTypeCacheLock = new();

    public ActivityController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
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

        // 从 activity_material 表获取图片素材
        var materials = await _dbContext.ActivityMaterials
            .AsNoTracking()
            .Where(x => x.ActivityId == id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        // material_type: 0=轮播图, 1=详情图, 2=主页图片, null=视频
        var mainImg = materials
            .Where(x => x.MaterialType == 2)
            .Select(x => NormalizeMediaUrl(x.MaterialUrl))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? NormalizeMediaUrl(activity.ImageUrl)
            ?? string.Empty;

        var carouselImgs = materials
            .Where(x => x.MaterialType == 0)
            .Select(x => NormalizeMediaUrl(x.MaterialUrl))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        var detailImgs = materials
            .Where(x => x.MaterialType == 1)
            .Select(x => NormalizeMediaUrl(x.MaterialUrl))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        var videoUrl = materials
            .Where(x => x.MaterialType == null && !string.IsNullOrWhiteSpace(x.VideoUrl))
            .Select(x => NormalizeMediaUrl(x.VideoUrl))
            .FirstOrDefault()
            ?? string.Empty;

        // 规格图 (material_type = 3)
        var specImgs = materials
            .Where(x => x.MaterialType == 3)
            .Select(x => NormalizeMediaUrl(x.MaterialUrl))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        // 详情图列表：详情图优先，其次轮播图，最后主图兜底（不再强制补满4张）
        var allImages = detailImgs.Count > 0 ? detailImgs : (carouselImgs.Count > 0 ? carouselImgs : new List<string>());
        if (allImages.Count == 0 && !string.IsNullOrWhiteSpace(mainImg))
        {
            allImages = new List<string> { mainImg };
        }

        var data = new ActivityDetailDto
        {
            Id = (int)activity.ActivityId,
            Title = activity.Title,
            Price = $"¥{activity.Price:0.##}",
            Date = $"{activity.StartDate:MM.dd}-{activity.EndDate:MM.dd}",
            StartDate = $"{activity.StartDate:yyyy-MM-dd HH:mm}",
            EndDate = $"{activity.EndDate:yyyy-MM-dd HH:mm}",
            Image = mainImg,
            Images = allImages,
            SpecImages = specImgs,
            CategoryName = categoryName ?? string.Empty,
            Description = activity.Description,
            Location = activity.Location,
            People = activity.People > 0 ? $"{activity.People}人" : string.Empty,
            Content = activity.Content ?? string.Empty,
            Participants = activityStats?.Participants ?? 0,
            RemainingSlots = activityStats?.RemainingSlots ?? activity.People,
            Video = videoUrl
        };

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
            .FirstOrDefaultAsync(x => x.IsDelete == 0 && x.StatusId == 1 && x.ActivityId == id, cancellationToken);

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
                x.TypeId,
                x.Duration
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
            CategoryName = typeMap?.GetValueOrDefault(x.TypeId) ?? string.Empty,
            Duration = x.Duration
        }).ToList();
    }

    private static string? NormalizeMediaUrl(string? url) => MediaUrlHelper.Normalize(url) is { Length: > 0 } r ? r : null;

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
