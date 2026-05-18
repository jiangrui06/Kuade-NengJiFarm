using Microsoft.EntityFrameworkCore;
using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Services;

public class PointsService : IPointsService
{
    private readonly AppDbContext _db;
    private static readonly Random _random = new();

    public PointsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PointsSummaryDto?> GetSummaryAsync(int userId, CancellationToken ct = default)
    {
        var points = await _db.UserPoints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (points is null)
        {
            return new PointsSummaryDto
            {
                TotalPoints = 0,
                EarnedPoints = 0,
                SpentPoints = 0,
                TodayEarned = 0
            };
        }

        var todayStart = DateTime.Today;
        var todayEarned = await _db.PointsRecords
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == "earn" && x.CreateTime >= todayStart)
            .SumAsync(x => x.Points, ct);

        return new PointsSummaryDto
        {
            TotalPoints = points.TotalPoints,
            EarnedPoints = points.EarnedPoints,
            SpentPoints = points.SpentPoints,
            TodayEarned = todayEarned
        };
    }

    public async Task<PointsRecordListDto> GetRecordsAsync(int userId, string? type, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.PointsRecords.AsNoTracking().Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim().ToLowerInvariant();
            if (t == "earn" || t == "spend")
                query = query.Where(x => x.Type == t);
        }

        var total = await query.CountAsync(ct);
        var records = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PointsRecordListDto
        {
            List = records.Select(r => new PointsRecordItemDto
            {
                Id = r.Id,
                Type = r.Type,
                Desc = r.Description,
                Points = r.Points,
                Balance = r.Balance,
                Time = r.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task EarnPointsAsync(int userId, string orderNo, decimal amount, CancellationToken ct = default)
    {
        var rule = await _db.PointsRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (rule is null)
        {
            // 没有配置规则时默认 10元 = 1积分
            var fallback = (int)(amount / 10);
            if (fallback <= 0) return;
            await EarnCoreAsync(userId, orderNo, fallback, ct);
            return;
        }

        var points = (int)(amount / rule.UnitAmount * rule.UnitPoints);
        if (points <= 0) return;
        await EarnCoreAsync(userId, orderNo, points, ct);
    }

    private async Task EarnCoreAsync(int userId, string orderNo, int points, CancellationToken ct = default)
    {

        var userPoints = await _db.UserPoints
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (userPoints is null)
        {
            userPoints = new UserPoints
            {
                UserId = userId,
                TotalPoints = points,
                EarnedPoints = points,
                SpentPoints = 0,
                UpdatedAt = DateTime.Now
            };
            _db.UserPoints.Add(userPoints);
        }
        else
        {
            userPoints.TotalPoints += points;
            userPoints.EarnedPoints += points;
            userPoints.UpdatedAt = DateTime.Now;
        }

        _db.PointsRecords.Add(new PointsRecord
        {
            UserId = userId,
            Type = "earn",
            Points = points,
            Balance = userPoints.TotalPoints,
            Description = $"消费获得积分",
            OrderNo = orderNo,
            CreateTime = DateTime.Now
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<PointsExchangeResultDto> ExchangeAsync(int userId, int commodityId, int quantity, CancellationToken ct = default)
    {
        quantity = Math.Max(1, quantity);

        var commodity = await _db.Commodities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CommodityId == commodityId && (x.ProductStatus ?? 0) == 1, ct);

        if (commodity is null)
            throw new BusinessException("商品不存在", 404);

        if (commodity.PointsPrice is null or <= 0)
            throw new BusinessException("该商品不支持积分兑换", 400);

        var totalPoints = commodity.PointsPrice.Value * quantity;

        var userPoints = await _db.UserPoints
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (userPoints is null || userPoints.TotalPoints < totalPoints)
            throw new BusinessException("积分不足", 409);

        // 扣库存
        var inStock = commodity.InStock ?? 0;
        if (inStock < quantity)
            throw new BusinessException("商品库存不足", 409);

        var orderNo = $"EXC{DateTime.Now:yyyyMMddHHmmssfff}{_random.Next(100, 999)}";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // 扣积分
        userPoints.TotalPoints -= totalPoints;
        userPoints.SpentPoints += totalPoints;
        userPoints.UpdatedAt = DateTime.Now;

        // 扣库存
        commodity.InStock = inStock - quantity;

        // 流水
        _db.PointsRecords.Add(new PointsRecord
        {
            UserId = userId,
            Type = "spend",
            Points = totalPoints,
            Balance = userPoints.TotalPoints,
            Description = $"兑换{commodity.ProductName}",
            OrderNo = orderNo,
            CreateTime = DateTime.Now
        });

        // 兑换记录
        _db.PointsExchanges.Add(new PointsExchange
        {
            UserId = userId,
            CommodityId = commodityId,
            Quantity = quantity,
            PointsSpent = totalPoints,
            OrderNo = orderNo,
            Status = "completed",
            CreateTime = DateTime.Now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PointsExchangeResultDto
        {
            ExchangeId = 0,
            OrderNo = orderNo,
            PointsSpent = totalPoints,
            PointsRemaining = userPoints.TotalPoints,
            Status = "completed"
        };
    }

    public async Task<PointsExchangeListDto> GetExchangeRecordsAsync(int userId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.PointsExchanges
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var total = await query.CountAsync(ct);
        var records = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var commodityIds = records.Select(x => x.CommodityId).Distinct().ToList();
        var commodities = await _db.Commodities.AsNoTracking()
            .Where(x => commodityIds.Contains(x.CommodityId))
            .Select(x => new { x.CommodityId, x.ProductName, x.ImageUrl })
            .ToListAsync(ct);
        var commodityMap = commodities.ToDictionary(x => x.CommodityId);

        return new PointsExchangeListDto
        {
            List = records.Select(r =>
            {
                var name = commodityMap.TryGetValue(r.CommodityId, out var c)
                    ? c.ProductName : "已下架";
                var image = commodityMap.TryGetValue(r.CommodityId, out var ci)
                    ? (ci.ImageUrl ?? string.Empty) : string.Empty;
                return new PointsExchangeItemDto
                {
                    Id = r.Id,
                    Name = name,
                    Image = image,
                    Points = r.PointsSpent,
                    Time = r.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = r.Status == "completed" ? "已完成" : "已取消",
                    OrderNo = r.OrderNo
                };
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
