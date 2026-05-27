using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

// ===================================================================
// ActivityService — 活动券 CRUD 业务逻辑
//
// 本次改动：
//   1. Create/Update 支持 StartDate / EndDate / LimitPerOrder
//   2. Detail 返回 StartDate / EndDate / LimitPerOrder / Stock
//   3. List 返回 StartDate / EndDate / LimitPerOrder
//   4. type 映射改为 采摘体验 / 亲子研学（兼容旧值 采摘券 / 研学活动券）
//   5. 新增 CalculateStock() 方法
// ===================================================================

public class ActivityService : IActivityService
{
    private readonly ManageAppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public ActivityService(ManageAppDbContext dbContext, IWebHostEnvironment env)
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

        var (idToName, _) = await LoadTypeMappingAsync(cancellationToken);
        var (statusIdToName, _) = await LoadStatusMappingAsync(cancellationToken);

        var rawRecords = await query
            .OrderByDescending(a => a.SortOrder)
            .ThenByDescending(a => a.ActivityId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.ActivityId,
                a.Title,
                a.TypeId,
                a.Price,
                a.StatusId,
                a.ImageUrl,
                a.People,
                a.Duration,
                a.Location,
                a.CreatedAt,
                a.StartDate,
                a.EndDate,
            })
            .ToListAsync(cancellationToken);

        var pageActivityIds = rawRecords.Select(r => r.ActivityId).ToList();

        // 查询当前页活动券的已售数量（状态=待核销2 / 已核销3）
        var soldLookup = new Dictionary<long, int>();
        var verifiedLookup = new Dictionary<long, int>();
        if (pageActivityIds.Count > 0)
        {
            var idList = string.Join(",", pageActivityIds);
            var conn = _dbContext.Database.GetDbConnection();
            var needClose = conn.State != System.Data.ConnectionState.Open;
            if (needClose)
                await conn.OpenAsync(cancellationToken);

            var soldCmd = conn.CreateCommand();
            soldCmd.CommandText = $@"SELECT d.activity_id, COALESCE(SUM(d.quantity),0) AS Cnt
FROM activity_order_detail d JOIN activity_orders o ON d.activity_order_id = o.order_id
WHERE o.order_status_id IN (2,3) AND d.activity_id IN ({idList})
GROUP BY d.activity_id";
            using (var reader = await soldCmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var aid = reader.GetInt64(0);
                    var cnt = reader.GetInt32(1);
                    soldLookup[aid] = cnt;
                }
            }

            var verifiedCmd = conn.CreateCommand();
            verifiedCmd.CommandText = $@"SELECT d.activity_id, COUNT(*) AS Cnt
FROM activity_verification_record v
JOIN activity_order_detail d ON v.activity_order_details_id = d.activity_order_details_id
WHERE d.activity_id IN ({idList})
GROUP BY d.activity_id";
            using (var reader = await verifiedCmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var aid = reader.GetInt64(0);
                    var cnt = reader.GetInt32(1);
                    verifiedLookup[aid] = cnt;
                }
            }
        }

        var records = rawRecords.Select(a => new ActivityListItemDto
        {
            Id = a.ActivityId,
            Name = a.Title,
            Type = idToName.TryGetValue(a.TypeId, out var tn) ? tn : "其他",
            Price = a.Price,
            Status = MapStatusToText(a.StatusId, statusIdToName),
            Image = a.ImageUrl ?? string.Empty,
            People = a.People,
            Duration = a.Duration,
            Location = a.Location,
            CreateTime = a.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            SoldCount = soldLookup.GetValueOrDefault(a.ActivityId),
            VerifiedCount = verifiedLookup.GetValueOrDefault(a.ActivityId),
        }).ToList();

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
                    .Where(m => m.MaterialType == 0 || m.MaterialType == 2)
                    .Select(m => new CarouselMediaDto
                    {
                        Type = m.MaterialType == 2 ? "video" : "image",
                        Url = MediaHelper.NormalizeImageUrl(m.MaterialUrl),
                    })
                    .Take(5)
                    .ToList();
            }
        }

        return (records, total);
    }

    public async Task<ActivityManageDetailDto?> GetActivityDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.IsdeleteId == 0, cancellationToken);

        if (activity is null)
            return null;

        var (idToName, _) = await LoadTypeMappingAsync(cancellationToken);
        var (statusIdToName, _) = await LoadStatusMappingAsync(cancellationToken);

        var materials = await _dbContext.ActivityMaterials
            .Where(m => m.ActivityId == id)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        var carouselMedia = materials
            .Where(m => m.MaterialType == 0 || m.MaterialType == 2)
            .Select(m => new CarouselMediaDto
            {
                Type = m.MaterialType == 2 ? "video" : "image",
                Url = MediaHelper.NormalizeImageUrl(m.MaterialUrl),
            })
            .Take(5)
            .ToList();

        var specImages = materials
            .Where(m => m.MaterialType == 3)
            .Select(m => MediaHelper.NormalizeImageUrl(m.MaterialUrl))
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .ToList();

        return new ActivityManageDetailDto
        {
            Id = activity.ActivityId,
            Name = activity.Title,
            Type = idToName.TryGetValue(activity.TypeId, out var tn) ? tn : "其他",
            Price = activity.Price,
            StatusId = activity.StatusId,
            Status = MapStatusToText(activity.StatusId, statusIdToName),
            Image = MediaHelper.NormalizeImageUrl(activity.ImageUrl),
            ImageName = Path.GetFileName(MediaHelper.NormalizeImageUrl(activity.ImageUrl)),
            VideoUrl = MediaHelper.NormalizeImageUrl(activity.VideoUrl),
            Description = activity.Description,
            Location = activity.Location,
            People = activity.People,
            Content = activity.Content,
            Duration = activity.Duration,
            StartDate = activity.StartDate,            // 新增
            EndDate = activity.EndDate,                // 新增
            Stock = CalculateStock(activity),          // 新增
            CarouselMedia = carouselMedia,
            SpecImages = specImages,
            CreateTime = activity.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        };
    }

    public async Task<long> CreateActivityAsync(CreateActivityDto dto, CancellationToken cancellationToken = default)
    {
        var (_, nameToId) = await LoadTypeMappingAsync(cancellationToken);

        var activity = new ActivityEntity
        {
            Title = dto.Name,
            Price = dto.Price > 0 ? dto.Price : 0.01m,
            ImageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath),
            VideoUrl = MediaHelper.ProcessImageData(dto.VideoUrl, _env.WebRootPath),
            Description = dto.Description,
            Location = dto.Location,
            People = dto.People ?? 0,
            Content = dto.Content,
            Duration = dto.Duration,
            StatusId = dto.StatusId,
            TypeId = nameToId.TryGetValue(dto.Type, out var typeId) ? typeId : nameToId.Values.FirstOrDefault(),
            SortOrder = 999,
            StartDate = dto.StartDate ?? DateTime.Now,                // 改用 dto.StartDate
            EndDate = dto.EndDate ?? DateTime.Now.AddDays(30),        // 改用 dto.EndDate
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
                    MaterialType = m.Type == "video" ? 2 : 0,
                    MaterialUrl = MediaHelper.ProcessImageData(m.Url, _env.WebRootPath),
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow,
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(materials);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (dto.SpecImages?.Count > 0)
        {
            var specMaterials = dto.SpecImages
                .Select((url, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = 3,
                    MaterialUrl = MediaHelper.ProcessImageData(url, _env.WebRootPath),
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow,
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(specMaterials);
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

        var (_, nameToId) = await LoadTypeMappingAsync(cancellationToken);

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
        activity.TypeId = nameToId.TryGetValue(dto.Type, out var typeId) ? typeId : nameToId.Values.FirstOrDefault();
        if (dto.StartDate.HasValue)                        // 新增
            activity.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue)                          // 新增
            activity.EndDate = dto.EndDate.Value;

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
                    MaterialType = m.Type == "video" ? 2 : 0,
                    MaterialUrl = MediaHelper.ProcessImageData(m.Url, _env.WebRootPath),
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow,
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(materials);
        }

        if (dto.SpecImages?.Count > 0)
        {
            var specMaterials = dto.SpecImages
                .Select((url, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = 3,
                    MaterialUrl = MediaHelper.ProcessImageData(url, _env.WebRootPath),
                    SortOrder = idx,
                    CreatedAt = DateTime.UtcNow,
                })
                .ToList();

            _dbContext.ActivityMaterials.AddRange(specMaterials);
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

    // ========== 辅助方法 ==========

    /// <summary>从 activity_status 表加载状态映射（id ↔ name）</summary>
    private async Task<(Dictionary<int, string> IdToName, Dictionary<string, int> NameToId)> LoadStatusMappingAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _dbContext.ActivityStatuses.ToListAsync(cancellationToken);
        var idToName = new Dictionary<int, string>();
        var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in statuses)
        {
            idToName[s.ActivityStatusId] = s.StatusName;
            // 兼容 "已上架" 和 "上架" 两种写法
            var raw = s.StatusName;
            nameToId[raw] = s.ActivityStatusId;
            if (raw.StartsWith("已", StringComparison.Ordinal))
                nameToId[raw[1..]] = s.ActivityStatusId;
        }

        return (idToName, nameToId);
    }

    public async Task<int> MapStatusToIdAsync(string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(status))
            return 1;

        var normalized = status.Trim();
        var (_, nameToId) = await LoadStatusMappingAsync(cancellationToken);
        return nameToId.TryGetValue(normalized, out var id) ? id : 1;
    }

    private static string MapStatusToText(int statusId, Dictionary<int, string>? statusMap)
    {
        if (statusMap != null && statusMap.TryGetValue(statusId, out var name))
            return name;
        return statusId switch
        {
            1 => "已上架",
            3 => "已售空",
            _ => "已下架"
        };
    }

    /// <summary>从 activity_type 表加载类型映射（id ↔ name）</summary>
    private async Task<(Dictionary<int, string> IdToName, Dictionary<string, int> NameToId)> LoadTypeMappingAsync(CancellationToken cancellationToken = default)
    {
        var types = await _dbContext.Set<ActivityType>().ToListAsync(cancellationToken);
        var idToName = new Dictionary<int, string>();
        var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in types)
        {
            idToName[t.ActivityTypeId] = t.TypeName;
            nameToId[t.TypeName] = t.ActivityTypeId;
        }

        return (idToName, nameToId);
    }

    /// <summary>计算剩余名额（当前 = people，后续可扣减已报名数）</summary>
    private static int CalculateStock(ActivityEntity activity)
    {
        if (activity.People is null or <= 0)
            return 0;
        return activity.People.Value;
    }

    public async Task<(int TotalSoldCount, int TotalVerifiedCount)> GetActivityTotalStatsAsync(CancellationToken cancellationToken = default)
    {
        var conn = _dbContext.Database.GetDbConnection();
        var needClose = conn.State != System.Data.ConnectionState.Open;
        if (needClose)
            await conn.OpenAsync(cancellationToken);

        var totalSold = 0;
        var soldCmd = conn.CreateCommand();
        soldCmd.CommandText = "SELECT COALESCE(SUM(d.quantity),0) FROM activity_order_detail d JOIN activity_orders o ON d.activity_order_id = o.order_id WHERE o.order_status_id IN (2,3)";
        var soldResult = await soldCmd.ExecuteScalarAsync(cancellationToken);
        if (soldResult is not null && soldResult != DBNull.Value)
            totalSold = Convert.ToInt32(soldResult);

        var totalVerified = 0;
        var verifiedCmd = conn.CreateCommand();
        verifiedCmd.CommandText = "SELECT COUNT(*) FROM activity_verification_record";
        var verifiedResult = await verifiedCmd.ExecuteScalarAsync(cancellationToken);
        if (verifiedResult is not null && verifiedResult != DBNull.Value)
            totalVerified = Convert.ToInt32(verifiedResult);

        return (totalSold, totalVerified);
    }
}
