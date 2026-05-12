using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pay")]
public class PayController : ControllerBase
{
    private readonly IWeChatPayService _weChatPayService;
    private readonly AppDbContext _dbContext;

    public PayController(IWeChatPayService weChatPayService, AppDbContext dbContext)
    {
        _weChatPayService = weChatPayService;
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("methods")]
    public IActionResult GetMethods()
    {
        return Ok(ApiResult.Success(new[]
        {
            new
            {
                id = 1,
                name = "微信支付",
                icon = "wechat-pay",
                description = "小程序 JSAPI 支付"
            }
        }));
    }

    [Authorize]
    [HttpPost("jsapi")]
    public async Task<IActionResult> CreateJsApiPayment(
        [FromBody] CreateJsApiPayRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var orderKey = request?.GetOrderKey();
            if (string.IsNullOrWhiteSpace(orderKey))
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var splitOrder = await FindSplitPayOrderAsync(orderKey, userId, request!.Type, cancellationToken);
            if (splitOrder is not null)
            {
                if (splitOrder.StatusId != 1)
                {
                    return Ok(ApiResult.Success(new
                    {
                        orderId = splitOrder.OrderNo,
                        orderNumber = splitOrder.OrderNo,
                        paymentStatus = 1,
                        amount = splitOrder.TotalAmount,
                        transactionId = splitOrder.TransactionId
                    }, "订单已支付"));
                }

                // 支付锁由 TryLockOrderAsync 的事务保证，此处不拦截重试
                var userInfo = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

                if (userInfo is null || string.IsNullOrWhiteSpace(userInfo.WxOpenId))
                {
                    return Ok(ApiResult.Fail("当前用户缺少微信 openid，无法发起支付", 400));
                }

                // 尝试获取支付锁（原子操作：检查状态并设置锁定标记）
                var lockValue = $"LOCKING:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var locked = await TryLockOrderAsync(splitOrder.OrderId, splitOrder.Type, lockValue, cancellationToken);
                if (!locked)
                {
                    return Ok(ApiResult.Fail("订单状态已变更，请重新尝试", 400));
                }

                try
                {
                    var splitResult = await _weChatPayService.CreateJsApiPaymentAsync(
                        new WeChatCreatePaymentRequest
                        {
                            Description = string.IsNullOrWhiteSpace(request.Description)
                                ? $"NengJi Farm Order {splitOrder.OrderNo}"
                                : request.Description.Trim(),
                            OutTradeNo = splitOrder.OrderNo,
                            TotalFeeFen = ConvertAmountToFen(splitOrder.TotalAmount),
                            OpenId = userInfo.WxOpenId,
                            Attach = $"{splitOrder.Type}:{splitOrder.OrderId}",
                            ClientIp = ResolveClientIp(),
                            NotifyUrl = BuildCurrentNotifyUrl()
                        },
                        cancellationToken);

                    // 预支付成功，锁保留（后续 query-payment-status 或 notify 会用真实交易ID覆盖）
                    return Ok(ApiResult.Success(new
                    {
                        appId = splitResult.AppId,
                        timeStamp = splitResult.TimeStamp,
                        nonceStr = splitResult.NonceStr,
                        package = splitResult.Package,
                        signType = splitResult.SignType,
                        paySign = splitResult.PaySign,
                        orderId = splitOrder.OrderNo,
                        orderNumber = splitOrder.OrderNo,
                        paymentStatus = 0,
                        amount = splitOrder.TotalAmount
                    }, "预支付创建成功"));
                }
                catch
                {
                    // 预支付失败，释放锁
                    await UnlockOrderAsync(splitOrder.OrderId, splitOrder.Type, lockValue, cancellationToken);
                    throw;
                }
            }

            return Ok(ApiResult.Fail("订单不存在", 404));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"发起微信支付失败：{ex.Message}"));
        }
    }

    [Authorize]
    [HttpPost("initiate-payment")]
    public Task<IActionResult> InitiatePaymentAsync(
        [FromBody] CreateJsApiPayRequest? request,
        CancellationToken cancellationToken)
    {
        return CreateJsApiPayment(request, cancellationToken);
    }

    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> GetPaymentStatus(
        [FromQuery] long orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (orderId <= 0)
            {
                return Ok(ApiResult.Fail("orderId 参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var splitOrder = await FindSplitPayOrderAsync(orderId, userId, null, cancellationToken);
            if (splitOrder is not null)
            {
                return Ok(ApiResult.Success(BuildSplitPaymentStatusResponse(splitOrder)));
            }

            return Ok(ApiResult.Fail("订单不存在", 404));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取支付状态失败：{ex.Message}"));
        }
    }


    [AllowAnonymous]
    [HttpPost("notify")]
    public async Task<IActionResult> Notify(CancellationToken cancellationToken)
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        try
        {
            var notifyResult = await _weChatPayService.ProcessPaymentNotificationAsync(body, cancellationToken);

            if (!notifyResult.IsSuccess)
            {
                return WeChatNotifyResponse("FAIL", "PAY_NOT_SUCCESS");
            }

            var splitOrder = await FindSplitPayOrderByOrderNoAsync(notifyResult.OutTradeNo, cancellationToken);
            if (splitOrder is not null)
            {
                await MarkSplitOrderPaidAsync(splitOrder, notifyResult.TotalFeeFen, notifyResult.TransactionId, cancellationToken);
                return WeChatNotifyResponse("SUCCESS", "OK");
            }

            return WeChatNotifyResponse("FAIL", "ORDER_NOT_FOUND");
        }
        catch (Exception ex)
        {
            return WeChatNotifyResponse("FAIL", ex.Message);
        }
    }

    [Authorize]
    [HttpGet("info")]
    public async Task<IActionResult> GetInfo([FromQuery] long orderId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var splitOrder = await FindSplitPayOrderAsync(orderId, userId, null, cancellationToken);

            if (splitOrder is null)
            {
                return Ok(ApiResult.Fail("待支付订单不存在", 404));
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == splitOrder.UserId, cancellationToken);

            var orderItems = await LoadSplitOrderItemsAsync(splitOrder, cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderId = splitOrder.OrderId,
                orderNumber = splitOrder.OrderNo,
                totalAmount = splitOrder.TotalAmount,
                actualAmount = splitOrder.TotalAmount,
                discountAmount = 0m,
                paymentStatus = splitOrder.StatusId == 1 ? 0 : 1,
                paymentTime = (string?)null,
                paymentMethod = splitOrder.StatusId == 1 ? 0 : 1,
                transactionId = splitOrder.StatusId != 1 ? splitOrder.TransactionId : null,
                userInfo = new
                {
                    name = user?.WxName ?? string.Empty,
                    phone = MaskPhone(user?.PhoneNumber ?? string.Empty)
                },
                addressInfo = new
                {
                    contactPerson = (string?)null,
                    contactNumber = (string?)null,
                    shippingAddress = (string?)null
                },
                orderItems
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取支付信息失败：{ex.Message}"));
        }
    }

    private async Task<List<object>> LoadSplitOrderItemsAsync(SplitPayOrder order, CancellationToken cancellationToken)
    {
        if (order.Type == "food")
        {
            var details = await _dbContext.DishOrderDetails
                .AsNoTracking()
                .Where(x => x.DishOrderId == order.OrderId)
                .ToListAsync(cancellationToken);
            var dishIds = details.Select(d => d.DishId).Distinct().ToList();
            var dishMap = dishIds.Count == 0
                ? new Dictionary<int, Dish>()
                : await _dbContext.Dishes.AsNoTracking()
                    .Where(x => dishIds.Contains(x.DishId))
                    .ToDictionaryAsync(x => x.DishId, cancellationToken);

            return details.Select(d => new
            {
                commodityId = d.DishId,
                name = dishMap.TryGetValue(d.DishId, out var dish) ? dish.DishName : $"菜品{d.DishId}",
                image = dishMap.TryGetValue(d.DishId, out var imgDish) ? (imgDish.ImageUrl ?? string.Empty) : string.Empty,
                price = d.UnitPrice,
                actualPrice = d.UnitPrice,
                count = d.Quantity,
                subtotal = d.SubtotalAmount
            } as object).ToList();
        }

        if (order.Type == "activity")
        {
            var details = await _dbContext.ActivityOrderDetails
                .AsNoTracking()
                .Where(x => x.ActivityOrderId == order.OrderId)
                .ToListAsync(cancellationToken);
            var activityIds = details.Select(d => (int)d.ActivityId).Distinct().ToList();
            var activityMap = activityIds.Count == 0
                ? new Dictionary<int, ActivityEntity>()
                : await _dbContext.Activities.AsNoTracking()
                    .Where(x => activityIds.Contains((int)x.ActivityId))
                    .ToDictionaryAsync(x => (int)x.ActivityId, cancellationToken);

            return details.Select(d => new
            {
                commodityId = d.ActivityId,
                name = activityMap.TryGetValue((int)d.ActivityId, out var act) ? act.Title : $"活动{d.ActivityId}",
                image = activityMap.TryGetValue((int)d.ActivityId, out var imgAct) ? (imgAct.ImageUrl ?? string.Empty) : string.Empty,
                price = d.UnitPrice,
                actualPrice = d.UnitPrice,
                count = d.Quantity,
                subtotal = d.SubtotalAmount
            } as object).ToList();
        }

        // goods / commodity orders
        var commodityDetails = await _dbContext.CommodityOrderDetails
            .AsNoTracking()
            .Where(x => x.OrderId == order.OrderId)
            .ToListAsync(cancellationToken);
        var commodityIds = commodityDetails.Select(d => d.CommodityId).Distinct().ToList();
        var commodityMap = commodityIds.Count == 0
            ? new Dictionary<int, Commodity>()
            : await _dbContext.Commodities.AsNoTracking()
                .Where(x => commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

        return commodityDetails.Select(d => new
        {
            commodityId = d.CommodityId,
            name = commodityMap.TryGetValue(d.CommodityId, out var c) ? c.ProductName : $"商品{d.CommodityId}",
            image = commodityMap.TryGetValue(d.CommodityId, out var imgC) ? (imgC.ImageUrl ?? string.Empty) : string.Empty,
            price = d.UnitPrice,
            actualPrice = d.UnitPrice,
            count = d.Quantity,
            subtotal = d.SubtotalAmount
        } as object).ToList();
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private async Task<SplitPayOrder?> FindSplitPayOrderAsync(
        long orderId,
        int userId,
        string? type,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeSplitOrderType(type);

        if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType == "goods")
        {
            var commodityOrder = await _dbContext.CommodityOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (commodityOrder is not null)
            {
                return new SplitPayOrder
                {
                    Type = "goods",
                    OrderId = commodityOrder.OrderId,
                    OrderNo = commodityOrder.OrderNo,
                    UserId = commodityOrder.UserId,
                    TotalAmount = commodityOrder.TotalAmount,
                    StatusId = commodityOrder.OrderStatusId,
                    TransactionId = commodityOrder.WxPayNo
                };
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType == "food")
        {
            var dishOrder = await _dbContext.DishOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (dishOrder is not null)
            {
                return new SplitPayOrder
                {
                    Type = "food",
                    OrderId = dishOrder.OrderId,
                    OrderNo = dishOrder.OrderNo,
                    UserId = dishOrder.UserId,
                    TotalAmount = dishOrder.TotalAmount,
                    StatusId = dishOrder.OrderStatusId,
                    TransactionId = dishOrder.WxPayNo
                };
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType == "activity")
        {
            var activityOrder = await _dbContext.ActivityOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
            if (activityOrder is not null)
            {
                return new SplitPayOrder
                {
                    Type = "activity",
                    OrderId = activityOrder.OrderId,
                    OrderNo = activityOrder.OrderNo,
                    UserId = activityOrder.UserId,
                    TotalAmount = activityOrder.TotalAmount,
                    StatusId = activityOrder.OrderStatusId,
                    TransactionId = activityOrder.WxPayNo
                };
            }
        }

        return null;
    }

    private async Task<SplitPayOrder?> FindSplitPayOrderAsync(
        string orderKey,
        int userId,
        string? type,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderKey))
        {
            return null;
        }

        var value = orderKey.Trim();
        var normalizedType = NormalizeSplitOrderType(type);

        if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType == "goods")
        {
            var commodityOrder = await _dbContext.CommodityOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderNo == value && x.UserId == userId, cancellationToken);
            if (commodityOrder is not null)
            {
                return new SplitPayOrder
                {
                    Type = "goods",
                    OrderId = commodityOrder.OrderId,
                    OrderNo = commodityOrder.OrderNo,
                    UserId = commodityOrder.UserId,
                    TotalAmount = commodityOrder.TotalAmount,
                    StatusId = commodityOrder.OrderStatusId,
                    TransactionId = commodityOrder.WxPayNo
                };
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType == "food")
        {
            var dishOrder = await _dbContext.DishOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderNo == value && x.UserId == userId, cancellationToken);
            if (dishOrder is not null)
            {
                return new SplitPayOrder
                {
                    Type = "food",
                    OrderId = dishOrder.OrderId,
                    OrderNo = dishOrder.OrderNo,
                    UserId = dishOrder.UserId,
                    TotalAmount = dishOrder.TotalAmount,
                    StatusId = dishOrder.OrderStatusId,
                    TransactionId = dishOrder.WxPayNo
                };
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType == "activity")
        {
            var activityOrder = await _dbContext.ActivityOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderNo == value && x.UserId == userId, cancellationToken);
            if (activityOrder is not null)
            {
                return new SplitPayOrder
                {
                    Type = "activity",
                    OrderId = activityOrder.OrderId,
                    OrderNo = activityOrder.OrderNo,
                    UserId = activityOrder.UserId,
                    TotalAmount = activityOrder.TotalAmount,
                    StatusId = activityOrder.OrderStatusId,
                    TransactionId = activityOrder.WxPayNo
                };
            }
        }

        return null;
    }

    private async Task<SplitPayOrder?> FindSplitPayOrderByOrderNoAsync(string orderNo, CancellationToken cancellationToken)
    { 
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return null;
        }

        var commodityOrder = await _dbContext.CommodityOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (commodityOrder is not null)
        {
            return new SplitPayOrder
            {
                Type = "goods",
                OrderId = commodityOrder.OrderId,
                OrderNo = commodityOrder.OrderNo,
                UserId = commodityOrder.UserId,
                TotalAmount = commodityOrder.TotalAmount,
                StatusId = commodityOrder.OrderStatusId,
                TransactionId = commodityOrder.WxPayNo
            };
        }

        var dishOrder = await _dbContext.DishOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (dishOrder is not null)
        {
            return new SplitPayOrder
            {
                Type = "food",
                OrderId = dishOrder.OrderId,
                OrderNo = dishOrder.OrderNo,
                UserId = dishOrder.UserId,
                TotalAmount = dishOrder.TotalAmount,
                StatusId = dishOrder.OrderStatusId,
                TransactionId = dishOrder.WxPayNo
            };
        }

        var activityOrder = await _dbContext.ActivityOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (activityOrder is not null)
        {
            return new SplitPayOrder
            {
                Type = "activity",
                OrderId = activityOrder.OrderId,
                OrderNo = activityOrder.OrderNo,
                UserId = activityOrder.UserId,
                TotalAmount = activityOrder.TotalAmount,
                StatusId = activityOrder.OrderStatusId,
                TransactionId = activityOrder.WxPayNo
            };
        }

        return null;
    }

    private async Task MarkSplitOrderPaidAsync(
        SplitPayOrder order,
        int totalFeeFen,
        string transactionId,
        CancellationToken cancellationToken)
    {
        if (order.StatusId != 1)
        {
            return;
        }

        var expectedFeeFen = ConvertAmountToFen(order.TotalAmount);
        if (expectedFeeFen != totalFeeFen)
        {
            throw new InvalidOperationException($"Payment amount mismatch, order {expectedFeeFen} fen, paid {totalFeeFen} fen.");
        }

        if (order.Type == "food")
        {
            var entity = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == order.OrderId, cancellationToken);
            if (entity is not null)
            {
                entity.OrderStatusId = 2;
                entity.WxPayNo = transactionId;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (order.Type == "activity")
        {
            var entity = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.OrderId == order.OrderId, cancellationToken);
            if (entity is not null)
            {
                entity.OrderStatusId = 2;
                entity.WxPayNo = transactionId;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var commodityOrder = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == order.OrderId, cancellationToken);
        if (commodityOrder is not null)
        {
            commodityOrder.OrderStatusId = 2;
            commodityOrder.WxPayNo = transactionId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static object BuildSplitPaymentStatusResponse(SplitPayOrder order)
    {
        var isPaid = order.StatusId != 1;
        return new
        {
            orderId = order.OrderId,
            orderNumber = order.OrderNo,
            orderType = order.Type,
            orderStatus = isPaid ? "paid" : "pending_payment",
            paymentStatus = isPaid ? 1 : 0,
            paid = isPaid,
            paymentMethod = isPaid ? 1 : 0,
            paymentTime = (string?)null,
            amount = order.TotalAmount,
            transactionId = isPaid ? order.TransactionId : null
        };
    }

    private static string? NormalizeSplitOrderType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        return type.Trim().ToLowerInvariant() switch
        {
            "cart" => "goods",
            "commodity" => "goods",
            "goods" => "goods",
            "dish" => "food",
            "food" => "food",
            "activity" => "activity",
            _ => null
        };
    }

    private static string MapOrderStatusText(int orderStatus, int paymentStatus)
    {
        if (orderStatus == 4)
        {
            return "cancelled";
        }

        if (paymentStatus == 0)
        {
            return "pending_payment";
        }

        return orderStatus switch
        {
            1 => "paid",
            2 => "shipped",
            3 => "completed",
            _ => "paid"
        };
    }

    private static int ConvertAmountToFen(decimal amount)
    {
        return Convert.ToInt32(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static string BuildAddressText(ShippingAddress address)
    {
        return $"{address.Province}{address.City}{address.MunicipalDistrict}{address.Town}{address.HouseNumber}";
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7)
        {
            return phone;
        }

        return $"{phone[..3]}****{phone[^4..]}";
    }

    private string? BuildCurrentNotifyUrl()
    {
        var forwardedProto = FirstHeaderValue(Request.Headers["X-Forwarded-Proto"].ToString());
        var forwardedHost = FirstHeaderValue(Request.Headers["X-Forwarded-Host"].ToString());
        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? Request.Scheme : forwardedProto;
        var host = string.IsNullOrWhiteSpace(forwardedHost) ? Request.Host.Value : forwardedHost;

        return string.IsNullOrWhiteSpace(host)
            ? null
            : $"{scheme}://{host}/api/pay/notify";
    }

    private string ResolveClientIp()
    {
        var forwardedFor = FirstHeaderValue(Request.Headers["X-Forwarded-For"].ToString());
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private ContentResult WeChatNotifyResponse(string code, string message)
    {
        var xml = $"<xml><return_code><![CDATA[{SanitizeCdata(code)}]]></return_code><return_msg><![CDATA[{SanitizeCdata(message)}]]></return_msg></xml>";
        return Content(xml, "text/xml", Encoding.UTF8);
    }

    private static string FirstHeaderValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
    }

    private static string SanitizeCdata(string value)
    {
        return (value ?? string.Empty).Replace("]]>", "]]]]><![CDATA[>", StringComparison.Ordinal);
    }

    /// <summary>
    /// 尝试获取支付锁，防止重复支付
    /// </summary>
    private async Task<bool> TryLockOrderAsync(long orderId, string type, string lockValue, CancellationToken ct)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        if (type == "food")
        {
            var entity = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (entity is null || entity.OrderStatusId != 1 || (!string.IsNullOrEmpty(entity.WxPayNo) && !entity.WxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal)))
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            entity.WxPayNo = lockValue;
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }

        if (type == "activity")
        {
            var entity = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (entity is null || entity.OrderStatusId != 1 || (!string.IsNullOrEmpty(entity.WxPayNo) && !entity.WxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal)))
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            entity.WxPayNo = lockValue;
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }

        // goods / commodity
        {
            var entity = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (entity is null || entity.OrderStatusId != 1 || (!string.IsNullOrEmpty(entity.WxPayNo) && !entity.WxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal)))
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            entity.WxPayNo = lockValue;
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
    }

    /// <summary>
    /// 释放支付锁（仅当 WxPayNo 仍为 lockValue 时清除）
    /// </summary>
    private async Task UnlockOrderAsync(long orderId, string type, string expectedLockValue, CancellationToken ct)
    {
        if (type == "food")
        {
            var entity = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (entity is not null && entity.WxPayNo == expectedLockValue)
            {
                entity.WxPayNo = null;
                await _dbContext.SaveChangesAsync(ct);
            }
            return;
        }

        if (type == "activity")
        {
            var entity = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (entity is not null && entity.WxPayNo == expectedLockValue)
            {
                entity.WxPayNo = null;
                await _dbContext.SaveChangesAsync(ct);
            }
            return;
        }

        // goods / commodity
        {
            var entity = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (entity is not null && entity.WxPayNo == expectedLockValue)
            {
                entity.WxPayNo = null;
                await _dbContext.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// 判断支付锁是否已过期（超过5分钟视为过期）
    /// </summary>
    private static bool IsLockExpired(string? wxPayNo)
    {
        if (string.IsNullOrEmpty(wxPayNo) || !wxPayNo.StartsWith("LOCKING:", StringComparison.Ordinal))
            return false;

        if (long.TryParse(wxPayNo.Replace("LOCKING:", ""), out var timestamp))
        {
            var lockTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            return (DateTimeOffset.UtcNow - lockTime).TotalMinutes > 5;
        }

        return true; // 格式无效，视为过期
    }

    public sealed class CreateJsApiPayRequest
    {
        public string? OrderNo { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Type { get; set; }

        public string? GetOrderKey()
        {
            return string.IsNullOrWhiteSpace(OrderNo) ? null : OrderNo.Trim();
        }
    }

    private sealed class SplitPayOrder
    {
        public string Type { get; set; } = string.Empty;
        public long OrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public int StatusId { get; set; }
        public string? TransactionId { get; set; }
    }
}
