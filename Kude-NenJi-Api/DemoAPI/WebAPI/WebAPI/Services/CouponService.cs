using Microsoft.EntityFrameworkCore;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;

namespace WebAPI.Services;

/// <summary>
/// 券品管理服务
/// </summary>
public class CouponService : ICouponService
{
    private readonly AppDbContext _dbContext;

    public CouponService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取券品列表
    /// </summary>
    //public async Task<(List<CouponListItemDto> Records, int Total)> GetCouponListAsync(
    //    int pageNum,
    //    int pageSize,
    //    string? keyword,
    //    CancellationToken cancellationToken = default)
    //{
    //    var query = _dbContext.Coupons
    //        .Include(c => c.CouponStatistic)
    //        .AsNoTracking();

    //    // 模糊搜索: 编码、名称、类型
    //    if (!string.IsNullOrWhiteSpace(keyword))
    //    {
    //        var keywordTrimmed = keyword.Trim();
    //        query = query.Where(c =>
    //            c.CouponCode.Contains(keywordTrimmed) ||
    //            c.Name.Contains(keywordTrimmed) ||
    //            c.Type.Contains(keywordTrimmed));
    //    }

    //    var total = await query.CountAsync(cancellationToken);

    //    var records = await query
    //        .OrderByDescending(c => c.CreatedAt)
    //        .Skip((pageNum - 1) * pageSize)
    //        .Take(pageSize)
    //        .Select(c => new CouponListItemDto
    //        {
    //            Id = c.CouponCode,
    //            Name = c.Name,
    //            Type = c.Type,
    //            Price = c.Price,
    //            Stock = c.Stock,
    //            LimitPerOrder = c.LimitPerOrder,
    //            ValidityPeriod = c.ValidityPeriod,
    //            ValidityUnit = c.ValidityUnit,
    //            Validity = $"{c.ValidityPeriod}{c.ValidityUnit}",
    //            RefundRule = c.RefundRule,
    //            UsageRules = c.UsageRules,
    //            Image = c.ImageUrl ?? string.Empty,
    //            CarouselMedia = [],
    //            SoldCount = c.CouponStatistic?.SoldCount ?? 0,
    //            VerifiedCount = c.CouponStatistic?.VerifiedCount ?? 0,
    //            CreateTime = c.CreatedAt.ToString("yyyy-MM-dd HH:mm")
    //        })
    //        .ToListAsync(cancellationToken);

    //    return (records, total);
    //}

    /// <summary>
    /// 获取券品详情
    /// </summary>
    //public async Task<CouponDetailDto?> GetCouponDetailAsync(string couponCode, CancellationToken cancellationToken = default)
    //{
    //    var coupon = await _dbContext.Coupons
    //        .Include(c => c.CouponMaterials)
    //        .Include(c => c.CouponStatistic)
    //        .AsNoTracking()
    //        .FirstOrDefaultAsync(c => c.CouponCode == couponCode, cancellationToken);

    //    if (coupon is null)
    //    {
    //        return null;
    //    }

    //    // 分类素材
    //    var carouselMedia = new List<CarouselMediaDto>();
    //    var materials = coupon.CouponMaterials.OrderBy(m => m.SortOrder).ToList();

    //    foreach (var material in materials)
    //    {
    //        if (material.MaterialType == "carousel")
    //        {
    //            carouselMedia.Add(new CarouselMediaDto
    //            {
    //                Type = IsVideoUrl(material.MaterialUrl) ? "video" : "image",
    //                Url = material.MaterialUrl,
    //                Thumb = material.ThumbUrl
    //            });
    //        }
    //    }

    //    return new CouponDetailDto
    //    {
    //        Id = coupon.CouponCode,
    //        Name = coupon.Name,
    //        Type = coupon.Type,
    //        Price = coupon.Price,
    //        Stock = coupon.Stock,
    //        LimitPerOrder = coupon.LimitPerOrder,
    //        ValidityPeriod = coupon.ValidityPeriod,
    //        ValidityUnit = coupon.ValidityUnit,
    //        Validity = $"{coupon.ValidityPeriod}{coupon.ValidityUnit}",
    //        RefundRule = coupon.RefundRule,
    //        UsageRules = coupon.UsageRules,
    //        Image = coupon.ImageUrl ?? string.Empty,
    //        CarouselMedia = carouselMedia.Take(5).ToList(),
    //        SoldCount = coupon.CouponStatistic?.SoldCount ?? 0,
    //        VerifiedCount = coupon.CouponStatistic?.VerifiedCount ?? 0,
    //        CreateTime = coupon.CreatedAt.ToString("yyyy-MM-dd HH:mm")
    //    };
    //}

    /// <summary>
    /// 新增券品
    /// </summary>
    public async Task<string> CreateCouponAsync(CreateCouponDto dto, CancellationToken cancellationToken = default)
    {
        // 生成券品编码: Q + yyyyMMddHHmmss + 两位序号
        var couponCode = GenerateCouponCode();

        var coupon = new Coupon
        {
            CouponCode = couponCode,
            Name = dto.Name,
            Type = dto.Type,
            Price = dto.Price,
            Stock = dto.Stock,
            LimitPerOrder = dto.LimitPerOrder,
            ValidityPeriod = dto.ValidityPeriod,
            ValidityUnit = dto.ValidityUnit,
            RefundRule = dto.RefundRule,
            UsageRules = dto.UsageRules,
            ImageUrl = dto.Image,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Coupons.Add(coupon);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 创建统计记录
        var statistic = new CouponStatistic
        {
            CouponId = coupon.CouponId,
            SoldCount = 0,
            VerifiedCount = 0
        };
        _dbContext.CouponStatistics.Add(statistic);

        // 保存轮播图
        if (dto.CarouselMedia.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, index) => new CouponMaterial
                {
                    CouponId = coupon.CouponId,
                    MaterialType = "carousel",
                    MaterialUrl = m.Url,
                    ThumbUrl = m.Thumb,
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _dbContext.CouponMaterials.AddRange(materials);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return couponCode;
    }

    /// <summary>
    /// 编辑券品
    /// </summary>
    public async Task<bool> UpdateCouponAsync(UpdateCouponDto dto, CancellationToken cancellationToken = default)
    {
        var coupon = await _dbContext.Coupons
            .FirstOrDefaultAsync(c => c.CouponCode == dto.Id, cancellationToken);

        if (coupon is null)
        {
            return false;
        }

        coupon.Name = dto.Name;
        coupon.Type = dto.Type;
        coupon.Price = dto.Price;
        coupon.Stock = dto.Stock;
        coupon.LimitPerOrder = dto.LimitPerOrder;
        coupon.ValidityPeriod = dto.ValidityPeriod;
        coupon.ValidityUnit = dto.ValidityUnit;
        coupon.RefundRule = dto.RefundRule;
        coupon.UsageRules = dto.UsageRules;
        coupon.ImageUrl = dto.Image;
        coupon.UpdatedAt = DateTime.UtcNow;

        // 删除旧的素材
        var oldMaterials = await _dbContext.CouponMaterials
            .Where(m => m.CouponId == coupon.CouponId)
            .ToListAsync(cancellationToken);

        _dbContext.CouponMaterials.RemoveRange(oldMaterials);

        // 添加新的素材
        if (dto.CarouselMedia.Count > 0)
        {
            var materials = dto.CarouselMedia
                .Select((m, index) => new CouponMaterial
                {
                    CouponId = coupon.CouponId,
                    MaterialType = "carousel",
                    MaterialUrl = m.Url,
                    ThumbUrl = m.Thumb,
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _dbContext.CouponMaterials.AddRange(materials);
        }

        _dbContext.Coupons.Update(coupon);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// 删除券品
    /// </summary>
    public async Task<bool> DeleteCouponAsync(string couponCode, CancellationToken cancellationToken = default)
    {
        var coupon = await _dbContext.Coupons
            .FirstOrDefaultAsync(c => c.CouponCode == couponCode, cancellationToken);

        if (coupon is null)
        {
            return false;
        }

        // 删除关联的素材
        var materials = await _dbContext.CouponMaterials
            .Where(m => m.CouponId == coupon.CouponId)
            .ToListAsync(cancellationToken);

        _dbContext.CouponMaterials.RemoveRange(materials);

        // 删除统计数据
        var statistic = await _dbContext.CouponStatistics
            .FirstOrDefaultAsync(s => s.CouponId == coupon.CouponId, cancellationToken);

        if (statistic is not null)
        {
            _dbContext.CouponStatistics.Remove(statistic);
        }

        _dbContext.Coupons.Remove(coupon);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// 批量删除券品
    /// </summary>
    public async Task<bool> DeleteCouponBatchAsync(string[] couponCodes, CancellationToken cancellationToken = default)
    {
        var coupons = await _dbContext.Coupons
            .Where(c => couponCodes.Contains(c.CouponCode))
            .ToListAsync(cancellationToken);

        if (coupons.Count == 0)
        {
            return false;
        }

        var couponIds = coupons.Select(c => c.CouponId).ToList();

        // 删除关联的素材
        var materials = await _dbContext.CouponMaterials
            .Where(m => couponIds.Contains(m.CouponId))
            .ToListAsync(cancellationToken);

        _dbContext.CouponMaterials.RemoveRange(materials);

        // 删除统计数据
        var statistics = await _dbContext.CouponStatistics
            .Where(s => couponIds.Contains(s.CouponId))
            .ToListAsync(cancellationToken);

        _dbContext.CouponStatistics.RemoveRange(statistics);

        _dbContext.Coupons.RemoveRange(coupons);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// 生成券品编码
    /// </summary>
    private string GenerateCouponCode()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // 获取今天最后一个序号
        var today = DateTime.Now.Date;
        var todayEnd = today.AddDays(1);

        var lastCoupon = _dbContext.Coupons
            .AsNoTracking()
            .Where(c => c.CreatedAt >= today && c.CreatedAt < todayEnd)
            .OrderByDescending(c => c.CouponId)
            .FirstOrDefault();

        var sequenceNumber = (lastCoupon?.CouponId ?? 0) % 100 + 1;
        var sequenceStr = sequenceNumber.ToString("D2");

        return $"Q{timestamp}{sequenceStr}";
    }

    /// <summary>
    /// 判断URL是否为视频
    /// </summary>
    private static bool IsVideoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
        var extension = Path.GetExtension(url).ToLowerInvariant();
        return videoExtensions.Contains(extension);
    }
}