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
    /// 获取订单列表
    /// StatusId 说明：
    /// - 1: 待付款（后厨不显示）
    /// - 2: 待出餐（type=2）
    /// - 3: 已出餐（type=3）
    /// </summary>
    public async Task<List<KitchenOrderListItemDto>> GetTodayOrderListAsync(int type, CancellationToken cancellationToken = default)
    {
        // 1. 校验与提示语修正
        if (type != 2 && type != 4)
        {
            throw new Exception("type 参数值不正确，仅支持 2 (待出餐) 或 4 (已出餐)");
        }

        var today = DateTime.Today; // 今天的 00:00:00
        var tomorrow = today.AddDays(1); // 明天的 00:00:00

        //var result = new List<KitchenOrderListItemDto>();

    //    var orders = await _context.DishOrde(o => o.CreateTime >= today && o.CreateTime < tomorrow)
    //.Where(o => _context.DishOrderDetails
    //    .Any(d => d.DishOrderId == o.OrderId && d.StatusId == type))
    //.OrderByDescending(o => o.CreateTime)
    //.ToListAsync(cancellationToken);ers
    //.Wher

        //2.主查询：获取包含特定状态菜品的订单
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

            result.Add(new KitchenOrderListItemDto
            {
                Id = order.OrderId,
                No = order.OrderNo,
                Time = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Table = tableNumber ?? "外带/未知",
                Total = order.TotalAmount,
                //Remark = order.Remark
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
        // 查询订单明细
        var detail = await _context.DishOrderDetails
            .FirstOrDefaultAsync(d => d.DishOrderDetailsId == dishOrderDetailsId, cancellationToken);

        if (detail == null)
        {
            throw new Exception("菜品明细不存在");
        }

        // 查询订单
        var order = await _context.DishOrders
            .FirstOrDefaultAsync(o => o.OrderId == detail.DishOrderId, cancellationToken);

        if (order == null)
        {
            throw new Exception("订单不存在");
        }

        // 幂等性检查：如果订单状态已经是“已出餐”，直接返回成功
        var currentStatus = await _context.DishOrderStatuses
            .FirstOrDefaultAsync(s => s.OrderStatusId == order.OrderStatusId, cancellationToken);

        if (currentStatus != null && currentStatus.OrderStatusId == 4)
        {
            _logger.LogInformation($"订单已出餐，无需重复标记 - OrderId: {order.OrderId}");

            return new MarkDishFinishResponseDto
            {
                AllFinished = true,
                FinishDish = 0, // 不需要统计明细
                TotalDish = 0   // 不需要统计明细
            };
        }

        // 更新订单状态为“已出餐”
        var finishedStatus = await _context.DishOrderStatuses
            .FirstOrDefaultAsync(s => s.OrderStatusId == 4, cancellationToken);

        if (finishedStatus == null)
        {
            throw new Exception("未找到'已出餐'状态，请检查状态配置");
        }

        order.OrderStatusId = finishedStatus.OrderStatusId;

        // 保存更改
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"订单状态已更新为'已出餐' - OrderId: {order.OrderId}");

        return new MarkDishFinishResponseDto
        {
            AllFinished = true,
            FinishDish = 0, // 不需要统计明细
            TotalDish = 0   // 不需要统计明细
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
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        // 1. 只取今天 + 只取有效状态（2、4）
        var details = await _context.DishOrderDetails
            .Where(d => _context.DishOrders
    .Where(o => o.CreateTime >= today && o.CreateTime < tomorrow)
    .Select(o => o.OrderId)
    .Contains(d.DishOrderId))
            .Where(d => d.StatusId == 2 || d.StatusId == 4)
            .ToListAsync(cancellationToken);

        // 2. 统计菜品
        var pendingDish = details.Count(d => d.StatusId == 2);
        var finishedDish = details.Count(d => d.StatusId == 4);

        // 3. 统计营业额（只算已出餐）
        var totalAmount = details
            .Where(d => d.StatusId == 4)
            .Sum(d => d.SubtotalAmount);

        // 4. 订单统计（只统计包含有效菜品的订单）
        var orderIds = details
            .Select(d => d.DishOrderId)
            .Distinct()
            .ToList();

        var totalOrderCount = orderIds.Count;

        var finishedOrderCount = details
            .Where(d => d.StatusId == 4)
            .Select(d => d.DishOrderId)
            .Distinct()
            .Count();

        return new KitchenStatisticsDto
        {
            TodayTotalAmount = totalAmount,        // ✅ 真实营业额
            TodayTotalOrder = totalOrderCount,     // ✅ 有效订单数
            TodayFinishedOrder = finishedOrderCount,
            TodayPendingDish = pendingDish,        // ✅ 待出餐数量
            TodayFinishedDish = finishedDish       // ✅ 已出餐数量
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
            3 => "已取消",
            4 => "已出餐"
        };
    }
}