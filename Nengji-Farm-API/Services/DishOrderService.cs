using Microsoft.EntityFrameworkCore;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

public class DishOrderService : IDishOrderService
{
    private readonly ManageAppDbContext _context;
    private readonly ILogger<DishOrderService> _logger;
    private readonly IWeChatPayService _weChatPayService;

    public DishOrderService(ManageAppDbContext context, ILogger<DishOrderService> logger, IWeChatPayService weChatPayService)
    {
        _context = context;
        _logger = logger;
        _weChatPayService = weChatPayService;
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
                OrderPrimaryId = item.o.OrderId,
                DishOrderId = item.o.OrderId,
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
                OrderPrimaryId = order.o.OrderId,
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

    public async Task<DishOrderRefundResponse> RefundAsync(DishOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNo) && request.OrderId <= 0)
            throw new Exception("请求参数不完整：orderNo 或 orderId 必须提供");

        DishOrders? order;
        if (request.OrderId > 0)
            order = await _context.DishOrders
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);
        else
            order = await _context.DishOrders
                .FirstOrDefaultAsync(o => o.OrderNo == request.OrderNo, cancellationToken);

        if (order is null)
            throw new Exception("订单不存在或已被删除");

        // 动态加载订单状态映射
        var dishStatusMap = await OrderStatusHelper.LoadDishOrderStatusMapAsync(_context, cancellationToken);
        var dsCompleted = dishStatusMap.Require("已完成", "dish_order_status");
        var dsCancelled = dishStatusMap.Require("已取消", "dish_order_status");
        var dsPending = dishStatusMap.Require("待付款", "dish_order_status");
        var dsCooking = dishStatusMap.Require("待出餐", "dish_order_status");

        // 幂等性检查
        var existingRefund = await _context.RefundRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderNo == order.OrderNo && r.OrderType == "food", cancellationToken);

        if (existingRefund is not null)
            throw new Exception("该订单已完成退款，请勿重复操作");

        if (order.OrderStatusId == dsCompleted || order.OrderStatusId == dsCancelled)
            throw new Exception("该订单已完成或已取消，无法退款");

        if (order.OrderStatusId != dsPending && order.OrderStatusId != dsCooking)
            throw new Exception("当前订单状态不允许退款");

        // 调用微信退款
        if (!string.IsNullOrWhiteSpace(order.WxPayNo) &&
            !order.WxPayNo.StartsWith("MOCK_", StringComparison.Ordinal) &&
            !order.WxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal) &&
            order.WxPayNo.All(char.IsDigit))
        {
            try
            {
                var totalFeeFen = (int)(order.TotalAmount * 100);
                var weChatRequest = new WeChatRefundRequest
                {
                    OutTradeNo = order.OrderNo,
                    TotalFeeFen = totalFeeFen,
                    RefundFeeFen = totalFeeFen,
                    RefundDesc = request.RefundReason,
                };

                await _weChatPayService.ProcessRefundAsync(weChatRequest, cancellationToken);
                _logger.LogInformation("微信退款成功 - OrderNo: {OrderNo}", order.OrderNo, order.WxPayNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信退款失败 - OrderNo: {OrderNo}, WxPayNo: {WxPayNo}", order.OrderNo, order.WxPayNo);
                throw new Exception($"微信退款失败：{ex.Message}");
            }
        }

        var now = DateTime.Now;
        var refundNo = $"RF{now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";

        var refund = new RefundRecord
        {
            RefundNo = refundNo,
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            OrderType = "food",
            UserId = order.UserId,
            Reason = "管理员退款",
            Description = request.RefundReason,
            RefundAmount = order.TotalAmount,
            Status = "completed",
            AdminReply = operatorName,
            ProcessTime = now,
            CreateTime = now,
        };

        _context.RefundRecords.Add(refund);

        // 更新订单状态为已退款
        order.OrderStatusId = dsCancelled;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("退款成功 - RefundNo: {RefundNo}, OrderNo: {OrderNo}, Amount: {Amount}, Operator: {Operator}",
            refundNo, order.OrderNo, order.TotalAmount, operatorName);

        return new DishOrderRefundResponse
        {
            RefundId = refundNo,
            OrderId = order.OrderNo,
            RefundAmount = order.TotalAmount.ToString("F2"),
            RefundTime = now.ToString("yyyy-MM-dd HH:mm"),
            Operator = operatorName,
        };
    }
}
