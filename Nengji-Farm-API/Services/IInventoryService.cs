namespace WebAPI.Services;

public enum ProductType
{
    Commodity,
    Dish
}

public class InventoryResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ProductName { get; init; }

    public static InventoryResult Ok() => new() { Success = true };
    public static InventoryResult Fail(string productName) =>
        new() { Success = false, ErrorMessage = $"商品 {productName} 库存不足", ProductName = productName };
}

/// <summary>
/// 库存扣减与恢复服务 — 所有操作直接走数据库原子 SQL，不依赖内存缓存
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// 原子扣减库存。使用 UPDATE ... WHERE stock >= qty + ROW_COUNT() 检查，
    /// 无需事务即可保证并发安全。
    /// </summary>
    Task<InventoryResult> DeductAsync(ProductType type, int productId, int quantity);

    /// <summary>
    /// 批量原子扣减。任一失败则全部回滚（需在外层事务中调用）。
    /// </summary>
    Task<InventoryResult> DeductBatchAsync(
        ProductType type,
        IReadOnlyList<(int productId, int quantity, string productName)> items);

    /// <summary>
    /// 恢复库存。用于取消订单时加回库存。
    /// </summary>
    Task RestoreAsync(ProductType type, int productId, int quantity);
}
