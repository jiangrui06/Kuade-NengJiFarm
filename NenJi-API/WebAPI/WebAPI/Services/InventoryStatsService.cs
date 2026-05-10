using Microsoft.EntityFrameworkCore;

using WebAPI.Data;

namespace WebAPI.Services;

public sealed class InventoryStatsService : IInventoryStatsService
{
    private const int DefaultAcreCapacity = 50;
    private readonly AppDbContext _dbContext;

    public InventoryStatsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Dictionary<int, CommodityInventoryStats>> GetCommodityStatsAsync(
        IEnumerable<int> commodityIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(commodityIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var commodities = await _dbContext.Commodities
            .AsNoTracking()
            .Where(x => ids.Contains(x.CommodityId))
            .Select(x => new
            {
                x.CommodityId,
                BaseSold = x.Quantity ?? 0,
                Stock = x.InStock ?? 0
            })
            .ToListAsync(cancellationToken);

        var paidSales = await (
            from detail in _dbContext.CommodityOrderDetails.AsNoTracking()
            join order in _dbContext.CommodityOrders.AsNoTracking() on detail.OrderId equals order.OrderId
            where ids.Contains(detail.CommodityId)
                  && order.OrderStatusId != 4 && order.OrderStatusId != 5
                  && order.OrderStatusId != 6 && order.OrderStatusId != 7
            group detail by detail.CommodityId
            into groupByCommodity
            select new
            {
                CommodityId = groupByCommodity.Key,
                Sold = groupByCommodity.Sum(x => x.Quantity)
            }
        ).ToDictionaryAsync(x => x.CommodityId, x => x.Sold, cancellationToken);

        return commodities.ToDictionary(
            x => x.CommodityId,
            x => new CommodityInventoryStats
            {
                Sold = Math.Max(0, x.BaseSold) + paidSales.GetValueOrDefault(x.CommodityId),
                Stock = Math.Max(0, x.Stock)
            });
    }

    public async Task<Dictionary<int, DishInventoryStats>> GetDishStatsAsync(
        IEnumerable<int> dishIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(dishIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var dishes = await _dbContext.Dishes
            .AsNoTracking()
            .Where(x => ids.Contains(x.DishId))
            .Select(x => new
            {
                x.DishId,
                BaseSold = x.DishSold,
                BaseRemaining = x.DishRemainingQuantity
            })
            .ToListAsync(cancellationToken);

        var paidSales = await (
            from detail in _dbContext.DishOrderDetails.AsNoTracking()
            join order in _dbContext.DishOrders.AsNoTracking() on detail.DishOrderId equals order.OrderId
            where ids.Contains(detail.DishId)
                  && order.OrderStatusId != 4
            group detail by detail.DishId
            into groupByDish
            select new
            {
                DishId = groupByDish.Key,
                Sold = groupByDish.Sum(x => x.Quantity)
            }
        ).ToDictionaryAsync(x => x.DishId, x => x.Sold, cancellationToken);

        return dishes.ToDictionary(
            x => x.DishId,
            x =>
            {
                var incrementalSold = paidSales.GetValueOrDefault(x.DishId);
                return new DishInventoryStats
                {
                    Sold = Math.Max(0, x.BaseSold) + incrementalSold,
                    Stock = Math.Max(0, x.BaseRemaining - incrementalSold)
                };
            });
    }

    public async Task<Dictionary<int, ActivityInventoryStats>> GetActivityStatsAsync(
        IEnumerable<int> activityIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(activityIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var activities = await _dbContext.Activities
            .AsNoTracking()
            .Where(x => ids.Contains((int)x.ActivityId))
            .Select(x => new
            {
                ActivityId = (int)x.ActivityId,
                Capacity = x.People
            })
            .ToListAsync(cancellationToken);

        var paidRegistrations = await (
            from detail in _dbContext.ActivityOrderDetails.AsNoTracking()
            join order in _dbContext.ActivityOrders.AsNoTracking() on detail.ActivityOrderId equals order.OrderId
            where ids.Contains((int)detail.ActivityId)
                  && order.OrderStatusId != 4
            group detail by detail.ActivityId
            into groupByActivity
            select new
            {
                ActivityId = (int)groupByActivity.Key,
                Participants = groupByActivity.Sum(x => x.Quantity)
            }
        ).ToDictionaryAsync(x => x.ActivityId, x => x.Participants, cancellationToken);

        return activities.ToDictionary(
            x => x.ActivityId,
            x =>
            {
                var incrementalParticipants = paidRegistrations.GetValueOrDefault(x.ActivityId);
                return new ActivityInventoryStats
                {
                    Participants = incrementalParticipants,
                    RemainingSlots = Math.Max(0, x.Capacity - incrementalParticipants)
                };
            });
    }

    public async Task<Dictionary<int, AcreInventoryStats>> GetAcreStatsAsync(
        IEnumerable<int> acreProjectIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(acreProjectIds);
        if (ids.Count == 0)
        {
            return [];
        }

        var paidSubscriptions = await (
            from detail in _dbContext.CommodityOrderDetails.AsNoTracking()
            join order in _dbContext.CommodityOrders.AsNoTracking() on detail.OrderId equals order.OrderId
            where false
            group detail by detail.CommodityId
            into groupByAcre
            select new
            {
                AcreProjectId = groupByAcre.Key,
                Sold = groupByAcre.Sum(x => x.Quantity)
            }
        ).ToDictionaryAsync(x => x.AcreProjectId, x => x.Sold, cancellationToken);

        return ids.ToDictionary(
            id => id,
            id =>
            {
                var sold = paidSubscriptions.GetValueOrDefault(id);
                return new AcreInventoryStats
                {
                    Sold = Math.Max(0, sold),
                    Remaining = Math.Max(0, DefaultAcreCapacity - sold),
                    Total = DefaultAcreCapacity
                };
            });
    }

    public async Task<int> GetAvailableCommodityStockAsync(
        int commodityId, int? totalQuantity, int? inStock,
        CancellationToken cancellationToken = default)
    {
        if ((inStock ?? 0) <= 0)
            return 0;

        var total = Math.Max(0, totalQuantity ?? 0);

        var soldQuantity = await _dbContext.CommodityOrderDetails
            .AsNoTracking()
            .Where(d => d.CommodityId == commodityId)
            .Join(_dbContext.CommodityOrders.AsNoTracking()
                    .Where(o => o.OrderStatusId != 5),
                detail => detail.OrderId,
                order => order.OrderId,
                (detail, order) => detail.Quantity)
            .SumAsync(cancellationToken);

        return Math.Max(0, total - soldQuantity);
    }

    private static List<int> NormalizeIds(IEnumerable<int> ids)
    {
        return ids
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }
}
