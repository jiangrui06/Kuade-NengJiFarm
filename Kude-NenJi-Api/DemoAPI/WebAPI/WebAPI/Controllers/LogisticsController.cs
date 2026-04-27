using System.Security.Claims;
using System.Text;
using System.Text.Json;

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
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public LogisticsController(AppDbContext dbContext, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
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

    /// <summary>
    /// 按物流平台+运单号查询物流轨迹（默认按时间降序，最多 2000 条）
    /// </summary>
    [HttpPost("track")]
    public async Task<IActionResult> Track([FromBody] LogisticsTrackRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.PlatformType) || string.IsNullOrWhiteSpace(request.WaybillNo))
            {
                return Ok(ApiResult.Fail("只需要传两个参数：platformType 和 waybillNo", 400));
            }

            var platformType = request.PlatformType.Trim().ToUpperInvariant();
            var waybillNo = request.WaybillNo.Trim();

            // 历史平台值兼容：把 JD/JDL 当作 EMS 处理（京东分支替换为邮政）
            if (platformType is "JD" or "JDL" or "京东")
            {
                platformType = "EMS";
            }

            List<TrackRow> rows;
            var normalizedPlatform = platformType switch
            {
                "顺丰" => "SF",
                "邮政" => "EMS",
                "POST" => "EMS",
                "中国邮政" => "EMS",
                _ => platformType
            };

            if (normalizedPlatform == "SF")
            {
                rows = await QuerySfTrackAsync(waybillNo, cancellationToken);
            }
            else if (normalizedPlatform == "EMS")
            {
                rows = await QueryEmsTrackAsync(waybillNo, cancellationToken);
            }
            else
            {
                return Ok(ApiResult.Fail("当前仅支持顺丰（SF）和邮政（EMS）查询", 400));
            }

            if (rows.Count == 0)
            {
                return Ok(ApiResult.Fail("未查询到物流轨迹，请确认平台类型和单号", 404));
            }

            var list = rows
                .OrderByDescending(x => x.OperationTime)
                .ThenByDescending(x => x.Sequence)
                .Take(2000)
                .Select(x => new
                {
                    operationTime = x.OperationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    remark = x.Remark,
                    routeAddress = x.RouteAddress
                })
                .ToList();

            return Ok(ApiResult.Success(new
            {
                platformType = normalizedPlatform,
                waybillNo,
                orderNo = waybillNo,
                currentProgress = list.FirstOrDefault(),
                list,
                total = list.Count
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"物流查询失败：{ex.Message}", -1));
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

    private async Task<List<TrackRow>> QuerySfTrackAsync(string waybillNo, CancellationToken cancellationToken)
    {
        var trackUrl = _configuration["Logistics:SfTrackUrl"];
        if (string.IsNullOrWhiteSpace(trackUrl))
        {
            trackUrl = "https://qiao.sf-express.com/Api?category=1&apiClassify=3";
        }

        var payload = new
        {
            mailNo = waybillNo,
            trackingNumber = new[] { waybillNo },
            orderNo = waybillNo,
            language = "zh-CN",
            methodType = 1,
            trackingType = 1
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(trackUrl, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"顺丰接口请求失败：{response.StatusCode}");
        }

        var root = JsonSerializer.Deserialize<JsonElement>(json);

        var tracks = new List<TrackRow>();
        ExtractTrackRows(root, tracks);

        return tracks;
    }

    private async Task<List<TrackRow>> QueryEmsTrackAsync(string waybillNo, CancellationToken cancellationToken)
    {
        var emsTrackUrl = _configuration["Logistics:EmsTrackUrl"];
        if (string.IsNullOrWhiteSpace(emsTrackUrl))
        {
            throw new InvalidOperationException("未配置邮政轨迹接口地址（Logistics:EmsTrackUrl）");
        }

        // 参考《中小电商企业接口规范_V2.7》5.19：waybillNo + direction
        var payload = new
        {
            waybillNo,
            direction = "0"
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(emsTrackUrl, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"邮政接口请求失败：{response.StatusCode}");
        }

        var root = JsonSerializer.Deserialize<JsonElement>(json);
        return ExtractEmsTrackRows(root);
    }

    private static List<TrackRow> ExtractEmsTrackRows(JsonElement root)
    {
        var list = new List<TrackRow>();

        // 支持根节点直接是 responseItems 或者嵌套 data.responseItems
        if (TryGetPropertyIgnoreCase(root, "responseItems", out var responseItems) && responseItems.ValueKind == JsonValueKind.Array)
        {
            ParseEmsResponseItems(responseItems, list);
            return list;
        }

        if (TryGetPropertyIgnoreCase(root, "data", out var dataNode))
        {
            if (TryGetPropertyIgnoreCase(dataNode, "responseItems", out var dataItems) && dataItems.ValueKind == JsonValueKind.Array)
            {
                ParseEmsResponseItems(dataItems, list);
                return list;
            }
        }

        return list;
    }

    private static void ParseEmsResponseItems(JsonElement items, List<TrackRow> output)
    {
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (!TryGetStringIgnoreCase(item, "opTime", out var opTimeRaw) || !DateTime.TryParse(opTimeRaw, out var opTime))
            {
                continue;
            }

            TryGetStringIgnoreCase(item, "opDesc", out var opDesc);
            TryGetStringIgnoreCase(item, "opOrgProvName", out var provName);
            TryGetStringIgnoreCase(item, "opOrgCity", out var cityName);
            TryGetStringIgnoreCase(item, "opOrgName", out var orgName);

            var routeAddress = $"{provName}{cityName}{orgName}".Trim();

            output.Add(new TrackRow
            {
                OperationTime = opTime,
                Remark = string.IsNullOrWhiteSpace(opDesc) ? "暂无备注" : opDesc!,
                RouteAddress = routeAddress,
                Sequence = index
            });

            index++;
        }
    }

    private static void ExtractTrackRows(JsonElement node, List<TrackRow> output)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var hasAcceptTime = TryGetStringIgnoreCase(node, "acceptTime", out var acceptTimeRaw);
            var hasRemark = TryGetStringIgnoreCase(node, "remark", out var remark);
            var hasAcceptAddress = TryGetStringIgnoreCase(node, "acceptAddress", out var acceptAddress);

            if (hasAcceptTime && DateTime.TryParse(acceptTimeRaw, out var time))
            {
                output.Add(new TrackRow
                {
                    OperationTime = time,
                    Remark = string.IsNullOrWhiteSpace(remark) ? "暂无备注" : remark!,
                    RouteAddress = string.IsNullOrWhiteSpace(acceptAddress) ? string.Empty : acceptAddress!
                });
            }

            foreach (var property in node.EnumerateObject())
            {
                ExtractTrackRows(property.Value, output);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            var seq = 0;
            foreach (var item in node.EnumerateArray())
            {
                var before = output.Count;
                ExtractTrackRows(item, output);

                for (var i = before; i < output.Count; i++)
                {
                    output[i].Sequence = seq;
                }

                seq++;
            }
        }
    }

    private static bool TryGetStringIgnoreCase(JsonElement obj, string name, out string? value)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public sealed class LogisticsTrackRequest
    {
        public string PlatformType { get; set; } = string.Empty;
        public string WaybillNo { get; set; } = string.Empty;
    }

    private sealed class TrackRow
    {
        public DateTime OperationTime { get; set; }
        public string Remark { get; set; } = string.Empty;
        public string RouteAddress { get; set; } = string.Empty;
        public int Sequence { get; set; }
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
