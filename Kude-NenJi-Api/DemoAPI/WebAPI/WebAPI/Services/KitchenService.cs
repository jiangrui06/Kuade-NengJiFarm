using System.Data;

using Microsoft.AspNetCore.Mvc;
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
    /// 修改说明：
    /// - type=0 待出餐：只返回已付款且有待出菜品的订单（排除待付款订单）
    /// - type=1 已出餐：只返回已付款且所有菜品都处理完的订单（排除待付款订单）
    /// 
    /// StatusId 说明：
    /// - 1: 待付款 ? 后厨不显示
    /// - 2: 待出餐 ? 待出餐列表
    /// - 3: 已出餐 ? 已出餐列表
    /// - 4: 已取消 ? 已出餐列表（作为已完成）
    /// </summary>
    public async Task<List<KitchenOrderListItemDto>> GetTodayOrderListAsync(int type = 0, CancellationToken cancellationToken = default)
    {
        List<DishOrders> orders;

        if (type == 0)
        {
            // 待出餐：返回有 StatusId = 2 的菜品的订单
            orders = await _context.DishOrders
                .Where(o => !_context.DishOrderDetails.Any(d => d.DishOrderId == o.OrderId && d.StatusId == 1))  // 排除待付款
                .Where(o => _context.DishOrderDetails.Any(d => d.DishOrderId == o.OrderId && d.StatusId == 2))    // 有待出菜品
                .OrderByDescending(o => o.CreateTime)
                .ToListAsync(cancellationToken);
        }
        else if (type == 1)
        {
            // 已出餐（已完成）：返回没有 StatusId = 2 的菜品的订单（即全是 3 或 4）
            orders = await _context.DishOrders
                .Where(o => !_context.DishOrderDetails.Any(d => d.DishOrderId == o.OrderId && d.StatusId == 1))  // 排除待付款
                .Where(o => !_context.DishOrderDetails.Any(d => d.DishOrderId == o.OrderId && d.StatusId == 2))   // 没有待出菜品
                .Where(o => _context.DishOrderDetails.Any(d => d.DishOrderId == o.OrderId))                        // 至少得有菜
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
                    Status = detail.StatusId,
                    Price = detail.UnitPrice
                });
            }

            result.Add(new KitchenOrderListItemDto
            {
                Id = order.OrderId,
                No = order.OrderNo,
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

        //var details = await _context.DishOrderDetails
        //    .Where(d => d.DishOrderId == orderId)
        //    .ToListAsync(cancellationToken);

        var details = await _context.DishOrderDetails
        .Where(d => d.DishOrderId == orderId)
        .ToListAsync(cancellationToken);

        //var details = await _context.DishOrderDetails
        //.Include(d => d.DishId) // 假设你的实体里定义了 Dish 导航属性
        //.Where(d => d.DishOrderId == orderId)
        //.ToListAsync(cancellationToken);

        //var DiningTables = await _context.DiningTables
        //.Where(d => d.DiningTableId == )
        //.ToListAsync(cancellationToken);

        var DiningTables = await _context.DiningTables
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
    /// StatusId 说明：
    /// - 1: 待付款（不统计）
    /// - 2: 待出餐（待出菜品）
    /// - 3: 已出餐（已完成菜品）
    /// - 4: 已取消（已完成菜品）
    /// </summary>
    public async Task<KitchenStatisticsDto> GetTodayStatisticsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Now.Date;
        var tomorrow = today.AddDays(1);

        // 1. 一次性获取今日所有订单及其明细状态
        var todayOrdersWithDetails = await _context.DishOrders
            .Where(o => o.CreateTime >= today && o.CreateTime < tomorrow)
            .Select(o => new
            {
                o.TotalAmount,
                // 获取该订单下所有菜品的 StatusId 列表
                DetailsStatus = _context.DishOrderDetails
                    .Where(d => d.DishOrderId == o.OrderId)
                    .Select(d => d.StatusId)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // 2. 判定「已完成订单」：订单有菜品，且不存在 StatusId = 2 的菜品
        // 即所有菜品都是 3（已出）或 4（已取消）
        var finishedOrders = todayOrdersWithDetails
            .Where(o => o.DetailsStatus.Count > 0 
                && !o.DetailsStatus.Any(status => status == 2))  // ✅ 核心：没有待出菜品
            .ToList();

        // 3. 计算各项指标
        var totalAmount = finishedOrders.Sum(o => o.TotalAmount);  // 仅统计已完成订单金额
        var totalOrderCount = todayOrdersWithDetails.Count;         // 今日总订单数
        var finishedOrderCount = finishedOrders.Count;              // 今日已完成订单数

        // 4. 统计菜品数量（展平所有状态进行计数）
        var allStatus = todayOrdersWithDetails
            .SelectMany(o => o.DetailsStatus)
            .Where(s => s != 1)  // ✅ 排除待付款状态
            .ToList();

        // 待出餐菜品 = StatusId = 2 的
        var pendingDish = allStatus.Count(s => s == 2);

        // 已完成菜品 = StatusId = 3 或 4 的
        var finishedDish = allStatus.Count(s => s == 3 || s == 4);

        return new KitchenStatisticsDto
        {
            TodayTotalAmount = totalAmount,
            TodayTotalOrder = totalOrderCount,
            TodayFinishedOrder = finishedOrderCount,
            TodayPendingDish = pendingDish,
            TodayFinishedDish = finishedDish
        };
    }


        //private readonly YourDbContext _context; // 替換為你的 DbContext

        //public KitchenService(YourDbContext context)
        //{
        //    _context = context;
        //}

        public async Task<(bool Success, string Message, object? Data)> CancelDishAsync(int detailId, CancellationToken ct)
        {
            // 1. 查詢明細
            var detail = await _context.DishOrderDetails
                .FirstOrDefaultAsync(d => d.DishOrderDetailsId == detailId, ct);

            if (detail == null)
                return (false, "未找到該菜品明細", null);

            // 2. 業務判定：已出餐 (StatusId == 3) 不可取消
            if (detail.StatusId == 3)
                return (false, "已出餐的菜品不可取消", null);

            // 3. 執行取消：將狀態改为 3 (根據你的文檔要求)
            detail.StatusId = 3;

            await _context.SaveChangesAsync(ct);

            // 返回成功結果
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
    private string GetDishStatusName(int statusId)
    {
        return statusId switch
        {
            1 => "未付款",
            2 => "待出餐",
            3 => "已出餐",
            4 => "已取消"
        };
    }
}