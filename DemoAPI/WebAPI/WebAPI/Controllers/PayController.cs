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
            if (request is null || request.OrderId <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var order = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == request.OrderId && x.UserId == userId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            if (order.PaymentStatus == 1)
            {
                return Ok(ApiResult.Success(new
                {
                    orderId = order.OrderId,
                    orderNumber = order.OrderNumber,
                    paymentStatus = 1,
                    paymentTime = order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    amount = order.ActualPayment
                }, "订单已支付"));
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null || string.IsNullOrWhiteSpace(user.WxOpenId))
            {
                return Ok(ApiResult.Fail("当前用户缺少微信 openid，无法发起支付", 400));
            }

            var result = await _weChatPayService.CreateJsApiPaymentAsync(
                new WeChatCreatePaymentRequest
                {
                    Description = string.IsNullOrWhiteSpace(request.Description)
                        ? $"农场订单 {order.OrderNumber}"
                        : request.Description.Trim(),
                    OutTradeNo = order.OrderNumber,
                    TotalFeeFen = ConvertAmountToFen(order.ActualPayment),
                    OpenId = user.WxOpenId,
                    Attach = order.OrderId.ToString(),
                    ClientIp = ResolveClientIp(),
                    NotifyUrl = BuildCurrentNotifyUrl()
                },
                cancellationToken);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId,
                orderNumber = order.OrderNumber,
                paymentStatus = order.PaymentStatus,
                amount = order.ActualPayment,
                payParams = new
                {
                    appId = result.AppId,
                    timeStamp = result.TimeStamp,
                    nonceStr = result.NonceStr,
                    package = result.Package,
                    signType = result.SignType,
                    paySign = result.PaySign
                }
            }, "预支付创建成功"));
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
            var order = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            return Ok(ApiResult.Success(BuildPaymentStatusResponse(order)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取支付状态失败：{ex.Message}"));
        }
    }

    [Authorize]
    [HttpPost("query-payment-status")]
    public async Task<IActionResult> QueryPaymentStatusAsync(
        [FromBody] QueryPaymentStatusRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.OrderId <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = GetCurrentUserId();
            var order = await _dbContext.Orders
                .FirstOrDefaultAsync(x => x.OrderId == request.OrderId && x.UserId == userId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            if (order.PaymentStatus != 1)
            {
                var weChatResult = await _weChatPayService.QueryPaymentStatusAsync(order.OrderNumber, cancellationToken);
                if (weChatResult.IsSuccess)
                {
                    await MarkOrderPaidAsync(order, weChatResult.TotalFeeFen, weChatResult.TransactionId, cancellationToken);
                }
            }

            await _dbContext.Entry(order).ReloadAsync(cancellationToken);
            return Ok(ApiResult.Success(BuildPaymentStatusResponse(order)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"查询支付状态失败：{ex.Message}"));
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

            var order = await _dbContext.Orders
                .FirstOrDefaultAsync(x => x.OrderNumber == notifyResult.OutTradeNo, cancellationToken);

            if (order is null)
            {
                return WeChatNotifyResponse("FAIL", "ORDER_NOT_FOUND");
            }

            await MarkOrderPaidAsync(order, notifyResult.TotalFeeFen, notifyResult.TransactionId, cancellationToken);
            return WeChatNotifyResponse("SUCCESS", "OK");
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
            var order = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("待支付订单不存在", 404));
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);

            var address = await _dbContext.ShippingAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AddressId == order.AddressId, cancellationToken);

            var orderItems = await (
                from detail in _dbContext.OrderDetails.AsNoTracking()
                join commodity in _dbContext.Commodities.AsNoTracking() on detail.CommodityId equals commodity.CommodityId into commodityJoin
                from commodity in commodityJoin.DefaultIfEmpty()
                where detail.OrderId == order.OrderId
                orderby detail.OrderDetailsId
                select new
                {
                    commodityId = detail.CommodityId,
                    name = commodity != null ? commodity.ProductName : $"商品{detail.CommodityId}",
                    image = commodity != null ? commodity.ImageUrl ?? string.Empty : string.Empty,
                    price = detail.UnitPrice,
                    actualPrice = detail.ActualUnitPrice,
                    count = detail.PurchaseQuantity,
                    subtotal = detail.SubtotalAmount
                })
                .ToListAsync(cancellationToken);

            var discountAmount = Math.Max(0, order.TotalOrderAmount - order.ActualPayment);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId,
                orderNumber = order.OrderNumber,
                totalAmount = order.TotalOrderAmount,
                actualAmount = order.ActualPayment,
                discountAmount,
                paymentStatus = order.PaymentStatus,
                paymentTime = order.PaymentStatus == 1 ? order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null,
                paymentMethod = order.PaymentMethods,
                userInfo = new
                {
                    name = user?.WxName ?? order.ContactPerson,
                    phone = MaskPhone(user?.PhoneNumber ?? order.ContactNumber)
                },
                addressInfo = new
                {
                    contactPerson = address?.ContactName ?? order.ContactPerson,
                    contactNumber = MaskPhone(order.ContactNumber),
                    shippingAddress = address is null ? order.ShippingAddress : BuildAddressText(address)
                },
                orderItems
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取支付信息失败：{ex.Message}"));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private async Task MarkOrderPaidAsync(
        OrderEntity order,
        int totalFeeFen,
        string transactionId,
        CancellationToken cancellationToken)
    {
        if (order.PaymentStatus == 1)
        {
            return;
        }

        var expectedFeeFen = ConvertAmountToFen(order.ActualPayment);
        if (expectedFeeFen != totalFeeFen)
        {
            throw new InvalidOperationException($"支付金额不匹配，订单金额 {expectedFeeFen} 分，回调金额 {totalFeeFen} 分");
        }

        order.PaymentStatus = 1;
        order.PaymentMethods = 1;
        order.OrderStatus = order.OrderStatus == 4 ? 4 : 1;
        order.PaymentTime = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static object BuildPaymentStatusResponse(OrderEntity order)
    {
        return new
        {
            orderId = order.OrderId,
            orderNumber = order.OrderNumber,
            orderStatus = MapOrderStatusText(order.OrderStatus, order.PaymentStatus),
            paymentStatus = order.PaymentStatus,
            paid = order.PaymentStatus == 1,
            paymentMethod = order.PaymentMethods,
            paymentTime = order.PaymentStatus == 1 ? order.PaymentTime.ToString("yyyy-MM-dd HH:mm:ss") : null,
            amount = order.ActualPayment
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

    public sealed class CreateJsApiPayRequest
    {
        public long OrderId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public sealed class QueryPaymentStatusRequest
    {
        public long OrderId { get; set; }
    }
}
