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
            from detail in _dbContext.OrderDetails.AsNoTracking()
            join order in _dbContext.Orders.AsNoTracking() on detail.OrderId equals order.OrderId
            where ids.Contains(detail.CommodityId)
                  && order.OrderType == 1
                  && order.PaymentStatus == 1
                  && order.OrderStatus != 4
            group detail by detail.CommodityId
            into groupByCommodity
            select new
            {
                CommodityId = groupByCommodity.Key,
                Sold = groupByCommodity.Sum(x => x.PurchaseQuantity)
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
            from mealDetail in _dbContext.MealsOrderDetails.AsNoTracking()
            join orderFood in _dbContext.OrderFoods.AsNoTracking() on mealDetail.OrderFoodId equals orderFood.OrderFoodId
            join order in _dbContext.Orders.AsNoTracking() on orderFood.OrderId equals order.OrderId
            where ids.Contains(mealDetail.DishId)
                  && order.OrderType == 2
                  && order.PaymentStatus == 1
                  && order.OrderStatus != 4
            group mealDetail by mealDetail.DishId
            into groupByDish
            select new
            {
                DishId = groupByDish.Key,
                Sold = groupByDish.Sum(x => x.MealOrderQuantity)
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
                x.Participants,
                x.RemainingSlots
            })
            .ToListAsync(cancellationToken);

        var paidRegistrations = await (
            from mealDetail in _dbContext.MealsOrderDetails.AsNoTracking()
            join orderFood in _dbContext.OrderFoods.AsNoTracking() on mealDetail.OrderFoodId equals orderFood.OrderFoodId
            join order in _dbContext.Orders.AsNoTracking() on orderFood.OrderId equals order.OrderId
            where ids.Contains(mealDetail.DishId)
                  && order.OrderType == 4
                  && order.PaymentStatus == 1
                  && order.OrderStatus != 4
            group mealDetail by mealDetail.DishId
            into groupByActivity
            select new
            {
                ActivityId = groupByActivity.Key,
                Participants = groupByActivity.Sum(x => x.MealOrderQuantity)
            }
        ).ToDictionaryAsync(x => x.ActivityId, x => x.Participants, cancellationToken);

        return activities.ToDictionary(
            x => x.ActivityId,
            x =>
            {
                var incrementalParticipants = paidRegistrations.GetValueOrDefault(x.ActivityId);
                return new ActivityInventoryStats
                {
                    Participants = Math.Max(0, x.Participants) + incrementalParticipants,
                    RemainingSlots = Math.Max(0, x.RemainingSlots - incrementalParticipants)
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
            from mealDetail in _dbContext.MealsOrderDetails.AsNoTracking()
            join orderFood in _dbContext.OrderFoods.AsNoTracking() on mealDetail.OrderFoodId equals orderFood.OrderFoodId
            join order in _dbContext.Orders.AsNoTracking() on orderFood.OrderId equals order.OrderId
            where ids.Contains(mealDetail.DishId)
                  && order.OrderType == 3
                  && order.PaymentStatus == 1
                  && order.OrderStatus != 4
            group mealDetail by mealDetail.DishId
            into groupByAcre
            select new
            {
                AcreProjectId = groupByAcre.Key,
                Sold = groupByAcre.Sum(x => x.MealOrderQuantity)
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

    private static List<int> NormalizeIds(IEnumerable<int> ids)
    {
        return ids
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }
}
