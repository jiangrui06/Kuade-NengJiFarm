using Microsoft.EntityFrameworkCore;

using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

public class ActivityService : IActivityService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(AppDbContext dbContext, ILogger<ActivityService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(List<ActivityListItemDto> Records, int Total)> GetActivityListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbContext.Activities
            .AsNoTracking()
            .Where(a => a.StatusId == 1);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            baseQuery = baseQuery.Where(a => a.Title.Contains(kw));
        }

        var total = await baseQuery.CountAsync(cancellationToken);

        var activities = await baseQuery
            .Include(a => a.ActivityMaterials)
            .OrderByDescending(a => a.SortOrder)
            .ThenByDescending(a => a.ActivityId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var records = activities.Select(MapToListItem).ToList();

        return (records, total);
    }

    public async Task<ActivityDetailDto?> GetActivityDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .Include(a => a.ActivityMaterials)
            .FirstOrDefaultAsync(a => a.ActivityId == id, cancellationToken);

        if (activity is null)
            return null;

        var materials = activity.ActivityMaterials
            .OrderBy(m => m.SortOrder)
            .ToList();

        return MapToDetail(activity, materials);
    }

    public async Task<long> CreateActivityAsync(CreateActivityDto dto, CancellationToken cancellationToken = default)
    {
        var activity = new ActivityEntity
        {
            Title = dto.Name,
            Price = dto.Price,
            ImageUrl = dto.Image ?? string.Empty,
            VideoUrl = dto.VideoUrl ?? string.Empty,
            Description = dto.Description,
            Location = dto.Location,
            People = dto.People,
            Content = dto.Content,
            Duration = dto.Duration,
            StatusId = dto.StatusId,
            TypeId = GetTypeIdFromType(dto.Type),
            SortOrder = 999,
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays(30),
            CreatedAt = DateTime.Now,
            ActivityMaterials = new List<ActivityMaterial>()
        };

        _dbContext.Activities.Add(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"活动创建成功 - ActivityId: {activity.ActivityId}, Title: {activity.Title}");

        if (dto.CarouselMedia?.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = m.Type == "video" ? "2" : "0",
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

    public async Task<bool> UpdateActivityAsync(long id, UpdateActivityDto dto, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .Include(a => a.ActivityMaterials)
            .FirstOrDefaultAsync(a => a.ActivityId == id, cancellationToken);

        if (activity is null)
            return false;

        activity.Title = dto.Name;
        activity.Price = dto.Price;
        activity.ImageUrl = dto.Image ?? string.Empty;
        activity.VideoUrl = dto.VideoUrl ?? string.Empty;
        activity.Description = dto.Description;
        activity.Location = dto.Location;
        activity.People = dto.People;
        activity.Content = dto.Content;
        activity.Duration = dto.Duration;
        activity.StatusId = dto.StatusId;
        activity.TypeId = GetTypeIdFromType(dto.Type);

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
                    MaterialType = m.Type == "video" ? "2" : "0",
                    MaterialUrl = m.Url,
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(materials);
        }

        _dbContext.Activities.Update(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"活动编辑成功 - ActivityId: {id}");

        return true;
    }

    public async Task<bool> DeleteActivityAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id, cancellationToken);

        if (activity is null)
            return false;

        activity.StatusId = 2;
        _dbContext.Activities.Update(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"活动删除成功 - ActivityId: {id}");

        return true;
    }

    public async Task<bool> DeleteActivityBatchAsync(long[] ids, CancellationToken cancellationToken = default)
    {
        var activities = await _dbContext.Activities
            .Where(a => ids.Contains(a.ActivityId))
            .ToListAsync(cancellationToken);

        if (activities.Count == 0)
            return false;

        foreach (var activity in activities)
        {
            activity.StatusId = 2;
        }

        _dbContext.Activities.UpdateRange(activities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"批量删除活动成功 - 数量: {activities.Count}");

        return true;
    }

    private ActivityListItemDto MapToListItem(ActivityEntity activity)
    {
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

        return new ActivityListItemDto
        {
            Id = activity.ActivityId,
            Name = activity.Title,
            Type = GetTypeNameFromTypeId(activity.TypeId),
            Price = activity.Price,
            Status = MapStatusToText(activity.StatusId),
            Image = activity.ImageUrl,
            People = activity.People,
            Duration = activity.Duration,
            Location = activity.Location,
            CarouselMedia = carouselMedia,
            CreateTime = activity.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private ActivityDetailDto MapToDetail(ActivityEntity activity, List<ActivityMaterial> materials)
    {
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

        return new ActivityDetailDto
        {
            Id = activity.ActivityId,
            Name = activity.Title,
            Type = GetTypeNameFromTypeId(activity.TypeId),
            Price = activity.Price,
            StatusId = activity.StatusId,
            Status = MapStatusToText(activity.StatusId),
            Image = activity.ImageUrl,
            ImageName = Path.GetFileName(activity.ImageUrl),
            VideoUrl = activity.VideoUrl,
            Description = activity.Description,
            Location = activity.Location,
            People = activity.People,
            Content = activity.Content,
            Duration = activity.Duration,
            CarouselMedia = carouselMedia,
            CreateTime = activity.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static string MapStatusToText(int statusId)
    {
        return statusId switch
        {
            1 => "已上架",
            3 => "已售空",
            _ => "已下架"
        };
    }

    private static int GetTypeIdFromType(string type)
    {
        return type switch
        {
            "采摘券" => 1,
            "研学活动券" => 2,
            _ => 3
        };
    }

    private static string GetTypeNameFromTypeId(int typeId)
    {
        return typeId switch
        {
            1 => "采摘券",
            2 => "研学活动券",
            _ => "活动券"
        };
    }
}
