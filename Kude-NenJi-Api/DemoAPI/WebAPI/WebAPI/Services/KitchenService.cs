using System;
using System.Data;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Dtos.Kitchen;
using WebAPI.Entities;
using WebAPI.PasswordHash;

namespace WebAPI.Services;

public class KitchenService : IKitchenService
{
    private readonly AppDbContext _context;
    private readonly ILogger<KitchenService> _logger;
    private readonly IPasswordService _passwordService;

    public KitchenService(
        AppDbContext context,
        ILogger<KitchenService> logger,
        IPasswordService passwordService)
    {
        _context = context;
        _logger = logger;
        _passwordService = passwordService;
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
    /// 获取今日订单列表（适配前端格式）
    /// </summary>
    public async Task<List<KitchenOrderListItemDto>> GetTodayOrderListAsync(int type = 0, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Now.Date;
        var tomorrow = today.AddDays(1);

        var ordersQuery = _context.DishOrders
            .Where(o => o.CreateTime >= today && o.CreateTime < tomorrow);

        List<DishOrders> orders;

        if (type == 0)
        {
            // 待出餐：存在未出餐的菜品（StatusId = 1 或 0）
            orders = await ordersQuery
                .Where(o => _context.DishOrderDetails
                    .Where(d => d.DishOrderId == o.OrderId)
                    .Any(d => d.StatusId == 1 || d.StatusId == 0))
                .OrderByDescending(o => o.CreateTime)
                .ToListAsync(cancellationToken);
        }
        else if (type == 1)
        {
            // 已出餐：所有菜品均已出餐（StatusId = 2）
            orders = await ordersQuery
                .Where(o => _context.DishOrderDetails
                    .Where(d => d.DishOrderId == o.OrderId)
                    .All(d => d.StatusId == 2))
                .Where(o => _context.DishOrderDetails
                    .Where(d => d.DishOrderId == o.OrderId)
                    .Any())
                .OrderByDescending(o => o.CreateTime)
                .ToListAsync(cancellationToken);
        }
        else
        {
            throw new Exception("type 参数值不正确，仅支持 0 或 1");
        }

        var result = new List<KitchenOrderListItemDto>();

        foreach (var order in orders)
        {
            var details = await _context.DishOrderDetails
                .Where(d => d.DishOrderId == order.OrderId)
                .ToListAsync(cancellationToken);

            var tableNumber = await _context.DiningTables
                .Where(t => t.DiningTableId == order.DiningTableId)
                .Select(t => t.TableNo)
                .FirstOrDefaultAsync(cancellationToken);

            // 构建菜品列表
            var items = new List<KitchenOrderItemDto>();
            foreach (var detail in details)
            {
                var dish = await _context.Dishes
                    .FirstOrDefaultAsync(d => d.DishId == detail.DishId, cancellationToken);

                items.Add(new KitchenOrderItemDto
                {
                    Name = dish?.DishName ?? "未知菜品",
                    Quantity = detail.Quantity,
                    Status = detail.StatusId == 2,  // true=已出, false=未出
                    Price = detail.UnitPrice
                    // 注：cancelled 字段仅在需要时才返回，默认不返回
                });
            }

            result.Add(new KitchenOrderListItemDto
            {
                Id = order.OrderNo,
                Time = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Table = tableNumber ?? string.Empty,
                Items = items,
                Total = order.TotalAmount
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

        var tableNumber = await _context.DiningTables
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
            TableNumber = tableNumber ?? string.Empty,
            CreateTime = order.CreateTime,
            TotalAmount = order.TotalAmount,
            DishList = dishList.Select(d => new KitchenOrderItemDto
            {
                Name = d.DishName,
                Quantity = d.Quantity,
                Status = d.DishStatus == 2,
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

        if (detail == null)
        {
            throw new Exception("菜品不存在");
        }

        var order = await _context.DishOrders
            .FirstOrDefaultAsync(o => o.OrderId == detail.DishOrderId, cancellationToken);

        if (order == null)
        {
            throw new Exception("订单不存在");
        }

        // 幂等性保证：如果已出餐，直接返回成功
        if (detail.StatusId == 2)
        {
            _logger.LogInformation($"菜品已出餐，无需重复标记 - DishOrderDetailsId: {dishOrderDetailsId}");
            
            var allDetails = await _context.DishOrderDetails
                .Where(d => d.DishOrderId == order.OrderId)
                .ToListAsync(cancellationToken);

            var finishCount = allDetails.Count(d => d.StatusId == 2);
            var allFinished = allDetails.All(d => d.StatusId == 2);

            return new MarkDishFinishResponseDto
            {
                AllFinished = allFinished,
                FinishDish = finishCount,
                TotalDish = allDetails.Count
            };
        }

        // 标记为已出餐（StatusId = 2）
        detail.StatusId = 2;

        // 获取订单的所有菜品
        var orderDetails = await _context.DishOrderDetails
            .Where(d => d.DishOrderId == order.OrderId)
            .ToListAsync(cancellationToken);

        // 检查订单是否全部出餐
        var isAllFinished = orderDetails.All(d => d.StatusId == 2);

        // 更新订单状态（如果全部出餐，更新订单状态为已出餐）
        if (isAllFinished)
        {
            var finishedStatus = await _context.DishOrderStatuses
                .FirstOrDefaultAsync(s => s.StatusName == "已出餐", cancellationToken);
            
            if (finishedStatus != null)
            {
                order.OrderStatusId = finishedStatus.OrderStatusId;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"菜品已标记为出餐 - DishOrderDetailsId: {dishOrderDetailsId}, AllFinished: {isAllFinished}");

        var finishDishCount = orderDetails.Count(d => d.StatusId == 2);
        return new MarkDishFinishResponseDto
        {
            AllFinished = isAllFinished,
            FinishDish = finishDishCount,
            TotalDish = orderDetails.Count
        };
    }

    /// <summary>
    /// 获取今日统计数据
    /// </summary>
    public async Task<KitchenStatisticsDto> GetTodayStatisticsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Now.Date;
        var tomorrow = today.AddDays(1);

        var todayOrders = await _context.DishOrders
            .Where(o => o.CreateTime >= today && o.CreateTime < tomorrow)
            .ToListAsync(cancellationToken);

        var totalAmount = todayOrders.Sum(o => o.TotalAmount);
        var totalOrder = todayOrders.Count;

        // 统计已完成的订单
        var finishedOrder = 0;
        foreach (var order in todayOrders)
        {
            var orderDetails = await _context.DishOrderDetails
                .Where(d => d.DishOrderId == order.OrderId)
                .ToListAsync(cancellationToken);

            if (orderDetails.Count > 0 && orderDetails.All(d => d.StatusId == 2))
            {
                finishedOrder++;
            }
        }

        // 统计所有菜品
        var allDetails = await _context.DishOrderDetails
            .Where(d => todayOrders.Select(o => o.OrderId).Contains(d.DishOrderId))
            .ToListAsync(cancellationToken);

        var pendingDish = allDetails.Count(d => d.StatusId != 2);
        var finishedDish = allDetails.Count(d => d.StatusId == 2);

        return new KitchenStatisticsDto
        {
            TodayTotalAmount = totalAmount,
            TodayTotalOrder = totalOrder,
            TodayFinishedOrder = finishedOrder,
            TodayPendingDish = pendingDish,
            TodayFinishedDish = finishedDish
        };
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
    private string GetDishStatusName(int statusId)
    {
        return statusId switch
        {
            0 => "未出餐",
            1 => "未出餐",
            2 => "已出餐",
            _ => "未知"
        };
    }
}