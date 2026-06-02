using System.Data;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;
using WebAPI.PasswordHash;

namespace WebAPI.Services;

public class KitchenService : IKitchenService
{
    private readonly ManageAppDbContext _context;
    private readonly ILogger<KitchenService> _logger;
    private readonly IPasswordService _passwordService;
    private readonly IPointsService _pointsService;

    public KitchenService(
        ManageAppDbContext context,
        ILogger<KitchenService> logger,
        IPasswordService passwordService,
        IPointsService pointsService)
    {
        _context = context;
        _logger = logger;
        _passwordService = passwordService;
        _pointsService = pointsService;
    }

    /// <summary>
    /// 后厨登录
    /// </summary>
    public async Task<KitchenLoginResponseDto> LoginAsync(string phoneNumber, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(password))
        {
            throw new Exception("账号或密码不能为空");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleId == user.RoleId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning($"后厨登录失败：手机号未注册 - {phoneNumber}");
            throw new Exception("该手机号未注册");
        }

        bool isPasswordValid = _passwordService.VerifyPassword(password, user.Password);

        if (!isPasswordValid)
        {
            _logger.LogWarning($"后厨登录失败：密码错误 - {phoneNumber}");
            throw new Exception("密码错误");
        }

        var allowedRoles = new[] { 4 };

        if (!allowedRoles.Contains(user.RoleId))
        {
            _logger.LogWarning($"权限不足 (RoleID: {user.RoleId})");
            throw new Exception("您的账号没有访问后厨系统的权限");
        }

        _logger.LogInformation($"后厨登录成功 - {phoneNumber}, UserId: {user.UserId}");

        return new KitchenLoginResponseDto
        {
            UserId = user.UserId,
            UserName = user.WxName ?? "后厨人员",
            PhoneNumber = user.PhoneNumber ?? string.Empty
        };
    }

    /// <summary>
    /// 获取订单列表
    /// OrderStatusId 说明：
    /// - 1: 待付款
    /// - 2: 待出餐（type=2）
    /// - 3: 已完成（type=3）
    /// - 4: 已取消
    /// </summary>
    public async Task<List<KitchenOrderListItemDto>> GetTodayOrderListAsync(int type, CancellationToken cancellationToken = default)
    {
        // 1. 校验与提示语修正
        if (type != 2 && type != 3)
        {
            throw new Exception("type 参数值不正确，仅支持 2 (待出餐) 或 3 (已完成)");
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
       var orders = await _context.DishOrders
       .Where(o => o.OrderStatusId == type &&
                   o.CreateTime >= today &&
                   o.CreateTime < tomorrow)
       .OrderByDescending(o => o.CreateTime)
       .ToListAsync(cancellationToken);

        var result = new List<KitchenOrderListItemDto>();

        foreach (var order in orders)
        {
            var tableNumber = await _context.DiningTables
           .Where(t => t.DiningTableId == order.DiningTableId)
           .Select(t => t.TableNo)
           .FirstOrDefaultAsync(cancellationToken);

            var items = await _context.DishOrderDetails
            .Where(d => d.DishOrderId == order.OrderId)
            .Select(d => new KitchenOrderItemDto
            {
                DishOrderDetailsId = d.DishOrderDetailsId,
                Name = _context.Dishes
                    .Where(x => x.DishId == d.DishId)
                    .Select(x => x.DishName)
                    .FirstOrDefault(),
                Quantity = d.Quantity,
                Price = d.UnitPrice,
                Status = d.StatusId
            })
            .ToListAsync(cancellationToken);

            result.Add(new KitchenOrderListItemDto
            {
                Id = order.OrderId,
                No = order.OrderNo,
                Time = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Table = tableNumber ?? "外带/未知",
                Total = order.TotalAmount,
                //Remark = order.Remark
                Items = items
            });
        }

        return result;
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    public async Task<KitchenOrderDetailDto> GetOrderDetailAsync(long orderId, CancellationToken cancellationToken)
    {
        var order = await _context.DishOrders
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

        if (order == null)
        {
            throw new Exception("订单不存在");
        }

        var details = await _context.DishOrderDetails
        .Where(d => d.DishOrderId == orderId)
        .ToListAsync(cancellationToken);

        var tableNo = await _context.DiningTables
            .Where(t => t.DiningTableId == order.DiningTableId)
            .Select(t => t.TableNo)
            .FirstOrDefaultAsync(cancellationToken);

        var dishList = new List<KitchenDishDetailDto>();

        foreach (var detail in details)
        {
            var dish = await _context.Dishes
                .FirstOrDefaultAsync(d => d.DishId == detail.DishId, cancellationToken);

            dishList.Add(new KitchenDishDetailDto
            {
                DishOrderDetailsId = detail.DishOrderDetailsId,
                DishId = detail.DishId,
                DishName = dish?.DishName ?? "未知菜品",
                Quantity = detail.Quantity,
                UnitPrice = detail.UnitPrice,
                SubtotalAmount = detail.SubtotalAmount,
                DishStatus = detail.StatusId,
                DishStatusName = GetDishStatusName(detail.StatusId)
            });
        }

        return new KitchenOrderDetailDto
        {
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            TableNumber = order.DiningTableId,
            CreateTime = order.CreateTime,
            TotalAmount = order.TotalAmount,
            Remark = order.Remark,
            DishList = dishList.Select(d => new KitchenOrderItemDto
            {
                DishOrderDetailsId = d.DishOrderDetailsId,
                Name = d.DishName,
                Quantity = d.Quantity,
                Status = d.DishStatus,
                Price = d.UnitPrice
            }).ToList()
        };
    }

    /// <summary>
    /// 标记菜品为已出餐（核心接口 - 幂等性保证）
    /// </summary>
    public async Task<MarkDishFinishResponseDto> MarkDishFinishAsync(long dishOrderDetailsId, CancellationToken cancellationToken)
    {
        var detail = await _context.DishOrderDetails
            .FirstOrDefaultAsync(d => d.DishOrderDetailsId == dishOrderDetailsId, cancellationToken);

        if (detail == null) throw new Exception("菜品明细不存在");

        // 检查父订单状态：已取消 / 已完成 / 退款中 禁止出餐
        var order = await _context.DishOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == detail.DishOrderId, cancellationToken);

        if (order != null)
        {
            var blockedNames = new[] { "已取消", "已完成", "退款中" };
            var blockedIds = await _context.Set<DishOrderStatus>()
                .AsNoTracking()
                .Where(s => blockedNames.Contains(s.StatusName))
                .Select(s => s.OrderStatusId)
                .ToListAsync(cancellationToken);

            if (blockedIds.Contains(order.OrderStatusId))
            {
                var statusName = await _context.Set<DishOrderStatus>()
                    .AsNoTracking()
                    .Where(s => s.OrderStatusId == order.OrderStatusId)
                    .Select(s => s.StatusName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "未知";

                throw new Exception($"该订单状态为「{statusName}」，无法出餐");
            }
        }

        if (detail.StatusId != 1)
            throw new Exception("该菜品已被处理，无法重复操作");

        detail.StatusId = 2; // 已出餐

        // 检查该订单下是否还有待出餐的菜品，没有则自动完成订单
        // 排除当前菜品（状态尚未保存到数据库，避免查询到旧值）
        var hasPending = await _context.DishOrderDetails
            .AnyAsync(d => d.DishOrderId == detail.DishOrderId
                && d.StatusId == 1
                && d.DishOrderDetailsId != dishOrderDetailsId, cancellationToken);

        if (!hasPending)
        {
            await _context.DishOrders
                .Where(o => o.OrderId == detail.DishOrderId && o.OrderStatusId == 2)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.OrderStatusId, 3), cancellationToken);

            // 订单完成时发放积分
            if (order != null)
            {
                await _pointsService.EarnPointsAsync(order.UserId, order.OrderNo, order.TotalAmount, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new MarkDishFinishResponseDto { AllFinished = true };
    }

    /// <summary>
    /// 获取今日统计数据
    /// DishOrderDetails.StatusId 说明：
    /// - 1: 待出餐
    /// - 2: 已出餐
    /// - 3: 已取消
    /// </summary>
    public async Task<KitchenStatisticsDto> GetTodayStatisticsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        // 1. 获取今日所有订单
        var todayOrders = await _context.DishOrders
            .Where(o => o.CreateTime >= today && o.CreateTime < tomorrow)
            .Select(o => new { o.OrderId })
            .ToListAsync(cancellationToken);

        var orderIds = todayOrders.Select(o => o.OrderId).ToList();
        var totalOrderCount = todayOrders.Count;

        // 2. 获取今日所有订单的菜品明细
        var details = await _context.DishOrderDetails
            .Where(d => orderIds.Contains(d.DishOrderId))
            .ToListAsync(cancellationToken);

        const int DISH_STATUS_PENDING = 1;
        const int DISH_STATUS_FINISHED = 2;

        var pendingDishCount = details.Count(d => d.StatusId == DISH_STATUS_PENDING);
        var finishedDishCount = details.Count(d => d.StatusId == DISH_STATUS_FINISHED);

        // 已完成订单：所有菜品都已出餐（没有待出餐也没有已取消）
        var finishedOrderCount = details
            .GroupBy(d => d.DishOrderId)
            .Count(g => g.Any() && g.All(d => d.StatusId == DISH_STATUS_FINISHED));

        // 今日营业额：只统计已出餐菜品的金额，取消出餐的不计入
        var totalAmount = details
            .Where(d => d.StatusId == DISH_STATUS_FINISHED)
            .Sum(d => d.SubtotalAmount);

        return new KitchenStatisticsDto
        {
            TodayTotalAmount = totalAmount,         // 今日营业额
            TodayTotalOrder = totalOrderCount,      // 今日总订单数
            TodayFinishedOrder = finishedOrderCount, // 已完成订单数
            TodayPendingDish = pendingDishCount,    // 待出餐菜品数
            TodayFinishedDish = finishedDishCount   // 已出餐菜品数
        };
    }

    public async Task<(bool Success, string Message, object? Data)> CancelDishAsync(long detailId, CancellationToken ct)
        {
            var detail = await _context.DishOrderDetails
                .FirstOrDefaultAsync(d => d.DishOrderDetailsId == detailId, ct);

            if (detail == null)
                return (false, "未找到该菜品明细", null);

            if (detail.StatusId != 1)
                return (false, "该菜品已被处理，无法重复操作", null);

            detail.StatusId = 3; // 已取消

            // 检查该订单下是否还有待出餐的菜品，没有则自动完成订单
            // 排除当前菜品（状态尚未保存到数据库，避免查询到旧值）
            var hasPendingAfterCancel = await _context.DishOrderDetails
                .AnyAsync(d => d.DishOrderId == detail.DishOrderId
                    && d.StatusId == 1
                    && d.DishOrderDetailsId != detailId, ct);

            if (!hasPendingAfterCancel)
            {
                var hasServed = await _context.DishOrderDetails
                    .AnyAsync(d => d.DishOrderId == detail.DishOrderId && d.StatusId == 2, ct);

                var targetStatus = hasServed ? 3 : 4; // 已完成 : 已取消
                await _context.DishOrders
                    .Where(o => o.OrderId == detail.DishOrderId && o.OrderStatusId == 2)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.OrderStatusId, targetStatus), ct);
            }

            await _context.SaveChangesAsync(ct);

            return (true, "操作成功", new { dishOrderDetailsId = detailId, status = 3 });
        }
    


    /// <summary>
    /// 获取当前登录用户信息
    /// </summary>
    public async Task<KitchenLoginResponseDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            throw new Exception("用户不存在");
        }

        return new KitchenLoginResponseDto
        {
            UserId = user.UserId,
            UserName = user.WxName ?? user.RealName ?? "后厨人员",
            PhoneNumber = user.PhoneNumber ?? string.Empty,
        };
    }

    /// <summary>
    /// 获取菜品状态名称
    /// </summary>
    private static string GetDishStatusName(int statusId)
    {
        return statusId switch
        {
            1 => "待付款",
            2 => "待出餐",
            3 => "已完成",
            4 => "已取消"
        };
    }
}