using Microsoft.EntityFrameworkCore;

using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

public class DishOrderService : IDishOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DishOrderService> _logger;

    public DishOrderService(AppDbContext context, ILogger<DishOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DishOrderListResponseDto> GetOrderListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken)
    {
        if (pageNum < 1) pageNum = 1;
        if (pageSize < 1) pageSize = 15;

        var query = from o in _context.DishOrders
                    join t in _context.DiningTables on o.DiningTableId equals t.DiningTableId into tJoin
                    from t in tJoin.DefaultIfEmpty()
                    join u in _context.Users on o.UserId equals u.UserId into uJoin
                    from u in uJoin.DefaultIfEmpty()
                    select new { o, t, u };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(x => x.o.OrderNo.Contains(kw) || x.t!.TableNo.Contains(kw));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.o.CreateTime)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderIds = items.Select(x => x.o.OrderId).ToList();
        var detailStatuses = await _context.DishOrderDetails
            .Where(d => orderIds.Contains(d.DishOrderId))
            .Select(d => new { d.DishOrderId, d.StatusId })
            .ToListAsync(cancellationToken);

        var statusLookup = detailStatuses
            .GroupBy(d => d.DishOrderId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StatusId).ToList());

        var records = items.Select(item =>
        {
            var statuses = statusLookup.GetValueOrDefault(item.o.OrderId, new List<int>());
            var (orderStatus, paymentStatus) = MapOrderStatus(item.o.OrderStatusId);
            var kitchenStatus = AggregateKitchenStatus(statuses);

            return new DishOrderListItemDto
            {
                OrderId = item.o.OrderNo,
                CustomerWechat = item.u?.WxName ?? string.Empty,
                ContactPhone = item.u?.PhoneNumber ?? string.Empty,
                TableNo = item.t?.TableNo ?? string.Empty,
                DishCount = item.o.TotalQuantity,
                ActualAmount = item.o.TotalAmount,
                PaymentMethod = "微信支付",
                PaymentStatus = paymentStatus,
                OrderStatus = orderStatus,
                KitchenStatus = kitchenStatus,
                OrderTime = item.o.CreateTime.ToString("yyyy-MM-dd HH:mm")
            };
        }).ToList();

        var pages = (int)Math.Ceiling((double)total / pageSize);

        return new DishOrderListResponseDto
        {
            Records = records,
            Total = total,
            PageNum = pageNum,
            PageSize = pageSize,
            Pages = pages
        };
    }

    public async Task<DishOrderDetailResponseDto> GetOrderDetailAsync(
        string orderNo, CancellationToken cancellationToken)
    {
        var orderQuery = from o in _context.DishOrders
                         join t in _context.DiningTables on o.DiningTableId equals t.DiningTableId into tJoin
                         from t in tJoin.DefaultIfEmpty()
                         join u in _context.Users on o.UserId equals u.UserId into uJoin
                         from u in uJoin.DefaultIfEmpty()
                         where o.OrderNo == orderNo
                         select new { o, t, u };

        var order = await orderQuery.FirstOrDefaultAsync(cancellationToken);
        if (order == null)
        {
            throw new Exception("订单不存在");
        }

        var details = await (
            from d in _context.DishOrderDetails
            join dish in _context.Dishes on d.DishId equals dish.DishId into dishJoin
            from dish in dishJoin.DefaultIfEmpty()
            where d.DishOrderId == order.o.OrderId
            select new { d, dish }
        ).ToListAsync(cancellationToken);

        var detailStatusIds = details.Select(x => x.d.StatusId).ToList();
        var kitchenStatus = AggregateKitchenStatus(detailStatusIds);
        var (orderStatus, paymentStatus) = MapOrderStatus(order.o.OrderStatusId);

        var orderItems = details.Select(x => new DishOrderItemDto
        {
            Image = x.d.ImageUrl ?? x.dish?.ImageUrl ?? string.Empty,
            Name = !string.IsNullOrEmpty(x.d.GoodsName) ? x.d.GoodsName : (x.dish?.DishName ?? "未知菜品"),
            Description = x.dish?.DishDescription ?? string.Empty,
            Remark = order.o.Remark ?? string.Empty,
            CookingNote = string.Empty,
            Quantity = x.d.Quantity,
            Price = x.d.UnitPrice,
            Subtotal = x.d.SubtotalAmount
        }).ToList();

        var buyerInfo = new DishOrderBuyerInfoDto
        {
            Nickname = order.u?.WxName ?? string.Empty,
            Name = order.u?.RealName ?? string.Empty,
            CustomerWechat = order.u?.WxName ?? "wx_" + order.o.UserId,
            Phone = order.u?.PhoneNumber ?? string.Empty,
            Remark = order.o.Remark ?? string.Empty
        };

        return new DishOrderDetailResponseDto
        {
            OrderInfo = new DishOrderInfoDto
            {
                OrderNo = order.o.OrderNo,
                OrderType = "现场菜品点餐",
                CreateTime = order.o.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                OrderStatus = orderStatus,
                PaymentStatus = paymentStatus,
                KitchenStatus = kitchenStatus,
                TableNo = order.t?.TableNo ?? string.Empty,
                TotalAmount = order.o.TotalAmount,
                PaymentMethod = "微信支付"
            },
            OrderItems = orderItems,
            BuyerInfo = buyerInfo
        };
    }

    private static (string orderStatus, string paymentStatus) MapOrderStatus(int statusId)
    {
        return statusId switch
        {
            1 => ("备餐中", "已支付"),
            2 => ("备餐中", "已支付"),
            3 => ("已完成", "已支付"),
            4 => ("已取消", "已退款"),
            _ => ("未知", "未知")
        };
    }

    private static string AggregateKitchenStatus(List<int> statusIds)
    {
        if (statusIds.Count == 0) return "未知";

        if (statusIds.All(s => s == 3)) return "已取消";
        if (statusIds.All(s => s == 2)) return "已出餐";
        if (statusIds.Any(s => s == 1)) return "待出餐";

        return "未知";
    }
}
