using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _dbContext;

    public InventoryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryResult> DeductAsync(ProductType type, int productId, int quantity)
    {
        if (quantity <= 0) return InventoryResult.Ok();

        var sql = type switch
        {
            ProductType.Commodity =>
                $"UPDATE commodity SET in_stock = in_stock - {{0}} WHERE commodity_id = {{1}} AND in_stock >= {{0}}",
            ProductType.Dish =>
                $"UPDATE dish SET dish_remaining_quantity = dish_remaining_quantity - {{0}} WHERE dish_id = {{1}} AND dish_remaining_quantity >= {{0}}",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        // 原子 UPDATE，int 参数无 SQL 注入风险
        var formattedSql = string.Format(sql, quantity, productId);
        var affected = await _dbContext.Database.ExecuteSqlRawAsync(formattedSql);

        if (affected > 0) return InventoryResult.Ok();

        // 扣减失败（库存不足），查名称用于错误提示
        var name = type switch
        {
            ProductType.Commodity => await _dbContext.Commodities
                .Where(x => x.CommodityId == productId)
                .Select(x => x.ProductName)
                .FirstOrDefaultAsync(),
            ProductType.Dish => await _dbContext.Dishes
                .Where(x => x.DishId == productId)
                .Select(x => x.DishName)
                .FirstOrDefaultAsync(),
            _ => null
        };

        return InventoryResult.Fail(name ?? productId.ToString());
    }

    public async Task<InventoryResult> DeductBatchAsync(
        ProductType type,
        IReadOnlyList<(int productId, int quantity, string productName)> items)
    {
        foreach (var (productId, quantity, _) in items)
        {
            var result = await DeductAsync(type, productId, quantity);
            if (!result.Success)
            {
                return result;
            }
        }

        return InventoryResult.Ok();
    }

    public async Task RestoreAsync(ProductType type, int productId, int quantity)
    {
        if (quantity <= 0) return;

        var sql = type switch
        {
            ProductType.Commodity =>
                $"UPDATE commodity SET in_stock = in_stock + {{0}} WHERE commodity_id = {{1}}",
            ProductType.Dish =>
                $"UPDATE dish SET dish_remaining_quantity = dish_remaining_quantity + {{0}} WHERE dish_id = {{1}}",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var formattedSql = string.Format(sql, quantity, productId);
        await _dbContext.Database.ExecuteSqlRawAsync(formattedSql);
    }
}
