using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;
using WebAPI.Entities.Entities;

namespace WebAPI.Services;

/// <summary>
/// 活动/券品管理服务
/// </summary>
public class ActivityService : IActivityService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ActivityService> _logger;
    private readonly IInventoryStatsService _inventoryStatsService;

    public ActivityService(
        AppDbContext dbContext, 
        ILogger<ActivityService> logger,
        IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _inventoryStatsService = inventoryStatsService;
    }

    /// <summary>
    /// 获取活动分页列表
    /// </summary>
    public async Task<(List<ActivitySummaryDto> Records, int Total)> GetActivityListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Activities
            .AsNoTracking()
            .Where(a => a.StatusId == 1);  // 排除已删除（StatusId = 1表示已删除）

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(a => a.Title.Contains(kw));
        }

        var total = await query.CountAsync(cancellationToken);

        var activities = await query
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.ActivityId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.ActivityId,
                a.Title,
                a.Price,
                a.ImageUrl,
                a.StartDate,
                a.EndDate,
                a.TypeId
            })
            .ToListAsync(cancellationToken);

        var records = activities.Select(a => MapToActivitySummary(a.ActivityId, a.Title, a.Price, 
            a.ImageUrl, a.StartDate, a.EndDate, a.TypeId)).ToList();

        return (records, total);
    }

    /// <summary>
    /// 获取活动详情
    /// </summary>
    public async Task<ActivityDetailDto?> GetActivityDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .Include(a => a.ActivityMaterials)
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.StatusId == 1, cancellationToken);

        if (activity is null)
            return null;

        // 获取参与统计（如果需要）
        var stats = (await _inventoryStatsService.GetActivityStatsAsync(
            new[] { (int)activity.ActivityId }, cancellationToken))
            .GetValueOrDefault((int)activity.ActivityId);

        // 构建详情
        var detail = new ActivityDetailDto
        {
            Id = (int)activity.ActivityId,
            Title = activity.Title,
            Price = $"?{activity.Price}",
            Date = FormatDateRange(activity.StartDate, activity.EndDate),
            Image = activity.ImageUrl ?? string.Empty,
            CategoryName = ResolveCategoryName(activity.Title),
            Description = activity.Title,
            Location = "农场优选生态农场",
            People = "不限",
            Content = "详见活动说明",
            Images = ExtractMediaUrls(activity.ActivityMaterials)
                .Take(4)
                .ToList()
        };

        // 确保至少有4张图
        while (detail.Images.Count > 0 && detail.Images.Count < 4)
        {
            detail.Images.Add(detail.Images[detail.Images.Count - 1]);
        }

        if (detail.Images.Count == 0 && !string.IsNullOrEmpty(detail.Image))
        {
            detail.Images = Enumerable.Repeat(detail.Image, 4).ToList();
        }

        return detail;
    }

    /// <summary>
    /// 活动报名
    /// </summary>
    public ActivityRegisterResponse RegisterActivity(long activityId)
    {
        var orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new ActivityRegisterResponse
        {
            Id = orderId,
            OrderId = orderId,
            ActivityId = activityId,
            PaymentStatus = "pending_payment"
        };
    }

    /// <summary>
    /// 获取所有活动（按分类）
    /// </summary>
    public async Task<Dictionary<string, List<ActivitySummaryDto>>> GetAllActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var activities = await _dbContext.Activities
            .AsNoTracking()
            .Where(a => a.StatusId != 1)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.ActivityId)
            .Select(a => new
            {
                a.ActivityId,
                a.Title,
                a.Price,
                a.ImageUrl,
                a.StartDate,
                a.EndDate,
                a.TypeId
            })
            .ToListAsync(cancellationToken);

        var all = activities
            .Select(a => MapToActivitySummary(a.ActivityId, a.Title, a.Price, 
                a.ImageUrl, a.StartDate, a.EndDate, a.TypeId))
            .ToList();

        var result = new Dictionary<string, List<ActivitySummaryDto>>
        {
            ["all"] = all
        };

        // 按类型分组
        var picking = all.Where(x => IsPickingActivity(x.Title)).ToList();
        if (picking.Count > 0)
            result["picking"] = picking;

        var camping = all.Where(x => IsCampingActivity(x.Title)).ToList();
        if (camping.Count > 0)
            result["camping"] = camping;

        return result;
    }

    /// <summary>
    /// 获取活动素材URL
    /// </summary>
    private static List<string> ExtractMediaUrls(ICollection<ActivityMaterial> materials)
    {
        return materials
            .Where(m => m.MaterialType == "0")  // 0=图片
            .OrderBy(m => m.SortOrder)
            .Select(m => m.MaterialUrl)
            .Where(url => !string.IsNullOrEmpty(url))
            .ToList();
    }

    /// <summary>
    /// 映射为活动摘要
    /// </summary>
    private static ActivitySummaryDto MapToActivitySummary(long id, string title, decimal price, 
        string? imageUrl, DateTime startDate, DateTime endDate, int typeId)
    {
        return new ActivitySummaryDto
        {
            Id = (int)id,
            Title = title,
            Price = $"?{price}",
            Date = FormatDateRange(startDate, endDate),
            Image = imageUrl ?? string.Empty,
            CategoryName = ResolveCategoryName(title)
        };
    }

    /// <summary>
    /// 格式化日期范围
    /// </summary>
    private static string FormatDateRange(DateTime startDate, DateTime endDate)
    {
        return $"{startDate:yyyy.M.d}-{endDate:yyyy.M.d}";
    }

    /// <summary>
    /// 从标题推断分类
    /// </summary>
    private static string ResolveCategoryName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "活动";

        if (IsPickingActivity(title))
            return "采摘活动";

        if (IsCampingActivity(title))
            return "露营";

        return "活动";
    }

    /// <summary>
    /// 是否为采摘活动
    /// </summary>
    private static bool IsPickingActivity(string title)
    {
        return title.Contains("采摘", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("picking", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为露营活动
    /// </summary>
    private static bool IsCampingActivity(string title)
    {
        return title.Contains("露营", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("camping", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 活动报名响应
/// </summary>
public class ActivityRegisterResponse
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ActivityId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}