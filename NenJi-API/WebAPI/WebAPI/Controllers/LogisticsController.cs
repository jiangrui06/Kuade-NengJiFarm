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
[Route("api/logistics")]
public class LogisticsController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";

    /// <summary>
    /// 物流公司信息映射表
    /// </summary>
    private static readonly Dictionary<string, (string Name, string Phone)> CompanyMap = new()
    {
        ["SF"] = ("顺丰快递", "95338"),
        ["EMS"] = ("中国邮政", "11183"),
        ["YTO"] = ("圆通速递", "95554"),
        ["ZTO"] = ("中通快递", "95311"),
        ["STO"] = ("申通快递", "95543"),
        ["YD"] = ("韵达快递", "95546"),
    };

    // 微信 access_token 简单缓存
    private static string? _cachedAccessToken;
    private static DateTime _accessTokenExpiry = DateTime.MinValue;

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
    /// 接口一：获取物流详情
    /// </summary>
    [HttpGet("{orderId}")]
    [Authorize]
    public async Task<IActionResult> GetDetail(string orderId, CancellationToken cancellationToken = default)
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

        var order = await _dbContext.CommodityOrders
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

        // 商品订单已发货（OrderStatusId == 3 shipping）或已完成（OrderStatusId == 4 completed）才可查物流
        if (order.OrderStatusId != 3 && order.OrderStatusId != 4)
        {
            return Ok(ApiResult.Fail("订单尚未发货，无法查询物流", 409));
        }

        var (companyCode, companyName, companyPhone) = ResolveCompanyInfo(order.TrackingTypeId);
        var waybillNo = ResolveWaybillNo(companyCode, order);
        var status = order.OrderStatusId == 4 ? "completed" : "shipping";
        var statusText = order.OrderStatusId == 4 ? "已完成" : "运输中";
        var shipTime = ComputeShipTime(order);
        var estimatedArrival = shipTime.AddDays(2).Date.AddHours(18);

        var shippingAddress = await LoadShippingAddressAsync(userId, order.AddressId, cancellationToken);
        var addressText = shippingAddress is null
            ? string.Empty
            : $"{shippingAddress.Province}{shippingAddress.City}{shippingAddress.MunicipalDistrict}{shippingAddress.Addres}";

        var items = await (
                from detail in _dbContext.CommodityOrderDetails.AsNoTracking()
                join commodity in _dbContext.Commodities.AsNoTracking()
                    on detail.CommodityId equals commodity.CommodityId
                where detail.OrderId == id
                select new
                {
                    id = detail.CommodityId,
                    name = commodity.ProductName,
                    image = NormalizeMediaUrl(commodity.ImageUrl) ?? string.Empty,
                    price = detail.UnitPrice > 0 ? detail.UnitPrice : (commodity.UnitPrice ?? detail.UnitPrice),
                    quantity = detail.Quantity,
                    subtotal = detail.SubtotalAmount > 0 ? detail.SubtotalAmount : detail.UnitPrice * detail.Quantity
                })
            .ToListAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            orderId = order.OrderId,
            orderNumber = order.OrderNo,
            companyName,
            companyCode,
            companyPhone,
            waybillNo,
            status,
            statusText,
            shippingAddress = new
            {
                name = shippingAddress?.ContactName ?? string.Empty,
                phone = MaskPhone(shippingAddress?.ContactPhone),
                address = addressText
            },
            items,
            shipTime = shipTime.ToString("yyyy-MM-dd HH:mm:ss"),
            estimatedArrival = estimatedArrival.ToString("yyyy-MM-dd HH:mm:ss")
        }));
    }

    /// <summary>
    /// 接口二：获取物流轨迹
    /// </summary>
    [HttpGet("{orderId}/trace")]
    [Authorize]
    public async Task<IActionResult> GetTrace(string orderId, CancellationToken cancellationToken = default)
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

        var order = await _dbContext.CommodityOrders
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

        if (order.OrderStatusId != 3 && order.OrderStatusId != 4)
        {
            return Ok(ApiResult.Fail("订单尚未发货，无法查询物流", 409));
        }

        var trace = GenerateTrace(order);
        return Ok(ApiResult.Success(trace));
    }

    /// <summary>
    /// 接口四：按运单号查询物流轨迹（管理端）
    /// </summary>
    [HttpPost("track")]
    [Authorize]
    public async Task<IActionResult> Track([FromBody] LogisticsTrackRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PlatformType) || string.IsNullOrWhiteSpace(request.WaybillNo))
        {
            return Ok(ApiResult.Fail("只需要传两个参数：platformType 和 waybillNo", 400));
        }

        var platformType = request.PlatformType.Trim().ToUpperInvariant();
        var waybillNo = request.WaybillNo.Trim();

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

    // ==================== 微信物流接口（运力列表 + 运单轨迹凭证） ====================

    /// <summary>
    /// 获取微信运力列表（快递公司列表）
    /// </summary>
    [HttpGet("delivery-list")]
    public async Task<IActionResult> GetDeliveryList(CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await GetWechatAccessTokenAsync(cancellationToken);
            var url = $"https://api.weixin.qq.com/cgi-bin/express/delivery/open_msg/get_delivery_list?access_token={accessToken}";
            var postContent = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, postContent, cancellationToken);
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            var root = JsonDocument.Parse(result).RootElement;

            if (root.TryGetProperty("errcode", out var errCode) && IsTokenError(errCode.GetInt32()))
            {
                // token 过期，清除缓存重试一次
                _cachedAccessToken = null;
                _accessTokenExpiry = DateTime.MinValue;

                accessToken = await GetWechatAccessTokenAsync(cancellationToken);
                url = $"https://api.weixin.qq.com/cgi-bin/express/delivery/open_msg/get_delivery_list?access_token={accessToken}";
                postContent = new StringContent("{}", Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(url, postContent, cancellationToken);
                result = await response.Content.ReadAsStringAsync(cancellationToken);
                root = JsonDocument.Parse(result).RootElement;
            }

            if (root.TryGetProperty("errcode", out errCode) && errCode.GetInt32() != 0)
            {
                var errMsg = root.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
                return Ok(ApiResult.Fail($"获取运力列表失败：{errMsg}", 502));
            }

            var list = new List<object>();
            if (root.TryGetProperty("delivery_list", out var deliveryList) && deliveryList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in deliveryList.EnumerateArray())
                {
                    list.Add(new
                    {
                        deliveryId = item.TryGetProperty("delivery_id", out var did) ? did.GetString() : "",
                        deliveryName = item.TryGetProperty("delivery_name", out var dname) ? dname.GetString() : ""
                    });
                }
            }

            return Ok(ApiResult.Success(new { list, total = list.Count }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"请求失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取物流查询 token（微信物流详情页跳转凭证）
    /// </summary>
    [HttpPost("waybill-token")]
    [Authorize]
    public async Task<IActionResult> GetWaybillToken([FromBody] WaybillTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.OrderId))
            {
                return Ok(ApiResult.Fail("订单号不能为空", 400));
            }

            // 1. 查订单
            var order = await ResolveCommodityOrderAsync(request.OrderId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            // 2. 检查是否有运单信息
            if (string.IsNullOrWhiteSpace(order.TrackingNumber) || order.TrackingTypeId is null)
            {
                return Ok(ApiResult.Fail("该订单暂无物流信息", 404));
            }

            // 3. 解析快递公司编码
            var (deliveryId, companyName, _) = ResolveCompanyInfo(order.TrackingTypeId);
            var waybillId = order.TrackingNumber;

            // 4. 获取用户 openid
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);
            var openId = request.OpenId ?? user?.WxOpenId ?? string.Empty;

            // 5. 获取收件人手机号（后 4 位即可）
            var shippingAddress = await LoadShippingAddressAsync(order.UserId, order.AddressId, cancellationToken);
            var phone = request.ReceiverPhone ?? shippingAddress?.ContactPhone ?? string.Empty;
            var receiverPhone = phone.Length >= 4 ? phone[^4..] : phone;

            // 6. 微信支付交易号
            var transId = request.TransId ?? order.WxPayNo ?? string.Empty;

            // 7. 获取商品信息
            var goodsList = request.GoodsList;
            if (goodsList is null or { Count: 0 })
            {
                goodsList = await ResolveOrderGoodsAsync(order.OrderId, cancellationToken);
            }

            // 8. 调微信 API 获取 waybill_token
            var body = new Dictionary<string, object>
            {
                ["openid"] = openId,
                ["waybill_id"] = waybillId,
                ["delivery_id"] = deliveryId,
                ["receiver_phone"] = receiverPhone,
                ["trans_id"] = transId
            };

            if (goodsList.Count > 0)
            {
                body["goods_info"] = new
                {
                    detail_list = goodsList.Select(g => new
                    {
                        goods_name = g.GoodsName,
                        goods_img_url = g.GoodsImgUrl
                    }).ToArray()
                };
            }

            var jsonBody = JsonSerializer.Serialize(body);

            // 9. 带 token 过期重试的请求
            var accessToken = await GetWechatAccessTokenAsync(cancellationToken);
            var url = $"https://api.weixin.qq.com/cgi-bin/express/delivery/open_msg/trace_waybill?access_token={accessToken}";

            var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, httpContent, cancellationToken);
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            var root = JsonDocument.Parse(result).RootElement;

            if (root.TryGetProperty("errcode", out var errCode) && IsTokenError(errCode.GetInt32()))
            {
                // token 过期，清除缓存重试一次
                _cachedAccessToken = null;
                _accessTokenExpiry = DateTime.MinValue;

                accessToken = await GetWechatAccessTokenAsync(cancellationToken);
                url = $"https://api.weixin.qq.com/cgi-bin/express/delivery/open_msg/trace_waybill?access_token={accessToken}";
                httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(url, httpContent, cancellationToken);
                result = await response.Content.ReadAsStringAsync(cancellationToken);
                root = JsonDocument.Parse(result).RootElement;
            }

            if (root.TryGetProperty("errcode", out var finalErrCode) && finalErrCode.GetInt32() != 0)
            {
                var errMsg = root.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
                return Ok(ApiResult.Fail($"获取物流 token 失败：{errMsg}", 502));
            }

            var waybillToken = root.TryGetProperty("waybill_token", out var token) ? token.GetString() : null;

            return Ok(ApiResult.Success(new
            {
                waybillToken,
                orderId = order.OrderId,
                orderNumber = order.OrderNo,
                waybillId,
                deliveryId,
                companyName
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"请求失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取微信 access_token（带简单缓存）
    /// </summary>
    private async Task<string> GetWechatAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _accessTokenExpiry)
        {
            return _cachedAccessToken;
        }

        var appId = _configuration["WeChat:AppId"];
        var appSecret = _configuration["WeChat:AppSecret"];

        var url = $"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={appId}&secret={appSecret}";
        var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

        if (response.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() != 0)
        {
            var errMsg = response.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
            throw new InvalidOperationException($"获取 access_token 失败：{errMsg}");
        }

        var newToken = response.GetProperty("access_token").GetString()!;
        var expiresIn = response.GetProperty("expires_in").GetInt32();

        _cachedAccessToken = newToken;
        _accessTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 300); // 提前 5 分钟过期

        return newToken;
    }

    /// <summary>
    /// 判断微信 API 返回的错误码是否为 token 过期
    /// </summary>
    private static bool IsTokenError(int errCode)
    {
        return errCode is 40001 or 40014 or 42001;
    }

    /// <summary>
    /// 根据订单号（数字 ID 或字符串编号）查询商品订单
    /// </summary>
    private async Task<CommodityOrder?> ResolveCommodityOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        if (long.TryParse(orderId, out var id) && id > 0)
        {
            var order = await _dbContext.CommodityOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == id, cancellationToken);
            if (order is not null) return order;
        }

        return await _dbContext.CommodityOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderNo == orderId, cancellationToken);
    }

    /// <summary>
    /// 查询订单关联的商品信息（用于微信物流商品展示）
    /// </summary>
    private async Task<List<WaybillGoodsItem>> ResolveOrderGoodsAsync(long orderId, CancellationToken cancellationToken)
    {
        var details = await (
            from detail in _dbContext.CommodityOrderDetails.AsNoTracking()
            join commodity in _dbContext.Commodities.AsNoTracking()
                on detail.CommodityId equals commodity.CommodityId
            where detail.OrderId == orderId
            select new
            {
                name = commodity.ProductName,
                image = NormalizeMediaUrl(commodity.ImageUrl)
            }
        ).ToListAsync(cancellationToken);

        return details.Select(d => new WaybillGoodsItem
        {
            GoodsName = d.name ?? "商品",
            GoodsImgUrl = d.image ?? string.Empty
        }).ToList();
    }

    /// <summary>
    /// 根据 TrackingTypeId 解析物流公司信息
    /// </summary>
    private static (string Code, string Name, string Phone) ResolveCompanyInfo(long? trackingTypeId)
    {
        var code = trackingTypeId switch
        {
            1 => "SF",
            2 => "EMS",
            3 => "YTO",
            4 => "ZTO",
            5 => "STO",
            6 => "YD",
            _ => "EMS"
        };

        if (CompanyMap.TryGetValue(code, out var info))
        {
            return (code, info.Name, info.Phone);
        }

        return (code, "中国邮政", "11183");
    }

    private string ResolveWaybillNo(string companyCode, CommodityOrder order)
    {
        if (!string.IsNullOrWhiteSpace(order.TrackingNumber))
        {
            return order.TrackingNumber;
        }

        var suffix = (order.OrderId % 10000000000000L).ToString().PadLeft(13, '0');
        return $"{companyCode}{suffix}";
    }

    /// <summary>
    /// 生成模拟物流轨迹，匹配需求文档格式
    /// </summary>
    private List<object> GenerateTrace(CommodityOrder order)
    {
        var shipTime = ComputeShipTime(order);
        var now = DateTime.Now;
        var isCompleted = order.OrderStatusId == 4;
        var trace = new List<object>();

        if (isCompleted)
        {
            trace.Add(new
            {
                time = ClampToNow(shipTime.AddDays(2).AddHours(10), now).ToString("yyyy-MM-dd HH:mm:ss"),
                desc = "快件已签收，感谢使用",
                location = "广州市",
                status = "delivered"
            });
        }

        trace.Add(new
        {
            time = ClampToNow(shipTime.AddDays(2).AddHours(8), now).ToString("yyyy-MM-dd HH:mm:ss"),
            desc = isCompleted ? "快件已到达【广州转运中心】" : "快递员正在派送中",
            location = "广州市",
            status = isCompleted ? "arrived" : "delivering"
        });

        trace.Add(new
        {
            time = ClampToNow(shipTime.AddDays(1).AddHours(18), now).ToString("yyyy-MM-dd HH:mm:ss"),
            desc = isCompleted ? "快件已从【广州转运中心】发出" : "快件已到达【广州转运中心】",
            location = "广州市",
            status = isCompleted ? "departed" : "arrived"
        });

        trace.Add(new
        {
            time = ClampToNow(shipTime.AddDays(1).AddHours(6), now).ToString("yyyy-MM-dd HH:mm:ss"),
            desc = "快件已从【深圳集散中心】发出",
            location = "深圳市",
            status = "departed"
        });

        trace.Add(new
        {
            time = ClampToNow(shipTime.AddHours(16), now).ToString("yyyy-MM-dd HH:mm:ss"),
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

        // 按时间倒序排列（最新在前）
        return trace.OrderByDescending(e => GetTimeValue(e)).ToList();
    }

    private static DateTime GetTimeValue(object obj)
    {
        var prop = obj.GetType().GetProperty("time")?.GetValue(obj) as string;
        return DateTime.TryParse(prop, out var t) ? t : DateTime.MinValue;
    }

    private int ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? Request.Headers["X-User-Id"].FirstOrDefault();

        return int.TryParse(userIdValue, out var userId) && userId > 0 ? userId : 0;
    }

    private async Task<ShippingAddress?> LoadShippingAddressAsync(int userId, long addressId, CancellationToken cancellationToken)
    {
        if (addressId <= 0) return null;

        var query = _dbContext.ShippingAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var matchedAddress = await query
            .FirstOrDefaultAsync(x => x.AddressId == addressId, cancellationToken);

        if (matchedAddress is not null)
        {
            return matchedAddress;
        }

        return await query
            .OrderByDescending(x => EF.Property<bool>(x, DefaultFlagProperty))
            .ThenByDescending(x => x.AddressId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DateTime ComputeShipTime(CommodityOrder order)
    {
        return order.CreateTime.AddHours(4);
    }

    private static DateTime ClampToNow(DateTime value, DateTime now)
    {
        return value > now ? now : value;
    }

    // ==================== 顺丰/邮政轨迹查询（接口四） ====================

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

    /// <summary>
    /// 微信物流查询 token 请求参数
    /// </summary>
    public sealed class WaybillTokenRequest
    {
        /// <summary>订单号（数字 ID 或字符串编号，如 GOODS2026...）</summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>用户 openid（可选，不传则自动从用户表获取）</summary>
        public string? OpenId { get; set; }

        /// <summary>收件人手机号（可选，不传则自动从收货地址获取）</summary>
        public string? ReceiverPhone { get; set; }

        /// <summary>微信支付交易单号（可选，不传则自动从订单获取）</summary>
        public string? TransId { get; set; }

        /// <summary>商品列表（可选，不传则自动从订单详情获取）</summary>
        public List<WaybillGoodsItem>? GoodsList { get; set; }
    }

    /// <summary>
    /// 微信物流商品信息
    /// </summary>
    public sealed class WaybillGoodsItem
    {
        /// <summary>商品名称</summary>
        public string GoodsName { get; set; } = string.Empty;

        /// <summary>商品图片 URL</summary>
        public string GoodsImgUrl { get; set; } = string.Empty;
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
            return trimmed
                .Replace("http://192.168.101.47", "http://127.0.0.1:5000", StringComparison.OrdinalIgnoreCase)
                .Replace("http://192.168.203.56", "http://127.0.0.1:5000", StringComparison.OrdinalIgnoreCase);
        }

        var normalizedName = trimmed.TrimStart('/');
        var ext = Path.GetExtension(trimmed).ToLowerInvariant();
        const string baseUrl = "http://127.0.0.1:5000";

        if (ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv")
        {
            return $"{baseUrl}/api/file/video/{normalizedName}";
        }

        return $"{baseUrl}/api/file/image/{normalizedName}";
    }
}
