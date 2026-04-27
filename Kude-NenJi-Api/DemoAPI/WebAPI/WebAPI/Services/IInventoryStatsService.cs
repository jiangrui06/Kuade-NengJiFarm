namespace WebAPI.Services;

public interface IInventoryStatsService
{
    Task<Dictionary<int, CommodityInventoryStats>> GetCommodityStatsAsync(IEnumerable<int> commodityIds, CancellationToken cancellationToken = default);

    Task<Dictionary<int, DishInventoryStats>> GetDishStatsAsync(IEnumerable<int> dishIds, CancellationToken cancellationToken = default);

    Task<Dictionary<int, ActivityInventoryStats>> GetActivityStatsAsync(IEnumerable<int> activityIds, CancellationToken cancellationToken = default);

    Task<Dictionary<int, AcreInventoryStats>> GetAcreStatsAsync(IEnumerable<int> acreProjectIds, CancellationToken cancellationToken = default);
}

public sealed class CommodityInventoryStats
{
    public int Sold { get; set; }
    public int Stock { get; set; }
}

public sealed class DishInventoryStats
{
    public int Sold { get; set; }
    public int Stock { get; set; }
}

public sealed class ActivityInventoryStats
{
    public int Participants { get; set; }
    public int RemainingSlots { get; set; }
}

public sealed class AcreInventoryStats
{
    public int Sold { get; set; }
    public int Remaining { get; set; }
    public int Total { get; set; }
}
