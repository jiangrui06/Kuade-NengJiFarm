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
        ["SF"] = ("顺丰速运", "95338"),
        ["EMS"] = ("邮政快递", "11183"),
        ["YTO"] = ("圆通速递", "95554"),
        ["ZTO"] = ("中通快递", "95311"),
        ["STO"] = ("申通快递", "95543"),
        ["YD"] = ("韵达快递", "95546"),
        ["JD"] = ("京东快递", "950616"),
    };

    /// <summary>
    /// 快递单号前缀 → (delivery_id, 名称) 映射，用于自动识别快递公司
    /// </summary>
    private static readonly (string Prefix, string Code)[] ExpressPrefixMap =
    {
        ("JDX", "JD"),   // 京东快递
        ("JD", "JD"),    // 京东快递
        ("SF", "SF"),    // 顺丰速运
        ("EMS", "EMS"),  // 邮政EMS
        ("YT", "YTO"),   // 圆通速递
        ("ZTO", "ZTO"),  // 中通快递
        ("ZT", "ZTO"),   // 中通快递
        ("STO", "STO"),  // 申通快递
        ("ST", "STO"),   // 申通快递
        ("YD", "YD"),    // 韵达快递
        ("YUNDA", "YD"), // 韵达快递
        ("FAST", "FAST"),// 快捷快递
        ("UC", "UC"),    // 优速快递
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

        var (companyCode, companyName, companyPhone) = ResolveCompanyInfo(order.TrackingTypeId, order.TrackingNumber);
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
    /// 优先使用前端传入的参数直接调微信 API；兜底走订单查询补充缺失字段
    /// </summary>
    [HttpPost("waybill-token")]
    public async Task<IActionResult> GetWaybillToken(
        [FromBody] WaybillTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ===== 参数准备：优先前端传入，缺失则从订单查询兜底 =====
            var waybillId = request.WaybillId ?? string.Empty;
            var deliveryId = request.DeliveryId ?? string.Empty;
            var openId = request.OpenId ?? string.Empty;
            var receiverPhone = request.ReceiverPhone ?? string.Empty;
            var transId = request.TransId ?? string.Empty;
            var goodsList = request.GoodsList;

            // 如果前端只传了 waybillId（微信插件回调模式），查订单补全
            if (!string.IsNullOrWhiteSpace(waybillId) &&
                (string.IsNullOrWhiteSpace(deliveryId) || string.IsNullOrWhiteSpace(receiverPhone)))
            {
                var order = await _dbContext.CommodityOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TrackingNumber == waybillId, cancellationToken);

                if (order is not null)
                {
                    if (string.IsNullOrWhiteSpace(deliveryId))
                        deliveryId = ResolveCompanyInfo(order.TrackingTypeId, order.TrackingNumber).Code;

                    if (string.IsNullOrWhiteSpace(openId))
                    {
                        var user = await _dbContext.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);
                        openId = user?.WxOpenId ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(receiverPhone))
                        receiverPhone = order.ReceiverPhone ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(transId))
                        transId = order.WxPayNo ?? string.Empty;

                    if (goodsList is null or { Count: 0 })
                        goodsList = await ResolveOrderGoodsAsync(order.OrderId, cancellationToken);
                }
            }

            // ===== 参数校验 =====
            if (string.IsNullOrWhiteSpace(waybillId))
                return Ok(ApiResult.Fail("运单号不能为空", 400));
            if (string.IsNullOrWhiteSpace(deliveryId))
                return Ok(ApiResult.Fail("快递公司编码不能为空", 400));
            if (string.IsNullOrWhiteSpace(openId))
                return Ok(ApiResult.Fail("用户 openId 不能为空", 400));
            if (string.IsNullOrWhiteSpace(receiverPhone))
                return Ok(ApiResult.Fail("收件人手机号不能为空", 400));

            // ===== 手机号清洗：只保留数字 + 去除 86 区号 =====
            var phoneDigits = NormalizePhone(receiverPhone);
            if (phoneDigits.Length != 11 || !phoneDigits.StartsWith("1"))
                return Ok(ApiResult.Fail("收件人手机号格式不正确", 400));

            Console.WriteLine($"[GetWaybillToken] waybillId={waybillId}, deliveryId={deliveryId}, phone={phoneDigits}");

            // ===== 调微信 API 获取 waybill_token =====
            var body = new Dictionary<string, object>
            {
                ["openid"] = openId,
                ["waybill_id"] = waybillId,
                ["delivery_id"] = deliveryId,
                ["receiver_phone"] = phoneDigits,          // 先传完整号
                ["trans_id"] = transId
            };

            if (goodsList is { Count: > 0 })
            {
                body["goods_info"] = new
                {
                    detail_list = goodsList.Select(g =>
                    {
                        var item = new Dictionary<string, object>
                        {
                            ["goods_name"] = g.GoodsName
                        };
                        if (!string.IsNullOrWhiteSpace(g.GoodsImgUrl))
                            item["goods_img_url"] = g.GoodsImgUrl;
                        return item;
                    }).ToArray()
                };
            }

            // 请求微信 API（自动重试：token 过期 或 手机号不匹配时用后 4 位重试）
            var (success, waybillToken, errorMsg) = await CallWechatWaybillApiAsync(
                body, phoneDigits, cancellationToken);

            if (!success)
                return Ok(ApiResult.Fail($"获取物流 token 失败：{errorMsg}", 502));

            return Ok(ApiResult.Success(new
            {
                waybillToken,
                waybillId,
                deliveryId
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"请求失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 调用微信物流轨迹 API，带 token 过期重试和手机号不匹配重试
    /// </summary>
    private async Task<(bool Success, string? WaybillToken, string? ErrorMsg)> CallWechatWaybillApiAsync(
        Dictionary<string, object> body, string fullPhone, CancellationToken ct)
    {
        var accessToken = await GetWechatAccessTokenAsync(ct);
        var url = $"https://api.weixin.qq.com/cgi-bin/express/delivery/open_msg/trace_waybill?access_token={accessToken}";

        // 第 1 次：用完整手机号（参考 API 直传模式）
        body["receiver_phone"] = fullPhone;
        var (result, errCode, errMsg) = await PostWechatAsync(url, body, ct);

        if (IsTokenError(errCode))
            (result, errCode, errMsg) = await RetryWithNewTokenAsync(url, body, ct);

        // 手机号不匹配 → 用后 4 位重试（部分快递公司只需后 4 位）
        if (errCode == 1002 && fullPhone.Length >= 4)
        {
            Console.WriteLine($"[GetWaybillToken] phone mismatch with full, retrying with last4");
            body["receiver_phone"] = fullPhone[^4..];
            (result, errCode, errMsg) = await PostWechatAsync(url, body, ct);

            if (IsTokenError(errCode))
                (result, errCode, errMsg) = await RetryWithNewTokenAsync(url, body, ct);
        }

        if (errCode != 0)
            return (false, null, errMsg);

        var waybillToken = result.RootElement.TryGetProperty("waybill_token", out var token) ? token.GetString() : null;
        return (true, waybillToken, null);
    }

    private async Task<(JsonDocument Response, int ErrCode, string ErrMsg)> PostWechatAsync(
        string url, Dictionary<string, object> body, CancellationToken ct)
    {
        var jsonBody = JsonSerializer.Serialize(body);
        var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, httpContent, ct);
        var result = await response.Content.ReadAsStringAsync(ct);
        Console.WriteLine($"[GetWaybillToken] WeChat response: {result}");
        var root = JsonDocument.Parse(result);

        var errCode = root.RootElement.TryGetProperty("errcode", out var ec) ? ec.GetInt32() : -1;
        var errMsg = root.RootElement.TryGetProperty("errmsg", out var msg) ? msg.GetString() : null;
        return (root, errCode, errMsg ?? "未知错误");
    }

    private async Task<(JsonDocument Response, int ErrCode, string ErrMsg)> RetryWithNewTokenAsync(
        string url, Dictionary<string, object> body, CancellationToken ct)
    {
        Console.WriteLine($"[GetWaybillToken] token expired, refreshing and retrying");
        _cachedAccessToken = null;
        _accessTokenExpiry = DateTime.MinValue;
        var newToken = await GetWechatAccessTokenAsync(ct);
        var newUrl = $"https://api.weixin.qq.com/cgi-bin/express/delivery/open_msg/trace_waybill?access_token={newToken}";
        return await PostWechatAsync(newUrl, body, ct);
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
                image = commodity.ImageUrl
            }
        ).ToListAsync(cancellationToken);

        return details.Select(d =>
        {
            var rawUrl = d.image?.Trim().Trim('`', '"', '\'') ?? string.Empty;

            // 只传递公开可访问的 HTTP(S) 图片 URL，本地地址/相对路径不传给微信
            var isPublicUrl = !string.IsNullOrWhiteSpace(rawUrl)
                && (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                && !rawUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                && !rawUrl.Contains("192.168.", StringComparison.OrdinalIgnoreCase)
                && !rawUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);

            return new WaybillGoodsItem
            {
                GoodsName = d.name ?? "商品",
                GoodsImgUrl = isPublicUrl ? rawUrl : string.Empty
            };
        }).ToList();
    }

    /// <summary>
    /// 根据 TrackingTypeId 和运单号解析物流公司信息
    /// 优先从运单号前缀自动识别，再回退到 trackingTypeId 映射
    /// </summary>
    private static (string Code, string Name, string Phone) ResolveCompanyInfo(long? trackingTypeId, string? trackingNumber = null)
    {
        // 优先从单号前缀自动识别快递公司
        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            var upper = trackingNumber.Trim().ToUpperInvariant();
            foreach (var (prefix, matchedCode) in ExpressPrefixMap)
            {
                if (!upper.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                if (CompanyMap.TryGetValue(matchedCode, out var companyInfo))
                {
                    return (matchedCode, companyInfo.Name, companyInfo.Phone);
                }
                return (matchedCode, matchedCode switch
                {
                    "JD" => "京东快递",
                    "FAST" => "快捷快递",
                    "UC" => "优速快递",
                    _ => "其他快递"
                }, "400-888-8888");
            }
        }

        // 回退：从 trackingTypeId 映射
        var fallbackCode = trackingTypeId switch
        {
            1 => "SF",
            2 => "EMS",
            3 => "YTO",
            4 => "ZTO",
            5 => "STO",
            6 => "YD",
            7 => "JD",
            _ => "EMS"
        };

        if (CompanyMap.TryGetValue(fallbackCode, out var fallbackInfo))
        {
            return (fallbackCode, fallbackInfo.Name, fallbackInfo.Phone);
        }

        return (fallbackCode, "中国邮政", "11183");
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

        /// <summary>运单号（可选，不传则自动从订单获取）</summary>
        public string? WaybillId { get; set; }

        /// <summary>快递公司编码（可选，不传则自动从运单号前缀识别）</summary>
        public string? DeliveryId { get; set; }

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

    private static string NormalizePhone(string phone)
    {
        // 只保留数字，过滤所有非数字字符（全角括号、空格、+86、- 等全部清除）
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // 处理中国大陆区号 86 前缀
        if (digits.Length == 13 && digits.StartsWith("86"))
            digits = digits[2..];
        else if (digits.Length == 12 && digits.StartsWith("86"))
            digits = digits[2..];

        return digits;
    }

    private static bool IsValidChinesePhone(string phone)
    {
        return phone.Length == 11 && phone.All(char.IsDigit) && phone.StartsWith("1");
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
