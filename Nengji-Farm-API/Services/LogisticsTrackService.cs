using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.Extensions.Options;

using WebAPI.Options;

namespace WebAPI.Services;

public sealed class LogisticsTrackService : ILogisticsTrackService
{
    private const string SfRouteServiceCode = "EXP_RECE_SEARCH_ROUTES";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LogisticsTrackService> _logger;
    private readonly LogisticsOptions _options;

    public LogisticsTrackService(
        HttpClient httpClient,
        IOptions<LogisticsOptions> options,
        ILogger<LogisticsTrackService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<LogisticsTrackResult> QueryAsync(LogisticsTrackQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedCompanyType = NormalizeCompanyType(query.CompanyType);
        if (string.IsNullOrWhiteSpace(normalizedCompanyType))
        {
            throw new InvalidOperationException("物流类型不能为空。");
        }

        if (string.IsNullOrWhiteSpace(query.TrackingNumber))
        {
            throw new InvalidOperationException("物流单号不能为空。");
        }

        query.Top = query.Top <= 0 ? 2000 : Math.Min(query.Top, 2000);

        return normalizedCompanyType switch
        {
            "sf" => await QuerySfAsync(query, cancellationToken),
            "jd" => await QueryJdAsync(query, cancellationToken),
            _ => throw new InvalidOperationException($"暂不支持物流类型：{query.CompanyType}")
        };
    }

    private async Task<LogisticsTrackResult> QuerySfAsync(LogisticsTrackQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SF.PartnerId) || string.IsNullOrWhiteSpace(_options.SF.CheckWord))
        {
            throw new InvalidOperationException("顺丰物流配置不完整，请先在 appsettings.json 中补充 Logistics:SF 配置。");
        }

        var trackingType = query.TrackingType is 1 or 2
            ? query.TrackingType.Value
            : _options.SF.DefaultTrackingType is 1 or 2 ? _options.SF.DefaultTrackingType : 1;

        var msgData = JsonSerializer.Serialize(new
        {
            language = _options.SF.Language,
            trackingType,
            trackingNumber = new[] { query.TrackingNumber.Trim() },
            methodType = 1,
            checkPhoneNo = string.IsNullOrWhiteSpace(query.PhoneNo) ? null : query.PhoneNo.Trim()
        }, JsonOptions);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var form = new Dictionary<string, string>
        {
            ["partnerID"] = _options.SF.PartnerId.Trim(),
            ["requestID"] = Guid.NewGuid().ToString("N"),
            ["serviceCode"] = SfRouteServiceCode,
            ["timestamp"] = timestamp,
            ["msgData"] = msgData,
            ["msgDigest"] = CreateSfDigest(msgData, timestamp, _options.SF.CheckWord.Trim())
        };

        if (!string.IsNullOrWhiteSpace(_options.SF.FromCode))
        {
            form["fromCode"] = _options.SF.FromCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_options.SF.VersionNo))
        {
            form["versionNo"] = _options.SF.VersionNo.Trim();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.SF.ServiceUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SF logistics query failed with HTTP {StatusCode}: {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException($"顺丰物流查询失败，HTTP {(int)response.StatusCode}。");
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        var apiResultCode = root.TryGetProperty("apiResultCode", out var apiResultCodeNode)
            ? apiResultCodeNode.GetString()
            : null;
        if (!string.Equals(apiResultCode, "A1000", StringComparison.OrdinalIgnoreCase))
        {
            var apiErrorMsg = root.TryGetProperty("apiErrorMsg", out var apiErrorMsgNode)
                ? apiErrorMsgNode.GetString()
                : null;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(apiErrorMsg)
                ? "顺丰物流平台返回失败。"
                : $"顺丰物流平台返回失败：{apiErrorMsg}");
        }

        var apiResultDataText = root.TryGetProperty("apiResultData", out var apiResultDataNode)
            ? apiResultDataNode.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(apiResultDataText))
        {
            throw new InvalidOperationException("顺丰物流接口未返回轨迹数据。");
        }

        using var apiResultDataDocument = JsonDocument.Parse(apiResultDataText);
        var apiResultDataRoot = apiResultDataDocument.RootElement;

        var success = apiResultDataRoot.TryGetProperty("success", out var successNode) && successNode.ValueKind == JsonValueKind.True;
        var errorCode = apiResultDataRoot.TryGetProperty("errorCode", out var errorCodeNode) ? errorCodeNode.GetString() : null;
        var errorMsg = apiResultDataRoot.TryGetProperty("errorMsg", out var errorMsgNode) ? errorMsgNode.GetString() : null;
        if (!success || !string.Equals(errorCode, "S0000", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorMsg)
                ? "顺丰物流查询失败。"
                : $"顺丰物流查询失败：{errorMsg}");
        }

        if (!apiResultDataRoot.TryGetProperty("msgData", out var msgDataNode) ||
            !msgDataNode.TryGetProperty("routeResps", out var routeRespsNode) ||
            routeRespsNode.ValueKind != JsonValueKind.Array ||
            routeRespsNode.GetArrayLength() == 0)
        {
            return BuildResult("sf", "顺丰", query.TrackingNumber.Trim(), query.OrderNumber, []);
        }

        var routeResp = routeRespsNode[0];
        var orderNumber = routeResp.TryGetProperty("orderId", out var orderIdNode)
            ? orderIdNode.GetString()
            : query.OrderNumber;

        var list = new List<LogisticsTrackNode>();
        if (routeResp.TryGetProperty("routes", out var routesNode) && routesNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var routeNode in routesNode.EnumerateArray())
            {
                list.Add(new LogisticsTrackNode
                {
                    Remark = ReadString(routeNode, "remark"),
                    OperationTime = ReadString(routeNode, "acceptTime"),
                    RouteAddress = ReadString(routeNode, "acceptAddress")
                });
            }
        }

        return BuildResult("sf", "顺丰", query.TrackingNumber.Trim(), orderNumber, list, query.Top);
    }

    private async Task<LogisticsTrackResult> QueryJdAsync(LogisticsTrackQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.JD.ServiceUrl))
        {
            throw new InvalidOperationException("京东物流查询地址未配置，请先在 appsettings.json 中补充 Logistics:JD:ServiceUrl。");
        }

        var payload = new
        {
            trackingNumber = query.TrackingNumber.Trim(),
            orderNo = query.OrderNumber,
            phoneNo = query.PhoneNo,
            customerCode = _options.JD.CustomerCode
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.JD.ServiceUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.JD.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.JD.AccessToken.Trim());
        }

        if (!string.IsNullOrWhiteSpace(_options.JD.AppKey))
        {
            request.Headers.TryAddWithoutValidation("app-key", _options.JD.AppKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(_options.JD.AppSecret))
        {
            request.Headers.TryAddWithoutValidation("app-secret", _options.JD.AppSecret.Trim());
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("JD logistics query failed with HTTP {StatusCode}: {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException($"京东物流查询失败，HTTP {(int)response.StatusCode}。");
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        var routeArray = TryResolveRouteArray(root);
        var orderNumber = TryReadFirstNonEmptyString(root,
            "orderNo",
            "orderNumber",
            "waybillCode",
            "waybillNo") ?? query.OrderNumber;

        var list = new List<LogisticsTrackNode>();
        if (routeArray.HasValue)
        {
            foreach (var routeNode in routeArray.Value.EnumerateArray())
            {
                list.Add(new LogisticsTrackNode
                {
                    Remark = TryReadFirstNonEmptyString(routeNode, "remark", "desc", "content"),
                    OperationTime = TryReadFirstNonEmptyString(routeNode, "operationTime", "acceptTime", "time"),
                    RouteAddress = TryReadFirstNonEmptyString(routeNode, "routeAddress", "acceptAddress", "address")
                });
            }
        }

        return BuildResult("jd", "京东物流", query.TrackingNumber.Trim(), orderNumber, list, query.Top);
    }

    private static LogisticsTrackResult BuildResult(
        string companyType,
        string companyName,
        string trackingNumber,
        string? orderNumber,
        IEnumerable<LogisticsTrackNode> source,
        int top = 2000)
    {
        var list = source
            .Where(x => !string.IsNullOrWhiteSpace(x.Remark) ||
                        !string.IsNullOrWhiteSpace(x.OperationTime) ||
                        !string.IsNullOrWhiteSpace(x.RouteAddress))
            .OrderByDescending(x => ParseDateTime(x.OperationTime))
            .ThenByDescending(x => x.OperationTime, StringComparer.Ordinal)
            .Take(top <= 0 ? 2000 : Math.Min(top, 2000))
            .ToList();

        var current = list.FirstOrDefault();

        return new LogisticsTrackResult
        {
            CompanyType = companyType,
            CompanyName = companyName,
            TrackingNumber = trackingNumber,
            OrderNumber = orderNumber,
            CurrentStatus = current?.Remark ?? string.Empty,
            CurrentRemark = current?.Remark,
            CurrentTime = current?.OperationTime,
            CurrentAddress = current?.RouteAddress,
            Total = list.Count,
            DataList = list
        };
    }

    private static string NormalizeCompanyType(string? companyType)
    {
        var value = companyType?.Trim().ToLowerInvariant();
        return value switch
        {
            "sf" or "shunfeng" or "shun-feng" or "顺丰" => "sf",
            "jd" or "jdl" or "jdwl" or "jingdong" or "京东" or "京东物流" => "jd",
            _ => value ?? string.Empty
        };
    }

    private static string CreateSfDigest(string msgData, string timestamp, string checkWord)
    {
        // 顺丰标准接入通常使用 Base64(MD5(msgData + timestamp + checkWord))。
        var raw = $"{msgData}{timestamp}{checkWord}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = MD5.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static DateTime ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : DateTime.MinValue;
    }

    private static string? ReadString(JsonElement node, string propertyName)
    {
        return node.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : null;
    }

    private static JsonElement? TryResolveRouteArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("data", out var dataNode))
        {
            if (dataNode.ValueKind == JsonValueKind.Array)
            {
                return dataNode;
            }

            if (dataNode.ValueKind == JsonValueKind.Object)
            {
                if (dataNode.TryGetProperty("data", out var innerDataNode) && innerDataNode.ValueKind == JsonValueKind.Array)
                {
                    return innerDataNode;
                }

                if (dataNode.TryGetProperty("routes", out var routesNode) && routesNode.ValueKind == JsonValueKind.Array)
                {
                    return routesNode;
                }

                if (dataNode.TryGetProperty("traceList", out var traceListNode) && traceListNode.ValueKind == JsonValueKind.Array)
                {
                    return traceListNode;
                }
            }
        }

        if (root.TryGetProperty("routes", out var rootRoutesNode) && rootRoutesNode.ValueKind == JsonValueKind.Array)
        {
            return rootRoutesNode;
        }

        if (root.TryGetProperty("traceList", out var rootTraceListNode) && rootTraceListNode.ValueKind == JsonValueKind.Array)
        {
            return rootTraceListNode;
        }

        return null;
    }

    private static string? TryReadFirstNonEmptyString(JsonElement node, params string[] propertyNames)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (node.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null)
            {
                var value = property.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
