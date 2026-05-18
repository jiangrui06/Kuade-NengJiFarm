using Microsoft.EntityFrameworkCore;

using ManageAPI.Common;
using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

public class ActivityService : IActivityService
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public ActivityService(AppDbContext dbContext, IWebHostEnvironment env)
    {
        _dbContext = dbContext;
        _env = env;
    }

    public async Task<(List<ActivityListItemDto> Records, int Total)> GetActivityListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Activities.AsNoTracking()
            .Where(a => a.IsdeleteId == 0);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordTrimmed = keyword.Trim();
            query = query.Where(a => a.Title.Contains(keywordTrimmed));
        }

        var total = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(a => a.SortOrder)
            .ThenByDescending(a => a.ActivityId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityListItemDto
            {
                Id = a.ActivityId,
                Name = a.Title,
                Type = GetTypeNameFromTypeId(a.TypeId),
                Price = a.Price,
                Status = MapStatusToText(a.StatusId),
                Image = a.ImageUrl ?? string.Empty,
                People = a.People,
                Duration = a.Duration,
                Location = a.Location,
                CreateTime = a.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            })
            .ToListAsync(cancellationToken);

        // Batch-load carousel media
        var activityIds = records.Select(r => r.Id).ToList();
        var materials = await _dbContext.ActivityMaterials
            .Where(m => activityIds.Contains(m.ActivityId))
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);
        var materialGroups = materials
            .GroupBy(m => m.ActivityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var r in records)
        {
            r.Image = MediaHelper.NormalizeImageUrl(r.Image);
            if (materialGroups.TryGetValue(r.Id, out var mats))
            {
                r.CarouselMedia = mats
                    .Where(m => m.MaterialType == "0" || m.MaterialType == "2")
                    .Select(m => new CarouselMediaDto
                    {
                        Type = m.MaterialType == "2" ? "video" : "image",
                        Url = MediaHelper.NormalizeImageUrl(m.MaterialUrl),
                    })
                    .Take(5)
                    .ToList();
            }
        }

        return (records, total);
    }

    public async Task<ActivityDetailDto?> GetActivityDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.IsdeleteId == 0, cancellationToken);

        if (activity is null)
            return null;

        var materials = await _dbContext.ActivityMaterials
            .Where(m => m.ActivityId == id)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        var carouselMedia = materials
            .Where(m => m.MaterialType == "0" || m.MaterialType == "2")
            .Select(m => new CarouselMediaDto
            {
                Type = m.MaterialType == "2" ? "video" : "image",
                Url = MediaHelper.NormalizeImageUrl(m.MaterialUrl),
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
            Image = MediaHelper.NormalizeImageUrl(activity.ImageUrl),
            ImageName = Path.GetFileName(MediaHelper.NormalizeImageUrl(activity.ImageUrl)),
            VideoUrl = MediaHelper.NormalizeImageUrl(activity.VideoUrl),
            Description = activity.Description,
            Location = activity.Location,
            People = activity.People,
            Content = activity.Content,
            Duration = activity.Duration,
            CarouselMedia = carouselMedia,
            CreateTime = activity.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        };
    }

    public async Task<long> CreateActivityAsync(CreateActivityDto dto, CancellationToken cancellationToken = default)
    {
        var activity = new ActivityEntity
        {
            Title = dto.Name,
            Price = dto.Price,
            ImageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath),
            VideoUrl = MediaHelper.ProcessImageData(dto.VideoUrl, _env.WebRootPath),
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
        };

        _dbContext.Activities.Add(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (dto.CarouselMedia?.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = m.Type == "video" ? "2" : "0",
                    MaterialUrl = MediaHelper.ProcessImageData(m.Url, _env.WebRootPath),
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow,
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
            .FirstOrDefaultAsync(a => a.ActivityId == id, cancellationToken);

        if (activity is null)
            return false;

        activity.Title = dto.Name;
        activity.Price = dto.Price;
        activity.ImageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath);
        activity.VideoUrl = MediaHelper.ProcessImageData(dto.VideoUrl, _env.WebRootPath);
        activity.Description = dto.Description;
        activity.Location = dto.Location;
        activity.People = dto.People;
        activity.Content = dto.Content;
        activity.Duration = dto.Duration;
        activity.StatusId = dto.StatusId;
        activity.TypeId = GetTypeIdFromType(dto.Type);

        // Replace all materials
        var oldMaterials = await _dbContext.ActivityMaterials
            .Where(m => m.ActivityId == id)
            .ToListAsync(cancellationToken);

        if (oldMaterials.Count > 0)
            _dbContext.ActivityMaterials.RemoveRange(oldMaterials);

        if (dto.CarouselMedia?.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = m.Type == "video" ? "2" : "0",
                    MaterialUrl = MediaHelper.ProcessImageData(m.Url, _env.WebRootPath),
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow,
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(materials);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteActivityAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.IsdeleteId == 0, cancellationToken);

        if (activity is null)
            return false;

        activity.IsdeleteId = 1;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteActivityBatchAsync(long[] ids, CancellationToken cancellationToken = default)
    {
        var activities = await _dbContext.Activities
            .Where(a => ids.Contains(a.ActivityId) && a.IsdeleteId == 0)
            .ToListAsync(cancellationToken);

        if (activities.Count == 0)
            return false;

        foreach (var a in activities)
            a.IsdeleteId = 1;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string MapStatusToText(int statusId)
    {
        return statusId switch
        {
            1 => "待付款",
            2 => "待核销",
            3 => "已核销",
            4 => "已取消",
            5 => "退款中",
            6 => "已退款",
            _ => "未知"
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
