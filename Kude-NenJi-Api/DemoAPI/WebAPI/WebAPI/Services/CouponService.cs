using Microsoft.EntityFrameworkCore;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;
using WebAPI.Entities.Entities;

namespace WebAPI.Services;

/// <summary>
/// 券品管理服务 - 基于活动表实现
/// 字段映射：
/// - Title → 券品名称
/// - Price → 售价
/// - StartDate/EndDate → 有效期
/// - ImageUrl → 封面图
/// - StatusId → 上下架状态
/// - TypeId → 活动类型（关联到券品类型）
/// </summary>
public class CouponService : ICouponService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CouponService> _logger;

    public CouponService(AppDbContext dbContext, ILogger<CouponService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 获取券品列表
    /// </summary>
    public async Task<(List<CouponListItemDto> Records, int Total)> GetCouponListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Activities
            .AsNoTracking()
            .Include(a => a.ActivityMaterials)
            .Where(a => a.StatusId > 1);  // 排除已删除状态（StatusId=1表示正常）

        // 关键词搜索：按标题
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

        var records = new List<CouponListItemDto>();

        foreach (var activity in activities)
        {
            var couponItem = MapActivityToCouponListItem(activity);
            records.Add(couponItem);
        }

        return (records, total);
    }

    /// <summary>
    /// 获取券品详情
    /// </summary>
    public async Task<CouponDetailDto?> GetCouponDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .AsNoTracking()
            .Include(a => a.ActivityMaterials)
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.StatusId > 1, cancellationToken);

        if (activity is null)
            return null;

        var materials = activity.ActivityMaterials
            .OrderBy(m => m.SortOrder)
            .ToList();

        return MapActivityToCouponDetail(activity, materials);
    }

    /// <summary>
    /// 新增券品
    /// </summary>
    public async Task<long> CreateCouponAsync(CreateCouponDto dto, CancellationToken cancellationToken = default)
    {
        // 计算活动日期（基于有效期）
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
            StatusId = 2,  // 2=已发布（上架）
            TypeId = GetTypeIdFromType(dto.Type),  // 根据类型获取 TypeId
            SortOrder = 999,
            ActivityMaterials = []
        };

        _dbContext.Activities.Add(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"券品新增成功 - ActivityId: {activity.ActivityId}, Title: {activity.Title}");

        // 保存轮播图素材
        if (dto.CarouselMedia?.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, idx) => new ActivityMaterial
                {
                    ActivityId = activity.ActivityId,
                    MaterialType = m.Url.EndsWith(".mp4") ? "2" : "0",  // 0=图片，2=视频
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

    /// <summary>
    /// 编辑券品
    /// </summary>
    public async Task<bool> UpdateCouponAsync(long id, UpdateCouponDto dto, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .Include(a => a.ActivityMaterials)
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.StatusId > 1, cancellationToken);

        if (activity is null)
            return false;

        // 更新基础字段
        activity.Title = dto.Name;
        activity.Price = dto.Price;
        activity.ImageUrl = dto.Image ?? string.Empty;
        activity.TypeId = GetTypeIdFromType(dto.Type);

        // 重新计算有效期
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

        // 删除旧素材
        var oldMaterials = activity.ActivityMaterials.ToList();
        foreach (var material in oldMaterials)
        {
            _dbContext.ActivityMaterials.Remove(material);
        }

        // 添加新素材
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

    /// <summary>
    /// 删除券品
    /// </summary>
    public async Task<bool> DeleteCouponAsync(long id, CancellationToken cancellationToken = default)
    {
        var activity = await _dbContext.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.StatusId > 1, cancellationToken);

        if (activity is null)
            return false;

        // 标记为已删除（StatusId = 1）
        activity.StatusId = 1;
        _dbContext.Activities.Update(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"券品删除成功 - ActivityId: {id}");

        return true;
    }

    /// <summary>
    /// 批量删除券品
    /// </summary>
    public async Task<bool> DeleteCouponBatchAsync(long[] ids, CancellationToken cancellationToken = default)
    {
        var activities = await _dbContext.Activities
            .Where(a => ids.Contains(a.ActivityId) && a.StatusId > 1)
            .ToListAsync(cancellationToken);

        if (activities.Count == 0)
            return false;

        foreach (var activity in activities)
        {
            activity.StatusId = 1;  // 标记为已删除
        }

        _dbContext.Activities.UpdateRange(activities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"批量删除券品成功 - 数量: {activities.Count}");

        return true;
    }

    /// <summary>
    /// 映射：活动 -> 券品列表项
    /// </summary>
    private CouponListItemDto MapActivityToCouponListItem(ActivityEntity activity)
    {
        var validity = CalculateValidityText(activity.StartDate, activity.EndDate);
        var (period, unit) = ParseValidityText(validity);
        var couponType = GetCouponTypeFromTypeId(activity.TypeId);

        return new CouponListItemDto
        {
            Id = activity.ActivityId,
            Name = activity.Title,
            Type = couponType,
            Price = activity.Price,
            Stock = 100,  // 无库存数据，默认100
            LimitPerOrder = 4,
            ValidityPeriod = period,
            ValidityUnit = unit,
            Validity = validity,
            RefundRule = "需人工审核退款",
            UsageRules = "详见券品详情",
            Image = activity.ImageUrl,
            CarouselMedia = [],
            SoldCount = 0,
            VerifiedCount = 0,
            CreateTime = activity.ActivityId.ToString().Length >= 14 
                ? DateTime.Now.ToString("yyyy-MM-dd HH:mm") 
                : DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };
    }

    /// <summary>
    /// 映射：活动 -> 券品详情
    /// </summary>
    private CouponDetailDto MapActivityToCouponDetail(
        ActivityEntity activity,
        List<ActivityMaterial> materials)
    {
        var validity = CalculateValidityText(activity.StartDate, activity.EndDate);
        var (period, unit) = ParseValidityText(validity);
        var couponType = GetCouponTypeFromTypeId(activity.TypeId);

        var carouselMedia = materials
            .Where(m => m.MaterialType == "0" || m.MaterialType == "2")  // 0=图片，2=视频
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
            Stock = 100,
            LimitPerOrder = 4,
            ValidityPeriod = period,
            ValidityUnit = unit,
            Validity = validity,
            RefundRule = "需人工审核退款",
            UsageRules = "详见券品详情",
            Image = activity.ImageUrl,
            ImageName = Path.GetFileName(activity.ImageUrl),
            CarouselMedia = carouselMedia,
            SoldCount = 0,
            VerifiedCount = 0,
            CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };
    }

    /// <summary>
    /// 计算有效期文本
    /// </summary>
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

    /// <summary>
    /// 解析有效期文本
    /// </summary>
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

    /// <summary>
    /// 根据券品类型获取 TypeId
    /// </summary>
    private int GetTypeIdFromType(string type)
    {
        return type switch
        {
            "采摘券" => 1,
            "研学活动券" => 2,
            _ => 3  // 其他
        };
    }

    /// <summary>
    /// 根据 TypeId 获取券品类型
    /// </summary>
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