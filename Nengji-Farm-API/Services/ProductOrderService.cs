using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

public class ProductOrderService : IProductOrderService
{
    private readonly ManageAppDbContext _context;
    private readonly ILogger<ProductOrderService> _logger;
    private readonly IWeChatPayService _weChatPayService;

    public ProductOrderService(ManageAppDbContext context, ILogger<ProductOrderService> logger, IWeChatPayService weChatPayService)
    {
        _context = context;
        _logger = logger;
        _weChatPayService = weChatPayService;
    }

    public async Task<ProductOrderListResponseDto> GetOrderListAsync(
        int pageNum, int pageSize, string? keyword, int? statusId, CancellationToken cancellationToken)
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

        if (statusId.HasValue)
            query = query.Where(x => x.o.OrderStatusId == statusId.Value);

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

        var orderStatusMap = await LoadOrderStatusMappingAsync(cancellationToken);

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
                (var os, var ps, var dn) = MapSubscriptionStatus(item.o.OrderStatusId, orderStatusMap);
                return new ProductOrderListItemDto
                {
                    OrderId = item.o.OrderNo,
                    CommodityOrderId = item.o.OrderId,
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
                (var os, var ps, var dn) = MapRetailStatus(item.o.OrderStatusId, orderStatusMap);
                return new ProductOrderListItemDto
                {
                    OrderId = item.o.OrderNo,
                    CommodityOrderId = item.o.OrderId,
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

        var orderStatusMap = await LoadOrderStatusMappingAsync(cancellationToken);

        string orderStatus, paymentStatus, deliveryNote;
        string orderType, orderSource, deliveryMethod;
        List<LogisticsRecordDto> logisticsRecords;

        if (isSubscription)
        {
            (orderStatus, paymentStatus, deliveryNote) = MapSubscriptionStatus(order.o.OrderStatusId, orderStatusMap);
            orderType = "认购一亩田";
            orderSource = "认购一亩田";
            deliveryMethod = "一亩田认购";
            logisticsRecords = new List<LogisticsRecordDto>();
        }
        else
        {
            (orderStatus, paymentStatus, deliveryNote) = MapRetailStatus(order.o.OrderStatusId, orderStatusMap);
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
                    "pending" => "退款中",
                    "completed" => "已退款",
                    "rejected" => "已驳回",
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

    public async Task UpdateOrderStatusAsync(UpdateProductOrderStatusDto dto, string? operatorName, CancellationToken cancellationToken)
    {
        var order = await _context.CommodityOrders
            .FirstOrDefaultAsync(o => o.OrderNo == dto.OrderNo, cancellationToken)
            ?? throw new Exception("订单不存在");

        // 动态加载订单状态映射
        var coStatusMap = await OrderStatusHelper.LoadCommodityOrderStatusMapAsync(_context, cancellationToken);
        var cosPendingPayment = coStatusMap.Require("待付款", "commodity_order_status");
        var cosPendingShipment = coStatusMap.Require("待发货", "commodity_order_status");
        var cosShipping = coStatusMap.Require("运输中", "commodity_order_status");
        var cosCompleted = coStatusMap.Require("已完成", "commodity_order_status");
        var cosCancelled = coStatusMap.Require("已取消", "commodity_order_status");
        var cosRefunding = coStatusMap.Require("退款中", "commodity_order_status");
        var cosRefunded = coStatusMap.Require("已退款", "commodity_order_status");

        switch (dto.Action)
        {
            case "cancel-pending-payment":
                if (order.OrderStatusId != cosPendingPayment)
                    throw new Exception("仅待付款订单可取消");
                await RestoreCommodityStockAsync(order.OrderId, cancellationToken);
                order.OrderStatusId = cosCancelled;
                break;

            case "cancel-pending-shipment":
                if (order.OrderStatusId != cosPendingShipment)
                    throw new Exception("仅待发货订单可取消");
                await RestoreCommodityStockAsync(order.OrderId, cancellationToken);
                order.OrderStatusId = cosCancelled;
                break;

            case "cancel-pending-receipt":
                if (order.OrderStatusId != cosShipping)
                    throw new Exception("仅待收货订单可取消");
                await RestoreCommodityStockAsync(order.OrderId, cancellationToken);
                order.OrderStatusId = cosCancelled;
                break;

            case "ship":
                if (order.OrderStatusId != cosPendingShipment)
                    throw new Exception("仅待发货订单可发货");
                if (string.IsNullOrWhiteSpace(dto.LogisticsType))
                    throw new Exception("发货必须填写物流类型");
                if (string.IsNullOrWhiteSpace(dto.LogisticsNo))
                    throw new Exception("发货必须填写物流单号");

                var trackingType = await _context.TrackingTypes
                    .FirstOrDefaultAsync(t => t.TrackingTypeName == dto.LogisticsType, cancellationToken)
                    ?? throw new Exception($@"物流类型「{dto.LogisticsType}」不存在");

                order.OrderStatusId = cosShipping;
                order.TrackingNumber = dto.LogisticsNo;
                order.TrackingTypeId = trackingType.TrackingTypeId;
                break;

            case "refund-request":
                if (order.OrderStatusId != cosPendingShipment)
                    throw new Exception("仅待发货订单可申请退款");
                order.OrderStatusId = cosRefunding;

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
                    Status = "pending",
                    CreateTime = DateTime.Now
                };
                _context.RefundRecords.Add(refundRecord);
                break;

            case "refund-process":
                if (order.OrderStatusId != cosRefunding)
                    throw new Exception("仅退款中订单可处理退款");

                // 查找待处理的退款记录
                var pendingRefundRecord = await _context.RefundRecords
                    .Where(r => r.OrderNo == dto.OrderNo && r.Status == "pending")
                    .OrderByDescending(r => r.CreateTime)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new Exception("未找到待处理的退款记录");

                // 调用微信退款（仅真实支付订单走微信退款）
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
                        RefundDesc = pendingRefundRecord.Reason ?? "管理员退款",
                    };

                    await _weChatPayService.ProcessRefundAsync(weChatRequest, cancellationToken);
                        _logger.LogInformation("微信退款成功 - OrderNo: {OrderNo}, TransactionId: {TransactionId}", order.OrderNo, order.WxPayNo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "微信退款失败 - OrderNo: {OrderNo}, WxPayNo: {WxPayNo}", order.OrderNo, order.WxPayNo);
                        throw new Exception($"微信退款失败：{ex.Message}");
                    }
                }

                // 恢复商品库存
                await RestoreCommodityStockAsync(order.OrderId, cancellationToken);

                // 更新退款记录
                var now = DateTime.Now;
                var refundNo = $"RF{now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
                pendingRefundRecord.RefundNo = refundNo;
                pendingRefundRecord.Status = "completed";
                pendingRefundRecord.ProcessTime = now;
                pendingRefundRecord.AdminReply = operatorName;
                pendingRefundRecord.RefundAmount = order.TotalAmount;

                // 更新订单状态为已退款
                order.OrderStatusId = cosRefunded;
                break;

            case "refund-reject":
                if (order.OrderStatusId != cosRefunding)
                    throw new Exception("仅退款中订单可驳回退款");
                order.OrderStatusId = cosPendingShipment;

                var pendingRefund = await _context.RefundRecords
                    .Where(r => r.OrderNo == dto.OrderNo && r.Status == "pending")
                    .OrderByDescending(r => r.CreateTime)
                    .FirstOrDefaultAsync(cancellationToken);
                if (pendingRefund != null)
                {
                    pendingRefund.Status = "rejected";
                    pendingRefund.ProcessTime = DateTime.Now;
                    pendingRefund.AdminReply = dto.AdminReply;
                    pendingRefund.ProcessNote = dto.ProcessNote;
                }
                break;

            case "subscription-sign":
                break;

            case "subscription-complete":
                if (order.OrderStatusId != cosPendingShipment)
                    throw new Exception("认购订单状态不正确");
                order.OrderStatusId = cosCompleted;
                break;

            default:
                throw new Exception($"不支持的操作: {dto.Action}");
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("订单 {OrderNo} 执行 {Action} 成功", dto.OrderNo, dto.Action);
    }

    public async Task<ProductOrderRefundResponse> RefundAsync(ProductOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNo) && request.OrderId <= 0)
            throw new Exception("请求参数不完整：orderNo 或 orderId 必须提供");

        CommodityOrder? order;
        if (request.OrderId > 0)
            order = await _context.CommodityOrders
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);
        else
            order = await _context.CommodityOrders
                .FirstOrDefaultAsync(o => o.OrderNo == request.OrderNo, cancellationToken);

        if (order is null)
            throw new Exception("订单不存在或已被删除");

        // 动态加载订单状态映射
        var coStatusMap = await OrderStatusHelper.LoadCommodityOrderStatusMapAsync(_context, cancellationToken);
        var cosPendingPayment = coStatusMap.Require("待付款", "commodity_order_status");
        var cosPendingShipment = coStatusMap.Require("待发货", "commodity_order_status");
        var cosShipping = coStatusMap.Require("运输中", "commodity_order_status");
        var cosCancelled = coStatusMap.Require("已取消", "commodity_order_status");
        var cosRefunding = coStatusMap.Require("退款中", "commodity_order_status");
        var cosRefunded = coStatusMap.Require("已退款", "commodity_order_status");

        // 幂等性检查：是否已退款
        var existingRefund = await _context.RefundRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderNo == order.OrderNo && r.OrderType == "goods", cancellationToken);

        if (existingRefund is not null)
            throw new Exception("该订单已完成退款，请勿重复操作");

        // 检查订单状态（仅已支付/待发货/运输中 可退款）
        if (order.OrderStatusId == cosRefunded)
            throw new Exception("该订单已完成退款，请勿重复操作");

        if (order.OrderStatusId != cosPendingShipment && order.OrderStatusId != cosShipping)
            throw new Exception("当前订单状态不允许退款（仅已支付订单可退款）");

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
                _logger.LogInformation("微信退款成功 - OrderNo: {OrderNo}, OutTradeNo: {OutTradeNo}", order.OrderNo, order.OrderNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信退款失败 - OrderNo: {OrderNo}, WxPayNo: {WxPayNo}", order.OrderNo, order.WxPayNo);
                throw new Exception($"微信退款失败：{ex.Message}");
            }
        }

        // 恢复商品库存
        await RestoreCommodityStockAsync(order.OrderId, cancellationToken);

        var now = DateTime.Now;
        var refundNo = $"RF{now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";

        var refund = new RefundRecord
        {
            RefundNo = refundNo,
            OrderId = order.OrderId,
            OrderNo = order.OrderNo,
            OrderType = "goods",
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
        order.OrderStatusId = cosRefunded;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("退款成功 - RefundNo: {RefundNo}, OrderNo: {OrderNo}, Amount: {Amount}, Operator: {Operator}",
            refundNo, order.OrderNo, order.TotalAmount, operatorName);

        return new ProductOrderRefundResponse
        {
            RefundId = refundNo,
            OrderId = order.OrderNo,
            RefundAmount = order.TotalAmount.ToString("F2"),
            RefundTime = now.ToString("yyyy-MM-dd HH:mm"),
            Operator = operatorName,
        };
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

    private async Task<Dictionary<int, string>> LoadOrderStatusMappingAsync(CancellationToken cancellationToken)
    {
        return await _context.CommodityOrderStatuses
            .ToDictionaryAsync(x => x.OrderStatusId, x => x.StatusName, cancellationToken);
    }

    private static (string orderStatus, string paymentStatus, string deliveryNote) MapRetailStatus(
        int statusId, Dictionary<int, string> statusMap)
    {
        var name = statusMap.GetValueOrDefault(statusId, "未知");
        var payment = statusId switch
        {
            1 => "待支付",
            5 or 7 => "已退款",
            _ => "已支付"
        };
        var note = statusId switch
        {
            1 => "等待客户支付",
            2 => "待仓库发货",
            3 => "已发货，等待客户签收",
            4 => "订单已完成",
            5 => "订单已取消",
            6 => "客户已申请退款，等待平台处理",
            7 => "退款已处理完成",
            _ => name
        };
        return (name, payment, note);
    }

    private static (string orderStatus, string paymentStatus, string deliveryNote) MapSubscriptionStatus(
        int statusId, Dictionary<int, string> statusMap)
    {
        var name = statusId == 2 ? "待签约" : statusMap.GetValueOrDefault(statusId, "未知");
        var payment = statusId switch
        {
            1 => "待支付",
            7 => "已退款",
            _ => "已支付"
        };
        var note = statusId switch
        {
            1 => "等待客户支付",
            2 => "支付完成，待后台确认签约",
            4 => "认购已完成",
            7 => "退款已处理完成",
            _ => name
        };
        return (name, payment, note);
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
