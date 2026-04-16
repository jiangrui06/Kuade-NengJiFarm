using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/logistics")]
public class LogisticsController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";
    private readonly AppDbContext _dbContext;

    public LogisticsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// GET /api/logistics/{orderId} - 获取物流详情信息
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetDetail(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!long.TryParse(orderId, out var id) || id <= 0)
            {
                return Ok(ApiResult.Fail("订单 ID 参数错误", 400));
            }

            var userId = ResolveCurrentUserId();
            if (userId <= 0)
            {
                return Ok(ApiResult.Fail("Unauthorized", 401));
            }

            var order = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == id, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            if (order.UserId != userId)
            {
                return Ok(ApiResult.Fail("无权查询该订单", 403));
            }

            if (order.OrderType != 1 || (order.OrderStatus != 2 && order.OrderStatus != 3))
            {
                return Ok(ApiResult.Fail("订单尚未发货，无法查询物流", 409));
            }

            var shipTime = ComputeShipTime(order);
            var estimatedArrival = shipTime.AddDays(2).Date.AddHours(18);

            var shippingAddress = await LoadShippingAddressAsync(userId, order.AddressId, cancellationToken);
            var addressText = shippingAddress is null
                ? (order.ShippingAddress ?? string.Empty)
                : $"{shippingAddress.Province}{shippingAddress.City}{shippingAddress.MunicipalDistrict}{shippingAddress.Town}{shippingAddress.HouseNumber}";

            var name = shippingAddress?.ContactName ?? order.ContactPerson ?? string.Empty;
            var phone = shippingAddress?.ContactPhone ?? order.ContactNumber ?? string.Empty;

            var items = await (
                    from detail in _dbContext.OrderDetails.AsNoTracking()
                    join commodity in _dbContext.Commodities.AsNoTracking()
                        on detail.CommodityId equals commodity.CommodityId
                    where detail.OrderId == id
                    select new
                    {
                        id = detail.CommodityId,
                        name = commodity.ProductName,
                        image = NormalizeMediaUrl(commodity.ImageUrl) ?? string.Empty,
                        price = detail.ActualUnitPrice > 0 ? detail.ActualUnitPrice : (commodity.UnitPrice ?? detail.UnitPrice),
                        quantity = detail.PurchaseQuantity,
                        subtotal = detail.SubtotalAmount > 0 ? detail.SubtotalAmount : detail.ActualUnitPrice * detail.PurchaseQuantity
                    })
                .ToListAsync(cancellationToken);

            var companyCode = "SF";
            var companyName = "顺丰快递";

            var status = order.OrderStatus == 3 ? "completed" : "shipping";
            var statusText = order.OrderStatus == 3 ? "已完成" : "运输中";

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId,
                orderNumber = order.OrderNumber,
                companyName,
                companyCode,
                waybillNo = BuildWaybillNo(companyCode, order),
                status,
                statusText,
                shippingAddress = new
                {
                    name,
                    phone = MaskPhone(phone),
                    address = addressText
                },
                items,
                shipTime = shipTime.ToString("yyyy-MM-dd HH:mm:ss"),
                estimatedArrival = estimatedArrival.ToString("yyyy-MM-dd HH:mm:ss")
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"查询物流信息失败：{ex.Message}", -1));
        }
    }

    /// <summary>
    /// GET /api/logistics/{orderId}/trace - 获取物流轨迹信息（倒序）
    /// </summary>
    [HttpGet("{orderId}/trace")]
    public async Task<IActionResult> GetTrace(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!long.TryParse(orderId, out var id) || id <= 0)
            {
                return Ok(ApiResult.Fail("订单 ID 参数错误", 400));
            }

            var userId = ResolveCurrentUserId();
            if (userId <= 0)
            {
                return Ok(ApiResult.Fail("Unauthorized", 401));
            }

            var order = await _dbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == id, cancellationToken);

            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            if (order.UserId != userId)
            {
                return Ok(ApiResult.Fail("无权查询该订单", 403));
            }

            if (order.OrderType != 1 || (order.OrderStatus != 2 && order.OrderStatus != 3))
            {
                return Ok(ApiResult.Fail("订单尚未发货，无法查询物流", 409));
            }

            var shipTime = ComputeShipTime(order);
            var now = DateTime.Now;
            var isCompleted = order.OrderStatus == 3;

            // 模拟轨迹：倒序（最新在前）
            var trace = new List<object>();

            if (isCompleted)
            {
                trace.Add(new
                {
                    time = shipTime.AddDays(2).AddHours(10).ToString("yyyy-MM-dd HH:mm:ss"),
                    desc = "快件已签收，感谢使用",
                    location = "广州市",
                    status = "delivered"
                });
            }
            else
            {
                trace.Add(new
                {
                    time = MinToNow(shipTime.AddDays(1).AddHours(8), now).ToString("yyyy-MM-dd HH:mm:ss"),
                    desc = "快递员正在派送中",
                    location = "广州市",
                    status = "delivering"
                });
            }

            trace.Add(new
            {
                time = MinToNow(shipTime.AddDays(1).AddHours(2), now).ToString("yyyy-MM-dd HH:mm:ss"),
                desc = "快件已到达【广州转运中心】",
                location = "广州市",
                status = "arrived"
            });
            trace.Add(new
            {
                time = MinToNow(shipTime.AddHours(18), now).ToString("yyyy-MM-dd HH:mm:ss"),
                desc = "快件已从【深圳集散中心】发出",
                location = "深圳市",
                status = "departed"
            });
            trace.Add(new
            {
                time = MinToNow(shipTime.AddHours(16), now).ToString("yyyy-MM-dd HH:mm:ss"),
                desc = "快件已到达【深圳集散中心】",
                location = "深圳市",
                status = "arrived"
            });
            trace.Add(new
            {
                time = shipTime.ToString("yyyy-MM-dd HH:mm:ss"),
                desc = "快递员已揽收",
                location = "深圳市",
                status = "picked"
            });

            return Ok(ApiResult.Success(trace));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"查询物流轨迹失败：{ex.Message}", -1));
        }
    }

    private int ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? Request.Headers["X-User-Id"].FirstOrDefault();

        return int.TryParse(userIdValue, out var userId) && userId > 0 ? userId : 0;
    }

    private async Task<ShippingAddress?> LoadShippingAddressAsync(int userId, int addressId, CancellationToken cancellationToken)
    {
        var query = _dbContext.ShippingAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (addressId > 0)
        {
            var matchedAddress = await query
                .FirstOrDefaultAsync(x => x.AddressId == addressId, cancellationToken);

            if (matchedAddress is not null)
            {
                return matchedAddress;
            }
        }

        return await query
            .OrderByDescending(x => EF.Property<bool>(x, DefaultFlagProperty))
            .ThenByDescending(x => x.AddressId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DateTime ComputeShipTime(OrderEntity order)
    {
        if (order.PaymentTime > DateTime.MinValue.AddDays(1))
        {
            return order.PaymentTime.AddHours(2);
        }

        return order.OrderCreationTime.AddHours(4);
    }

    private static DateTime MinToNow(DateTime value, DateTime now)
    {
        return value > now ? now : value;
    }

    private static string BuildWaybillNo(string companyCode, OrderEntity order)
    {
        // 示例运单号：SF + 13 位数字
        var suffix = (order.OrderId % 10000000000000L).ToString().PadLeft(13, '0');
        return $"{companyCode}{suffix}";
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 7)
        {
            return phone.Trim();
        }

        return $"{digits[..3]}****{digits[^4..]}";
    }

    private static string? NormalizeMediaUrl(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var trimmed = rawPath.Trim().Trim('`', '"', '\'');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Replace("http://127.0.0.1:5000", "http://192.168.203.56");
        }

        var normalizedName = trimmed.TrimStart('/');
        var ext = Path.GetExtension(trimmed).ToLowerInvariant();
        var baseUrl = "http://192.168.203.56";

        if (ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv")
        {
            return $"{baseUrl}/api/file/video/{normalizedName}";
        }

        return $"{baseUrl}/api/file/image/{normalizedName}";
    }
}
