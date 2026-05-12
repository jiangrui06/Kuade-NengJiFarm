using Microsoft.EntityFrameworkCore;

using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

/// <summary>
/// 活动/券品管理服务
/// </summary>
public class ActivityService : IActivityService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(AppDbContext dbContext, ILogger<ActivityService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(List<CouponListItemDto> Records, int Total)> GetActivityListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Activities
            .AsNoTracking()
            .Include(a => a.ActivityMaterials)
            .Where(a => a.StatusId != 1);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(a => a.Title.Contains(kw));
        }

        var total = await query.CountAsync(cancellationToken);

        var activities = await query
            .OrderByDescending(a => a.SortOrder)
            .ThenByDescending(a => a.ActivityId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var records = activities.Select(MapToListItem).ToList();

        return (records, total);
    }

    public async Task<CouponDetailDto?> GetActivityDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .Include(a => a.ActivityMaterials)
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.StatusId != 1, cancellationToken);

        if (activity is null)
            return null;

        var materials = activity.ActivityMaterials
            .OrderBy(m => m.SortOrder)
            .ToList();

        return MapToDetail(activity, materials);
    }

    public async Task<long> CreateActivityAsync(CreateCouponDto dto, CancellationToken cancellationToken = default)
    {
        var startDate = DateTime.Now;
        var endDate = dto.ValidityUnit switch
        {
            "天" => startDate.AddDays(dto.ValidityPeriod),
            "月" => startDate.AddMonths(dto.ValidityPeriod),
            "年" => startDate.AddYears(dto.ValidityPeriod),
            _ => startDate.AddDays(30)
        };

        var activity = new ActivityEntity
        {
            Title = dto.Name,
            Price = dto.Price,
            StartDate = startDate,
            EndDate = endDate,
            ImageUrl = dto.Image ?? string.Empty,
            StatusId = 2,
            TypeId = GetTypeIdFromType(dto.Type),
            SortOrder = 999,
            Stock = dto.Stock,
            LimitPerOrder = dto.LimitPerOrder,
            RefundRule = dto.RefundRule,
            UsageRules = dto.UsageRules,
            ActivityMaterials = []
        };

        _dbContext.Activities.Add(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"券品创建成功 - ActivityId: {activity.ActivityId}, Title: {activity.Title}");

        if (dto.CarouselMedia?.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = m.Url.EndsWith(".mp4") ? "2" : "0",
                    MaterialUrl = m.Url,
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(materials);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return activity.ActivityId;
    }

    public async Task<bool> UpdateActivityAsync(long id, UpdateCouponDto dto, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .Include(a => a.ActivityMaterials)
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.StatusId != 1, cancellationToken);

        if (activity is null)
            return false;

        activity.Title = dto.Name;
        activity.Price = dto.Price;
        activity.ImageUrl = dto.Image ?? string.Empty;
        activity.TypeId = GetTypeIdFromType(dto.Type);
        activity.Stock = dto.Stock;
        activity.LimitPerOrder = dto.LimitPerOrder;
        activity.RefundRule = dto.RefundRule;
        activity.UsageRules = dto.UsageRules;

        var startDate = DateTime.Now;
        var endDate = dto.ValidityUnit switch
        {
            "天" => startDate.AddDays(dto.ValidityPeriod),
            "月" => startDate.AddMonths(dto.ValidityPeriod),
            "年" => startDate.AddYears(dto.ValidityPeriod),
            _ => startDate.AddDays(30)
        };

        activity.StartDate = startDate;
        activity.EndDate = endDate;

        var oldMaterials = activity.ActivityMaterials.ToList();
        foreach (var material in oldMaterials)
        {
            _dbContext.ActivityMaterials.Remove(material);
        }

        if (dto.CarouselMedia?.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = m.Url.EndsWith(".mp4") ? "2" : "0",
                    MaterialUrl = m.Url,
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(materials);
        }

        _dbContext.Activities.Update(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"券品编辑成功 - ActivityId: {id}");

        return true;
    }

    public async Task<bool> DeleteActivityAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.StatusId != 1, cancellationToken);

        if (activity is null)
            return false;

        activity.StatusId = 1;
        _dbContext.Activities.Update(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"券品删除成功 - ActivityId: {id}");

        return true;
    }

    public async Task<bool> DeleteActivityBatchAsync(long[] ids, CancellationToken cancellationToken = default)
    {
        var activities = await _dbContext.Activities
            .Where(a => ids.Contains(a.ActivityId) && a.StatusId != 1)
            .ToListAsync(cancellationToken);

        if (activities.Count == 0)
            return false;

        foreach (var activity in activities)
        {
            activity.StatusId = 1;
        }

        _dbContext.Activities.UpdateRange(activities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"批量删除券品成功 - 数量: {activities.Count}");

        return true;
    }

    private CouponListItemDto MapToListItem(ActivityEntity activity)
    {
        var validity = CalculateValidityText(activity.StartDate, activity.EndDate);
        var (period, unit) = ParseValidityText(validity);
        var couponType = GetCouponTypeFromTypeId(activity.TypeId);

        var carouselMedia = activity.ActivityMaterials
            .Where(m => m.MaterialType == "0" || m.MaterialType == "2")
            .OrderBy(m => m.SortOrder)
            .Select(m => new CarouselMediaDto
            {
                Type = m.MaterialType == "2" ? "video" : "image",
                Url = m.MaterialUrl,
                Thumb = null
            })
            .Take(5)
            .ToList();

        return new CouponListItemDto
        {
            Id = activity.ActivityId,
            Name = activity.Title,
            Type = couponType,
            Price = activity.Price,
            Stock = activity.Stock ?? 0,
            LimitPerOrder = activity.LimitPerOrder ?? 0,
            ValidityPeriod = period,
            ValidityUnit = unit,
            Validity = validity,
            RefundRule = activity.RefundRule ?? "未使用可退款",
            UsageRules = activity.UsageRules ?? string.Empty,
            Image = activity.ImageUrl,
            CarouselMedia = carouselMedia,
            SoldCount = 0,
            VerifiedCount = 0,
            CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private CouponDetailDto MapToDetail(ActivityEntity activity, List<ActivityMaterial> materials)
    {
        var validity = CalculateValidityText(activity.StartDate, activity.EndDate);
        var (period, unit) = ParseValidityText(validity);
        var couponType = GetCouponTypeFromTypeId(activity.TypeId);

        var carouselMedia = materials
            .Where(m => m.MaterialType == "0" || m.MaterialType == "2")
            .Select(m => new CarouselMediaDto
            {
                Type = m.MaterialType == "2" ? "video" : "image",
                Url = m.MaterialUrl,
                Thumb = null
            })
            .Take(5)
            .ToList();

        return new CouponDetailDto
        {
            Id = activity.ActivityId,
            Name = activity.Title,
            Type = couponType,
            Price = activity.Price,
            Stock = activity.Stock ?? 0,
            LimitPerOrder = activity.LimitPerOrder ?? 0,
            ValidityPeriod = period,
            ValidityUnit = unit,
            Validity = validity,
            RefundRule = activity.RefundRule ?? "未使用可退款",
            UsageRules = activity.UsageRules ?? string.Empty,
            Image = activity.ImageUrl,
            ImageName = Path.GetFileName(activity.ImageUrl),
            CarouselMedia = carouselMedia,
            SoldCount = 0,
            VerifiedCount = 0,
            CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static string CalculateValidityText(DateTime startDate, DateTime endDate)
    {
        var days = (endDate - startDate).Days;

        if (days % 365 == 0)
            return $"{days / 365}年";
        else if (days % 30 == 0)
            return $"{days / 30}月";
        else
            return $"{days}天";
    }

    private static (int period, string unit) ParseValidityText(string validity)
    {
        if (string.IsNullOrEmpty(validity))
            return (30, "天");

        var numberStr = new string(validity.Where(char.IsDigit).ToArray());
        var unit = "天";

        if (!int.TryParse(numberStr, out var period))
            period = 30;

        if (validity.Contains("年"))
            unit = "年";
        else if (validity.Contains("月"))
            unit = "月";

        return (period, unit);
    }

    private int GetTypeIdFromType(string type)
    {
        return type switch
        {
            "采摘券" => 1,
            "研学活动券" => 2,
            _ => 3
        };
    }

    private string GetCouponTypeFromTypeId(int typeId)
    {
        return typeId switch
        {
            1 => "采摘券",
            2 => "研学活动券",
            _ => "活动券"
        };
    }
}
