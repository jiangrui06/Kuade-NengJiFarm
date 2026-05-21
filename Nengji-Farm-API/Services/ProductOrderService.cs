using Microsoft.EntityFrameworkCore;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

public class ProductOrderService : IProductOrderService
{
    private readonly ManageAppDbContext _context;
    private readonly ILogger<ProductOrderService> _logger;

    public ProductOrderService(ManageAppDbContext context, ILogger<ProductOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductOrderListResponseDto> GetOrderListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken)
    {
        if (pageNum < 1) pageNum = 1;
        if (pageSize < 1) pageSize = 15;

        var addressSql = "SELECT address_id AS AddressId, user_id AS UserId, contact_name AS ContactName, contact_phone AS ContactPhone, province AS Province, city AS City, municipal_district AS MunicipalDistrict, addres AS Addres, is_default AS IsDefault FROM shipping_address";
        var addressList = await _context.Database
            .SqlQueryRaw<AddressRaw>(addressSql)
            .ToListAsync(cancellationToken);
        var addressLookup = addressList.ToDictionary(a => (long)a.AddressId);

        var query = from o in _context.CommodityOrders
                    join u in _context.Users on o.UserId equals u.UserId into uJoin
                    from u in uJoin.DefaultIfEmpty()
                    join t in _context.TrackingTypes on o.TrackingTypeId equals t.TrackingTypeId into tJoin
                    from t in tJoin.DefaultIfEmpty()
                    select new { o, u, t };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            var matchedAddressIds = addressLookup
                .Where(a => a.Value.ContactName.Contains(kw))
                .Select(a => a.Key)
                .ToHashSet();

            query = query.Where(x =>
                x.o.OrderNo.Contains(kw) ||
                (x.u != null && x.u.WxName != null && x.u.WxName.Contains(kw)) ||
                (x.u != null && x.u.PhoneNumber.Contains(kw)) ||
                (x.o.ReceiverPhone != null && x.o.ReceiverPhone.Contains(kw)) ||
                (x.o.AddressId != null && matchedAddressIds.Contains(x.o.AddressId.Value)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.o.CreateTime)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderIds = items.Select(x => x.o.OrderId).ToList();
        var goodsNames = await _context.CommodityOrderDetails
            .Where(d => orderIds.Contains(d.OrderId))
            .OrderBy(d => d.OrderId)
            .ThenBy(d => d.CommodityOrderDetailsId)
            .Select(d => new { d.OrderId, d.GoodsName })
            .ToListAsync(cancellationToken);

        var summaryLookup = goodsNames
            .GroupBy(g => g.OrderId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.GoodsName).ToList());

        var orderNos = items.Select(x => x.o.OrderNo).ToList();
        var refundRecords = await _context.RefundRecords
            .Where(r => orderNos.Contains(r.OrderNo))
            .GroupBy(r => r.OrderNo)
            .Select(g => g.OrderByDescending(r => r.CreateTime).First())
            .ToListAsync(cancellationToken);
        var refundLookup = refundRecords.ToDictionary(r => r.OrderNo);

        var records = items.Select(item =>
        {
            var isSubscription = item.o.OrderNo.StartsWith("S");
            var names = summaryLookup.GetValueOrDefault(item.o.OrderId, new List<string>());
            var summary = string.Join("、", names.Take(2));
            var refund = refundLookup.GetValueOrDefault(item.o.OrderNo);
            var addr = item.o.AddressId != null ? addressLookup.GetValueOrDefault(item.o.AddressId.Value) : null;

            string orderSource;
            string deliveryMethod;

            if (isSubscription)
            {
                orderSource = "认购一亩田";
                deliveryMethod = "一亩田认购";
                (var os, var ps, var dn) = MapSubscriptionStatus(item.o.OrderStatusId);
                return new ProductOrderListItemDto
                {
                    OrderId = item.o.OrderNo,
                    OrderCategory = "subscription",
                    OrderSource = orderSource,
                    CustomerWechat = item.u?.WxName ?? string.Empty,
                    ContactPhone = item.o.ReceiverPhone ?? item.u?.PhoneNumber ?? string.Empty,
                    ReceiverName = addr?.ContactName ?? string.Empty,
                    UserName = item.u?.RealName ?? addr?.ContactName,
                    ProductSummary = summary,
                    ItemCount = names.Count,
                    ActualAmount = item.o.TotalAmount,
                    DeliveryMethod = deliveryMethod,
                    LogisticsType = item.t?.TrackingTypeName ?? string.Empty,
                    LogisticsNo = item.o.TrackingNumber ?? string.Empty,
                    DeliveryNote = dn,
                    PaymentStatus = ps,
                    OrderStatus = os,
                    OrderTime = item.o.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                    RefundReason = refund?.Reason,
                    RefundApplyTime = refund?.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                    RefundProofImages = ParseRefundImages(refund?.Images)
                };
            }
            else
            {
                orderSource = "商城下单";
                deliveryMethod = (item.o.AddressId > 0 && item.o.TrackingTypeId != null) ? "快递配送" : "到店自提";
                (var os, var ps, var dn) = MapRetailStatus(item.o.OrderStatusId);
                return new ProductOrderListItemDto
                {
                    OrderId = item.o.OrderNo,
                    OrderSource = orderSource,
                    CustomerWechat = item.u?.WxName ?? string.Empty,
                    ContactPhone = item.o.ReceiverPhone ?? item.u?.PhoneNumber ?? string.Empty,
                    ReceiverName = addr?.ContactName ?? string.Empty,
                    ProductSummary = summary,
                    ItemCount = names.Count,
                    ActualAmount = item.o.TotalAmount,
                    DeliveryMethod = deliveryMethod,
                    LogisticsType = item.t?.TrackingTypeName ?? string.Empty,
                    LogisticsNo = item.o.TrackingNumber ?? string.Empty,
                    DeliveryNote = dn,
                    PaymentStatus = ps,
                    OrderStatus = os,
                    OrderTime = item.o.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                    RefundReason = refund?.Reason,
                    RefundApplyTime = refund?.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                    RefundProofImages = ParseRefundImages(refund?.Images)
                };
            }
        }).ToList();

        return new ProductOrderListResponseDto
        {
            Records = records,
            Total = total,
            PageNum = pageNum,
            PageSize = pageSize,
            Pages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    private static List<string>? ParseRefundImages(string? imagesJson)
    {
        if (string.IsNullOrEmpty(imagesJson)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(imagesJson); }
        catch { return null; }
    }

    public async Task<ProductOrderDetailResponseDto> GetOrderDetailAsync(
        string orderNo, CancellationToken cancellationToken)
    {
        var isSubscription = orderNo.StartsWith("S");

        var order = await (
            from o in _context.CommodityOrders
            join u in _context.Users on o.UserId equals u.UserId into uJoin
            from u in uJoin.DefaultIfEmpty()
            join t in _context.TrackingTypes on o.TrackingTypeId equals t.TrackingTypeId into tJoin
            from t in tJoin.DefaultIfEmpty()
            where o.OrderNo == orderNo
            select new { o, u, t }
        ).FirstOrDefaultAsync(cancellationToken);

        if (order == null)
            throw new Exception("订单不存在");

        var addr = await _context.Database
            .SqlQueryRaw<AddressRaw>(
                "SELECT address_id AS AddressId, user_id AS UserId, contact_name AS ContactName, contact_phone AS ContactPhone, province AS Province, city AS City, municipal_district AS MunicipalDistrict, addres AS Addres, is_default AS IsDefault FROM shipping_address WHERE address_id = {0}",
                order.o.AddressId)
            .FirstOrDefaultAsync(cancellationToken);

        var details = await (
            from d in _context.CommodityOrderDetails
            join c in _context.Commodities on d.CommodityId equals c.CommodityId into cJoin
            from c in cJoin.DefaultIfEmpty()
            where d.OrderId == order.o.OrderId
            select new { d, c }
        ).ToListAsync(cancellationToken);

        var commodityIds = details.Select(x => x.d.CommodityId).Distinct().ToList();
        var materialList = await _context.CommodityMaterials
            .Where(m => commodityIds.Contains(m.CommodityId) && m.MaterialType == 0)
            .OrderBy(m => m.CommodityId).ThenBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);
        var materialImageLookup = materialList
            .GroupBy(m => m.CommodityId)
            .ToDictionary(g => g.Key, g => g.First().MaterialUrl);

        var refund = await _context.RefundRecords
            .Where(r => r.OrderNo == orderNo)
            .OrderByDescending(r => r.CreateTime)
            .FirstOrDefaultAsync(cancellationToken);

        string orderStatus, paymentStatus, deliveryNote;
        string orderType, orderSource, deliveryMethod;
        List<LogisticsRecordDto> logisticsRecords;

        if (isSubscription)
        {
            (orderStatus, paymentStatus, deliveryNote) = MapSubscriptionStatus(order.o.OrderStatusId);
            orderType = "认购一亩田";
            orderSource = "认购一亩田";
            deliveryMethod = "一亩田认购";
            logisticsRecords = new List<LogisticsRecordDto>();
        }
        else
        {
            (orderStatus, paymentStatus, deliveryNote) = MapRetailStatus(order.o.OrderStatusId);
            orderType = "农产品购买";
            orderSource = "商城下单";
            deliveryMethod = (order.o.AddressId > 0 && order.o.TrackingTypeId != null) ? "快递配送" : "到店自提";

            logisticsRecords = new List<LogisticsRecordDto>
            {
                new()
                {
                    LogisticsType = order.t?.TrackingTypeName ?? string.Empty,
                    NodeName = order.o.OrderStatusId switch
                    {
                        1 => "等待支付",
                        2 => "订单审核",
                        3 => "已发货",
                        4 => "已完成",
                        5 => "已取消",
                        6 => "退款处理中",
                        7 => "已退款",
                        _ => "订单审核"
                    },
                    Handler = "商城系统",
                    Status = order.o.OrderStatusId >= 3 ? "已完成" : "进行中",
                    UpdateTime = order.o.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                    Remark = deliveryNote
                }
            };
        }

        var orderItems = details.Select(x => new ProductOrderItemDto
        {
            ProductId = x.d.CommodityId.ToString(),
            Image = materialImageLookup.GetValueOrDefault(x.d.CommodityId) ?? x.d.ImageUrl ?? x.c?.ImageUrl ?? string.Empty,
            Name = x.d.GoodsName,
            Description = x.c?.SpecDescription ?? x.c?.ProductName ?? string.Empty,
            Spec = x.c?.SpecDescription ?? string.Empty,
            NetWeight = x.c?.WeightText ?? string.Empty,
            Quantity = x.d.Quantity,
            Price = x.d.UnitPrice,
            Subtotal = x.d.SubtotalAmount
        }).ToList();

        string fullAddress;
        if (deliveryMethod == "到店自提")
        {
            fullAddress = "到店自提";
        }
        else if (addr != null)
        {
            fullAddress = $"{addr.Province}{addr.City}{addr.MunicipalDistrict}{addr.Addres}";
        }
        else
        {
            fullAddress = string.Empty;
        }

        return new ProductOrderDetailResponseDto
        {
            OrderInfo = new ProductOrderInfoDto
            {
                OrderNo = order.o.OrderNo,
                OrderType = orderType,
                CreateTime = order.o.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                OrderStatus = orderStatus,
                PaymentStatus = paymentStatus,
                DeliveryMethod = deliveryMethod,
                LogisticsType = order.t?.TrackingTypeName ?? string.Empty,
                DeliveryNote = deliveryNote,
                TotalAmount = order.o.TotalAmount,
                PaymentMethod = "微信支付"
            },
            OrderItems = orderItems,
            LogisticsRecords = logisticsRecords,
            BuyerInfo = new ProductOrderBuyerInfoDto
            {
                Nickname = order.u?.WxName ?? string.Empty,
                Name = order.u?.RealName ?? string.Empty,
                CustomerWechat = order.u?.WxName ?? string.Empty,
                Phone = order.u?.PhoneNumber ?? string.Empty,
                OrderSource = orderSource,
                Remark = string.Empty
            },
            FulfillmentInfo = new FulfillmentInfoDto
            {
                ReceiverName = addr?.ContactName ?? string.Empty,
                Phone = addr?.ContactPhone ?? order.o.ReceiverPhone ?? string.Empty,
                Address = fullAddress,
                Schedule = order.t?.TrackingTypeName ?? string.Empty,
                TrackingNo = order.o.TrackingNumber ?? string.Empty,
                Remark = string.Empty
            },
            RefundInfo = refund != null ? new ProductOrderRefundInfoDto
            {
                RefundId = refund.RefundId.ToString(),
                RefundNo = refund.RefundNo,
                RefundAmount = refund.RefundAmount,
                RefundStatus = refund.Status switch
                {
                    "待处理" => "退款中",
                    "completed" or "已退款" => "已退款",
                    "已驳回" => "已驳回",
                    _ => refund.Status
                },
                RefundReason = refund.Reason,
                RefundApplyTime = refund.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                RefundProofImages = ParseRefundImages(refund.Images),
                AdminReply = refund.AdminReply,
                ProcessNote = refund.ProcessNote,
            } : null
        };
    }

    public async Task UpdateOrderStatusAsync(UpdateProductOrderStatusDto dto, CancellationToken cancellationToken)
    {
        var order = await _context.CommodityOrders
            .FirstOrDefaultAsync(o => o.OrderNo == dto.OrderNo, cancellationToken)
            ?? throw new Exception("订单不存在");

        switch (dto.Action)
        {
            case "cancel-pending-payment":
                if (order.OrderStatusId != 1)
                    throw new Exception("仅待付款订单可取消");
                await RestoreCommodityStockAsync(order.OrderId, cancellationToken);
                order.OrderStatusId = 5;
                break;

            case "cancel-pending-shipment":
                if (order.OrderStatusId != 2)
                    throw new Exception("仅待发货订单可取消");
                await RestoreCommodityStockAsync(order.OrderId, cancellationToken);
                order.OrderStatusId = 5;
                break;

            case "cancel-pending-receipt":
                if (order.OrderStatusId != 3)
                    throw new Exception("仅待收货订单可取消");
                await RestoreCommodityStockAsync(order.OrderId, cancellationToken);
                order.OrderStatusId = 5;
                break;

            case "ship":
                if (order.OrderStatusId != 2)
                    throw new Exception("仅待发货订单可发货");
                if (string.IsNullOrWhiteSpace(dto.LogisticsType))
                    throw new Exception("发货必须填写物流类型");
                if (string.IsNullOrWhiteSpace(dto.LogisticsNo))
                    throw new Exception("发货必须填写物流单号");

                var trackingType = await _context.TrackingTypes
                    .FirstOrDefaultAsync(t => t.TrackingTypeName == dto.LogisticsType, cancellationToken)
                    ?? throw new Exception($"物流类型「{dto.LogisticsType}」不存在");

                order.OrderStatusId = 3;
                order.TrackingNumber = dto.LogisticsNo;
                order.TrackingTypeId = trackingType.TrackingTypeId;
                break;

            case "refund-request":
                if (order.OrderStatusId != 2)
                    throw new Exception("仅待发货订单可申请退款");
                order.OrderStatusId = 6;

                var refundRecord = new RefundRecord
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,
                    OrderType = "commodity",
                    UserId = order.UserId,
                    Reason = dto.RefundReason ?? "用户申请退款",
                    Description = dto.RefundReason,
                    Images = dto.EffectiveRefundImages != null
                        ? System.Text.Json.JsonSerializer.Serialize(dto.EffectiveRefundImages)
                        : null,
                    RefundAmount = order.TotalAmount,
                    Status = "待处理",
                    CreateTime = DateTime.Now
                };
                _context.RefundRecords.Add(refundRecord);
                break;

            case "refund-process":
                if (order.OrderStatusId != 6)
                    throw new Exception("仅退款中订单可处理退款");
                order.OrderStatusId = 7;

                var existingRefund = await _context.RefundRecords
                    .Where(r => r.OrderNo == dto.OrderNo && r.Status == "待处理")
                    .OrderByDescending(r => r.CreateTime)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingRefund != null)
                {
                    existingRefund.Status = "已退款";
                    existingRefund.ProcessTime = DateTime.Now;
                }
                break;

            case "refund-reject":
                if (order.OrderStatusId != 6)
                    throw new Exception("仅退款中订单可驳回退款");
                order.OrderStatusId = 2;

                var pendingRefund = await _context.RefundRecords
                    .Where(r => r.OrderNo == dto.OrderNo && r.Status == "待处理")
                    .OrderByDescending(r => r.CreateTime)
                    .FirstOrDefaultAsync(cancellationToken);
                if (pendingRefund != null)
                {
                    pendingRefund.Status = "已驳回";
                    pendingRefund.ProcessTime = DateTime.Now;
                    pendingRefund.AdminReply = dto.AdminReply;
                    pendingRefund.ProcessNote = dto.ProcessNote;
                }
                break;

            case "subscription-sign":
                break;

            case "subscription-complete":
                if (order.OrderStatusId != 2)
                    throw new Exception("认购订单状态不正确");
                order.OrderStatusId = 4;
                break;

            default:
                throw new Exception($"不支持的操作: {dto.Action}");
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("订单 {OrderNo} 执行 {Action} 成功", dto.OrderNo, dto.Action);
    }

    private async Task RestoreCommodityStockAsync(long orderId, CancellationToken cancellationToken)
    {
        var details = await _context.CommodityOrderDetails
            .Where(d => d.OrderId == orderId)
            .ToListAsync(cancellationToken);

        foreach (var detail in details)
        {
            var commodity = await _context.Commodities
                .FirstOrDefaultAsync(c => c.CommodityId == detail.CommodityId, cancellationToken);
            if (commodity?.InStock != null)
            {
                commodity.InStock += detail.Quantity;
            }
        }
    }

    private static (string orderStatus, string paymentStatus, string deliveryNote) MapRetailStatus(int statusId)
    {
        return statusId switch
        {
            1 => ("待支付", "待支付", "等待客户支付"),
            2 => ("待发货", "已支付", "待仓库发货"),
            3 => ("待收货", "已支付", "已发货，等待客户签收"),
            4 => ("已完成", "已支付", "订单已完成"),
            5 => ("已取消", "已退款", "订单已取消"),
            6 => ("退款中", "已支付", "客户已申请退款，等待平台处理"),
            7 => ("已退款", "已退款", "退款已处理完成"),
            _ => ("未知", "未知", "")
        };
    }

    private static (string orderStatus, string paymentStatus, string deliveryNote) MapSubscriptionStatus(int statusId)
    {
        return statusId switch
        {
            1 => ("待支付", "待支付", "等待客户支付"),
            2 => ("待签约", "已支付", "支付完成，待后台确认签约"),
            4 => ("已完成", "已支付", "认购已完成"),
            7 => ("已退款", "已退款", "退款已处理完成"),
            _ => ("未知", "未知", "")
        };
    }
}

internal class AddressRaw
{
    public long AddressId { get; set; }
    public int UserId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string MunicipalDistrict { get; set; } = string.Empty;
    public string Addres { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
