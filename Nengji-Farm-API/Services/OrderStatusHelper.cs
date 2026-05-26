using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

/// <summary>
/// 订单状态查询帮助类 — 从数据库动态获取状态 ID，避免硬编码
/// </summary>
public static class OrderStatusHelper
{
    public static async Task<Dictionary<string, int>> LoadActivityOrderStatusMapAsync(ManageAppDbContext db, CancellationToken ct = default)
    {
        return await db.Set<ActivityOrderStatus>()
            .ToDictionaryAsync(s => s.StatusName, s => s.ActivityOrderStatusId, StringComparer.OrdinalIgnoreCase, ct);
    }

    public static async Task<Dictionary<string, int>> LoadCommodityOrderStatusMapAsync(ManageAppDbContext db, CancellationToken ct = default)
    {
        return await db.Set<CommodityOrderStatus>()
            .ToDictionaryAsync(s => s.StatusName, s => s.OrderStatusId, StringComparer.OrdinalIgnoreCase, ct);
    }

    public static async Task<Dictionary<string, int>> LoadDishOrderStatusMapAsync(ManageAppDbContext db, CancellationToken ct = default)
    {
        return await db.Set<DishOrderStatus>()
            .ToDictionaryAsync(s => s.StatusName, s => s.OrderStatusId, StringComparer.OrdinalIgnoreCase, ct);
    }

    public static async Task<Dictionary<string, int>> LoadDishOrderDetailStatusMapAsync(ManageAppDbContext db, CancellationToken ct = default)
    {
        return await db.Set<DishOrderDetailStatus>()
            .ToDictionaryAsync(s => s.StatusName, s => s.DetailStatusId, StringComparer.OrdinalIgnoreCase, ct);
    }

    /// <summary>从状态映射中查找指定名称的状态ID，找不到则抛出异常</summary>
    public static int Require(this Dictionary<string, int> map, string name, string tableName = "状态表")
    {
        if (map.TryGetValue(name, out var id))
            return id;
        throw new KeyNotFoundException($"状态「{name}」在 {tableName} 中不存在，请检查数据库配置");
    }
}
