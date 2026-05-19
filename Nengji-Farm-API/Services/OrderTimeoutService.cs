using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Services;

public class OrderTimeoutService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderTimeoutService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OrderTimeout = TimeSpan.FromMinutes(30);

    public OrderTimeoutService(IServiceProvider serviceProvider, ILogger<OrderTimeoutService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("订单超时自动取消服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CancelTimeoutOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订单超时扫描异常");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CancelTimeoutOrdersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var timeoutThreshold = DateTime.Now.Add(-OrderTimeout);

        // 商品订单：status 1=pending → 5=cancelled
        var goodsOrders = await db.CommodityOrders
            .Where(o => o.OrderStatusId == 1 && o.CreateTime <= timeoutThreshold)
            .ToListAsync(cancellationToken);

        foreach (var order in goodsOrders)
        {
            var details = await db.CommodityOrderDetails
                .Where(d => d.OrderId == order.OrderId)
                .ToListAsync(cancellationToken);

            foreach (var d in details)
            {
                await inventoryService.RestoreAsync(ProductType.Commodity, d.CommodityId, d.Quantity);
            }

            order.OrderStatusId = 5;
            _logger.LogInformation("商品订单 {OrderNo}({OrderId}) 超时未支付，系统自动取消", order.OrderNo, order.OrderId);
        }

        if (goodsOrders.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // 点餐订单：status 1=pending → 4=cancelled
        var foodOrders = await db.DishOrders
            .Where(o => o.OrderStatusId == 1 && o.CreateTime <= timeoutThreshold)
            .ToListAsync(cancellationToken);

        foreach (var order in foodOrders)
        {
            var details = await db.DishOrderDetails
                .Where(d => d.DishOrderId == order.OrderId)
                .ToListAsync(cancellationToken);

            foreach (var d in details)
            {
                await inventoryService.RestoreAsync(ProductType.Dish, d.DishId, d.Quantity);
            }

            order.OrderStatusId = 4;
            _logger.LogInformation("点餐订单 {OrderNo}({OrderId}) 超时未支付，系统自动取消", order.OrderNo, order.OrderId);
        }

        if (foodOrders.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // 活动订单：status 1=pending → 4=cancelled
        var activityOrders = await db.ActivityOrders
            .Where(o => o.OrderStatusId == 1 && o.CreateTime <= timeoutThreshold)
            .ToListAsync(cancellationToken);

        foreach (var order in activityOrders)
        {
            order.OrderStatusId = 4;
            _logger.LogInformation("活动订单 {OrderNo}({OrderId}) 超时未支付，系统自动取消", order.OrderNo, order.OrderId);
        }

        if (activityOrders.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        if (goodsOrders.Count + foodOrders.Count + activityOrders.Count > 0)
        {
            _logger.LogInformation("本次扫描取消 {Count} 笔超时订单（商品 {G}/点餐 {F}/活动 {A}）",
                goodsOrders.Count + foodOrders.Count + activityOrders.Count,
                goodsOrders.Count, foodOrders.Count, activityOrders.Count);
        }
    }
}
