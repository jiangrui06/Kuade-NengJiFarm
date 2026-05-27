using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Services;

public class PointsService : IPointsService
{
    private readonly AppDbContext _db;
    private static readonly Random _random = new();
    private static readonly Dictionary<int, string> _statusCache = new()
    {
        { 1, "待核销" },
        { 2, "已核销" },
        { 3, "已取消" }
    };

    public PointsService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 从 points_commodity_order_status 表加载状态映射（DB驱动，不硬编码）
    /// 兜底用上面缓存的默认值
    /// </summary>
    private async Task<Dictionary<int, string>> LoadStatusMapAsync(CancellationToken ct = default)
    {
        try
        {
            var statuses = await _db.PointsCommodityOrderStatuses
                .AsNoTracking()
                .ToListAsync(ct);

            if (statuses.Count > 0)
            {
                return statuses.ToDictionary(s => s.Id, s => s.StatusName);
            }
        }
        catch
        {
            // 表或数据不存在，使用默认映射
        }

        return new Dictionary<int, string>(_statusCache);
    }

    /// <summary>
    /// 从 points_commodity_status 表加载商品状态映射
    /// </summary>
    private async Task<HashSet<int>> LoadActiveCommodityStatusIdsAsync(CancellationToken ct = default)
    {
        try
        {
            // 取所有 status_name 为 "active" 或 "上架" 的 status_id
            var active = await _db.PointsCommodityStatuses
                .AsNoTracking()
                .Where(s => s.StatusName == "active" || s.StatusName == "上架")
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (active.Count > 0)
                return active.ToHashSet();
        }
        catch
        {
            // 表或数据不存在
        }

        // 默认 status_id = 1 视为上架
        return [1];
    }

    public async Task<PointsSummaryDto?> GetSummaryAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (user is null)
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

        var totalEarned = await _db.PointsRecords
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == "earn")
            .SumAsync(x => (int?)x.Points, ct) ?? 0;

        var totalSpent = await _db.PointsRecords
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == "spend")
            .SumAsync(x => (int?)x.Points, ct) ?? 0;

        return new PointsSummaryDto
        {
            TotalPoints = (int)user.Points,
            EarnedPoints = totalEarned,
            SpentPoints = totalSpent,
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

        if (rule is null) return;

        var points = (int)(amount / rule.UnitAmount * rule.UnitPoints);
        if (points <= 0) return;
        await EarnCoreAsync(userId, orderNo, points, ct);
    }

    private async Task EarnCoreAsync(int userId, string orderNo, int points, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (user is null)
            throw new BusinessException("用户不存在", 404);

        user.Points += points;

        _db.PointsRecords.Add(new PointsRecord
        {
            UserId = userId,
            Type = "earn",
            Points = points,
            Description = "消费获得积分",
            OrderNo = orderNo,
            CreateTime = DateTime.Now
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<PointsExchangeResultDto> ExchangeAsync(int userId, int commodityId, int quantity, CancellationToken ct = default)
    {
        quantity = Math.Max(1, quantity);

        // 从 points_commodity 表查积分商品（根据数据库设计，不查 commodity 表）
        var commodity = await _db.PointsCommodities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == commodityId && x.IsDelete == 0, ct);

        if (commodity is null)
            throw new BusinessException("商品不存在", 404);

        if (commodity.PointsPrice <= 0)
            throw new BusinessException("该商品不支持积分兑换", 400);

        var totalPoints = commodity.PointsPrice * quantity;

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (user is null)
            throw new BusinessException("用户不存在", 404);

        if (user.Points < totalPoints)
            throw new BusinessException("积分不足", 409);

        // 扣库存
        if (commodity.Stock < quantity)
            throw new BusinessException("商品库存不足", 409);

        var now = DateTime.Now;
        var orderNo = $"EXC{now:yyyyMMddHHmmssfff}{_random.Next(100, 999)}";

        // 生成 QR 码文件
        var qrcodeUrl = await SaveQrCodeFileAsync(orderNo);

        // 默认 status_id = 1 (pending)
        var statusId = await GetPendingStatusIdAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // 扣积分（user 表）
        user.Points -= totalPoints;

        // 扣库存（points_commodity 表）
        commodity.Stock -= quantity;

        // 积分流水
        _db.PointsRecords.Add(new PointsRecord
        {
            UserId = userId,
            Type = "spend",
            Points = totalPoints,
            Description = $"兑换{commodity.Name}",
            OrderNo = orderNo,
            CreateTime = now
        });

        // 兑换记录（points_commodity_order 表）
        var exchange = new PointsExchange
        {
            UserId = userId,
            CommodityId = commodityId,
            Quantity = quantity,
            PointsSpent = totalPoints,
            OrderNo = orderNo,
            StatusId = statusId,
            VerifyCode = orderNo,  // 核销码 = 订单号
            CreateTime = now
        };
        _db.PointsExchanges.Add(exchange);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var statusMap = await LoadStatusMapAsync(ct);
        var statusName = statusMap.GetValueOrDefault(statusId, "pending");

        return new PointsExchangeResultDto
        {
            ExchangeId = exchange.Id,
            OrderNo = orderNo,
            PointsSpent = totalPoints,
            PointsRemaining = (int)user.Points,
            QrcodeUrl = qrcodeUrl,
            Status = statusName,
            StatusText = statusName,
            Name = commodity.Name,
            Image = MediaUrlHelper.Normalize(commodity.ImageUrl),
            Time = now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    public async Task<PointsCancelResultDto> CancelExchangeAsync(string orderNo, int userId, CancellationToken ct = default)
    {
        var exchange = await _db.PointsExchanges
            .FirstOrDefaultAsync(x => x.OrderNo == orderNo && x.UserId == userId, ct);

        if (exchange is null)
            throw new BusinessException("兑换记录不存在", 404);

        // 动态获取状态映射
        var statusMap = await LoadStatusMapAsync(ct);
        var pendingName = statusMap.GetValueOrDefault(1, "待核销");
        var cancelledName = statusMap.GetValueOrDefault(3, "已取消");

        // 校验状态：仅待核销可取消
        if (exchange.StatusId != 1)
        {
            var currentStatusName = statusMap.GetValueOrDefault(exchange.StatusId, "未知");
            throw new BusinessException($"该兑换已{currentStatusName}，无法取消", 400);
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (user is null)
            throw new BusinessException("用户不存在", 404);

        // 查询商品名（用于积分流水描述）
        var commodity = await _db.PointsCommodities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == exchange.CommodityId && x.IsDelete == 0, ct);
        var commodityName = commodity?.Name ?? "积分商品";

        // 动态获取已取消状态的 ID
        var cancelledStatusId = await GetStatusIdByNameAsync("已取消", ct);

        var now = DateTime.Now;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // 1. 更新兑换状态为已取消
        exchange.StatusId = cancelledStatusId;

        // 2. 退回积分
        user.Points += exchange.PointsSpent;

        // 3. 记录积分流水（退回积分标记为 earn 类型，前端按绿色"+"号展示）
        _db.PointsRecords.Add(new PointsRecord
        {
            UserId = userId,
            Type = "earn",
            Points = exchange.PointsSpent,
            Description = $"取消兑换{commodityName}",
            OrderNo = orderNo,
            CreateTime = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PointsCancelResultDto
        {
            OrderNo = orderNo,
            PointsReturned = exchange.PointsSpent,
            PointsRemaining = (int)user.Points
        };
    }

    public async Task<PointsExchangeDetailDto?> GetExchangeDetailAsync(string orderNo, int userId, CancellationToken ct = default)
    {
        var exchange = await _db.PointsExchanges
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderNo == orderNo && x.UserId == userId, ct);

        if (exchange is null)
            return null;

        // 生成 QR 码文件
        var qrcodeUrl = await SaveQrCodeFileAsync(exchange.OrderNo);

        var commodity = await _db.PointsCommodities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == exchange.CommodityId && x.IsDelete == 0, ct);

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        var statusMap = await LoadStatusMapAsync(ct);
        var statusName = statusMap.GetValueOrDefault(exchange.StatusId, "pending");

        return new PointsExchangeDetailDto
        {
            OrderNo = exchange.OrderNo,
            Name = commodity?.Name ?? "已下架",
            Image = MediaUrlHelper.Normalize(commodity?.ImageUrl),
            PointsSpent = exchange.PointsSpent,
            PointsRemaining = (int)(user?.Points ?? 0),
            QrcodeUrl = qrcodeUrl,
            Status = statusName,
            StatusText = statusName,
            Time = exchange.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            VerifyTime = exchange.VerifyTime?.ToString("yyyy-MM-dd HH:mm:ss")
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
        var commodities = await _db.PointsCommodities.AsNoTracking()
            .Where(x => commodityIds.Contains(x.Id) && x.IsDelete == 0)
            .Select(x => new { x.Id, x.Name, x.ImageUrl })
            .ToListAsync(ct);
        var commodityMap = commodities.ToDictionary(x => x.Id);

        var statusMap = await LoadStatusMapAsync(ct);

        return new PointsExchangeListDto
        {
            List = records.Select(r =>
            {
                var name = commodityMap.TryGetValue(r.CommodityId, out var c)
                    ? c.Name : "已下架";
                var image = commodityMap.TryGetValue(r.CommodityId, out var ci)
                    ? (ci.ImageUrl ?? string.Empty) : string.Empty;
                var statusName = statusMap.GetValueOrDefault(r.StatusId, "pending");
                return new PointsExchangeItemDto
                {
                    Id = r.Id,
                    Name = name,
                    Image = MediaUrlHelper.Normalize(image),
                    Points = r.PointsSpent,
                    Time = r.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = statusName,
                    OrderNo = r.OrderNo
                };
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 获取 points_commodity_order_status 表中"待核销"对应的 ID
    /// </summary>
    public async Task<int> GetPendingStatusIdAsync(CancellationToken ct = default)
    {
        try
        {
            var pending = await _db.PointsCommodityOrderStatuses
                .AsNoTracking()
                .Where(s => s.StatusName == "待核销")
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);

            if (pending.HasValue)
                return pending.Value;
        }
        catch { }

        return 1; // 默认待核销 = 1
    }

    /// <summary>
    /// 按状态名称查询状态 ID（数据库驱动，默认兜底）
    /// </summary>
    private async Task<int> GetStatusIdByNameAsync(string statusName, CancellationToken ct = default)
    {
        try
        {
            var id = await _db.PointsCommodityOrderStatuses
                .AsNoTracking()
                .Where(s => s.StatusName == statusName)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);

            if (id.HasValue)
                return id.Value;
        }
        catch { }

        // 默认映射：待核销=1, 已核销=2, 已取消=3
        return statusName switch
        {
            "已核销" => 2,
            "已取消" => 3,
            _ => 1
        };
    }

    /// <summary>
    /// 获取状态映射：status_id → status_name
    /// </summary>
    public async Task<string> GetStatusNameAsync(int statusId, CancellationToken ct = default)
    {
        try
        {
            var name = await _db.PointsCommodityOrderStatuses
                .AsNoTracking()
                .Where(s => s.Id == statusId)
                .Select(s => s.StatusName)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch { }

        return _statusCache.GetValueOrDefault(statusId, "unknown");
    }

    /// <summary>
    /// 获取商品名称
    /// </summary>
    public async Task<string> GetCommodityNameAsync(int commodityId, CancellationToken ct = default)
    {
        try
        {
            var name = await _db.PointsCommodities
                .AsNoTracking()
                .Where(x => x.Id == commodityId && x.IsDelete == 0)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch { }

        return "积分商品";
    }

    /// <summary>
    /// 生成 QR 码并保存为文件，返回 https://api.nengjifarm.com 完整 URL
    /// </summary>
    private async Task<string> SaveQrCodeFileAsync(string orderNo)
    {
        var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var qrDir = Path.Combine(wwwroot, "images", "qrcode");
        Directory.CreateDirectory(qrDir);

        var fileName = $"points_{orderNo}.png";
        var filePath = Path.Combine(qrDir, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(orderNo, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(20);
            await System.IO.File.WriteAllBytesAsync(filePath, bytes);
        }

        return $"https://api.nengjifarm.com/api/file/image/{fileName}";
    }
}
