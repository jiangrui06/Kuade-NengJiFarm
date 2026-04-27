using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Extensions.Options;

using WebAPI.Options;

namespace WebAPI.Services;

public interface IWeChatPayService
{
    Task<WeChatCreatePaymentResult> CreateJsApiPaymentAsync(
        WeChatCreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<WeChatOrderQueryResult> QueryPaymentStatusAsync(
        string outTradeNo,
        CancellationToken cancellationToken = default);

    Task<WeChatPaymentNotifyResult> ProcessPaymentNotificationAsync(
        string requestBody,
        CancellationToken cancellationToken = default);
}

public sealed class WeChatPayService : IWeChatPayService
{
    private const string BaseUrl = "https://api.mch.weixin.qq.com";
    private const string UnifiedOrderPath = "/pay/unifiedorder";
    private const string OrderQueryPath = "/pay/orderquery";
    private const string DefaultSignType = "HMAC-SHA256";

    private readonly HttpClient _httpClient;
    private readonly WeChatPayOptions _options;

    public WeChatPayService(HttpClient httpClient, IOptions<WeChatPayOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<WeChatCreatePaymentResult> CreateJsApiPaymentAsync(
        WeChatCreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBaseConfiguration(requireNotifyUrl: false);

        if (request.TotalFeeFen <= 0)
        {
            throw new InvalidOperationException("WeChat Pay amount must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(request.OpenId))
        {
            throw new InvalidOperationException("WeChat openid is required for JSAPI payment.");
        }

        if (string.IsNullOrWhiteSpace(request.OutTradeNo))
        {
            throw new InvalidOperationException("Merchant order number is required.");
        }

        var notifyUrl = ResolveNotifyUrl(request.NotifyUrl);
        var signType = ResolveSignType();
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["appid"] = _options.AppId.Trim(),
            ["mch_id"] = _options.MchId.Trim(),
            ["nonce_str"] = CreateNonce(),
            ["sign_type"] = signType,
            ["body"] = NormalizeBody(request.Description),
            ["out_trade_no"] = request.OutTradeNo.Trim(),
            ["total_fee"] = request.TotalFeeFen.ToString(CultureInfo.InvariantCulture),
            ["spbill_create_ip"] = NormalizeClientIp(request.ClientIp),
            ["notify_url"] = notifyUrl,
            ["trade_type"] = "JSAPI",
            ["openid"] = request.OpenId.Trim()
        };

        if (!string.IsNullOrWhiteSpace(request.Attach))
        {
            values["attach"] = request.Attach.Trim();
        }

        values["sign"] = CreateSign(values, signType);

        var responseText = await PostXmlAsync(UnifiedOrderPath, BuildXml(values), cancellationToken);
        var response = ParseXml(responseText);
        EnsureWeChatResultSuccess(response, "unifiedorder");
        EnsureValidSign(response);

        var prepayId = GetValue(response, "prepay_id");
        if (string.IsNullOrWhiteSpace(prepayId))
        {
            throw new InvalidOperationException("WeChat Pay did not return prepay_id.");
        }

        var clientNonce = CreateNonce();
        var timeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var packageValue = $"prepay_id={prepayId}";
        var clientPayValues = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["appId"] = _options.AppId.Trim(),
            ["timeStamp"] = timeStamp,
            ["nonceStr"] = clientNonce,
            ["package"] = packageValue,
            ["signType"] = signType
        };

        return new WeChatCreatePaymentResult
        {
            AppId = _options.AppId.Trim(),
            TimeStamp = timeStamp,
            NonceStr = clientNonce,
            Package = packageValue,
            SignType = signType,
            PaySign = CreateSign(clientPayValues, signType),
            PrepayId = prepayId,
            OutTradeNo = request.OutTradeNo
        };
    }

    public async Task<WeChatOrderQueryResult> QueryPaymentStatusAsync(
        string outTradeNo,
        CancellationToken cancellationToken = default)
    {
        ValidateBaseConfiguration(requireNotifyUrl: false);

        if (string.IsNullOrWhiteSpace(outTradeNo))
        {
            throw new InvalidOperationException("Merchant order number is required.");
        }

        var signType = ResolveSignType();
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["appid"] = _options.AppId.Trim(),
            ["mch_id"] = _options.MchId.Trim(),
            ["out_trade_no"] = outTradeNo.Trim(),
            ["nonce_str"] = CreateNonce(),
            ["sign_type"] = signType
        };
        values["sign"] = CreateSign(values, signType);

        var responseText = await PostXmlAsync(OrderQueryPath, BuildXml(values), cancellationToken);
        var response = ParseXml(responseText);
        EnsureWeChatResultSuccess(response, "orderquery");
        EnsureValidSign(response);

        var tradeState = GetValue(response, "trade_state");
        return new WeChatOrderQueryResult
        {
            IsSuccess = string.Equals(tradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase),
            OutTradeNo = GetValue(response, "out_trade_no") ?? outTradeNo,
            TransactionId = GetValue(response, "transaction_id") ?? string.Empty,
            TradeState = tradeState ?? string.Empty,
            TradeStateDesc = GetValue(response, "trade_state_desc") ?? string.Empty,
            TotalFeeFen = ParseInt(GetValue(response, "total_fee"))
        };
    }

    public Task<WeChatPaymentNotifyResult> ProcessPaymentNotificationAsync(
        string requestBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateBaseConfiguration(requireNotifyUrl: false);

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new InvalidOperationException("WeChat Pay notification body is empty.");
        }

        var values = ParseXml(requestBody);
        EnsureValidSign(values);

        var returnCode = GetValue(values, "return_code");
        var resultCode = GetValue(values, "result_code");
        var tradeState = string.Equals(returnCode, "SUCCESS", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(resultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                ? "SUCCESS"
                : "FAILED";

        return Task.FromResult(new WeChatPaymentNotifyResult
        {
            IsSuccess = string.Equals(tradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase),
            OutTradeNo = GetValue(values, "out_trade_no") ?? string.Empty,
            TransactionId = GetValue(values, "transaction_id") ?? string.Empty,
            TradeState = tradeState,
            TotalFeeFen = ParseInt(GetValue(values, "total_fee"))
        });
    }

    private async Task<string> PostXmlAsync(
        string requestPath,
        string xml,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(xml, Encoding.UTF8, "text/xml");
        using var response = await _httpClient.PostAsync($"{BaseUrl}{requestPath}", content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"WeChat Pay request failed ({(int)response.StatusCode}): {responseText}");
        }

        return responseText;
    }

    private string CreateSign(IReadOnlyDictionary<string, string> values, string signType)
    {
        var stringA = string.Join("&", values
            .Where(x => !string.Equals(x.Key, "sign", StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}"));

        var stringSignTemp = $"{stringA}&key={_options.ApiV2Key.Trim()}";
        var data = Encoding.UTF8.GetBytes(stringSignTemp);

        if (string.Equals(signType, "MD5", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToHexString(MD5.HashData(data)).ToUpperInvariant();
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ApiV2Key.Trim()));
        return Convert.ToHexString(hmac.ComputeHash(data)).ToUpperInvariant();
    }

    private void EnsureValidSign(IReadOnlyDictionary<string, string> values)
    {
        var sign = GetValue(values, "sign");
        if (string.IsNullOrWhiteSpace(sign))
        {
            throw new InvalidOperationException("WeChat Pay response is missing sign.");
        }

        var signType = GetValue(values, "sign_type") ?? ResolveSignType();
        var expectedSign = CreateSign(values, signType);
        if (!string.Equals(sign, expectedSign, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("WeChat Pay signature verification failed.");
        }
    }

    private static string BuildXml(IReadOnlyDictionary<string, string> values)
    {
        var xml = new XElement("xml",
            values.Select(x => new XElement(x.Key, new XCData(x.Value))));
        return xml.ToString(SaveOptions.DisableFormatting);
    }

    private static Dictionary<string, string> ParseXml(string xml)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(reader);
            var root = document.Root ?? throw new InvalidOperationException("XML root is missing.");

            return root.Elements()
                .GroupBy(x => x.Name.LocalName, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new InvalidOperationException("WeChat Pay XML could not be parsed.", ex);
        }
    }

    private void EnsureWeChatResultSuccess(
        IReadOnlyDictionary<string, string> values,
        string operation)
    {
        var returnCode = GetValue(values, "return_code");
        if (!string.Equals(returnCode, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"WeChat Pay {operation} failed: {GetValue(values, "return_msg") ?? "unknown error"}");
        }

        var resultCode = GetValue(values, "result_code");
        if (!string.Equals(resultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            var code = GetValue(values, "err_code") ?? "UNKNOWN";
            var description = GetValue(values, "err_code_des") ?? GetValue(values, "return_msg") ?? "unknown error";
            throw new InvalidOperationException($"WeChat Pay {operation} failed: {code} - {description}");
        }
    }

    private string ResolveNotifyUrl(string? requestNotifyUrl)
    {
        if (IsUsableNotifyUrl(_options.NotifyUrl))
        {
            return _options.NotifyUrl.Trim();
        }

        if (IsUsableNotifyUrl(requestNotifyUrl))
        {
            return requestNotifyUrl!.Trim();
        }

        throw new InvalidOperationException("WeChat:NotifyUrl must be a public HTTPS URL, for example https://domain.com/api/pay/notify.");
    }

    private static bool IsUsableNotifyUrl(string? notifyUrl)
    {
        if (string.IsNullOrWhiteSpace(notifyUrl))
        {
            return false;
        }

        var value = notifyUrl.Trim();
        if (value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("your-domain", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("example.com", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("浣犵", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("你的", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            !string.IsNullOrWhiteSpace(uri.Host);
    }

    private void ValidateBaseConfiguration(bool requireNotifyUrl = true)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.AppId))
        {
            missing.Add("WeChat:AppId");
        }

        if (string.IsNullOrWhiteSpace(_options.MchId) || IsPlaceholder(_options.MchId))
        {
            missing.Add("WeChat:MchId");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiV2Key) || IsPlaceholder(_options.ApiV2Key))
        {
            missing.Add("WeChat:ApiV2Key");
        }

        if (requireNotifyUrl && !IsUsableNotifyUrl(_options.NotifyUrl))
        {
            missing.Add("WeChat:NotifyUrl");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"WeChat Pay configuration is incomplete: {string.Join(", ", missing)}.");
        }
    }

    private static bool IsPlaceholder(string value)
    {
        return value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("your-", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("浣犵", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("你的", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveSignType()
    {
        return string.Equals(_options.SignType, "MD5", StringComparison.OrdinalIgnoreCase)
            ? "MD5"
            : DefaultSignType;
    }

    private static string NormalizeBody(string description)
    {
        var body = string.IsNullOrWhiteSpace(description) ? "NengJi Farm Order" : description.Trim();
        return body.Length <= 127 ? body : body[..127];
    }

    private static string NormalizeClientIp(string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            return "127.0.0.1";
        }

        var value = clientIp.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? clientIp;

        return value.Contains(':', StringComparison.Ordinal) ? "127.0.0.1" : value;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static string CreateNonce()
    {
        return Guid.NewGuid().ToString("N");
    }
}

public sealed class WeChatCreatePaymentRequest
{
    public string Description { get; set; } = string.Empty;

    public string OutTradeNo { get; set; } = string.Empty;

    public int TotalFeeFen { get; set; }

    public string OpenId { get; set; } = string.Empty;

    public string? Attach { get; set; }

    public string? ClientIp { get; set; }

    public string? NotifyUrl { get; set; }
}

public sealed class WeChatCreatePaymentResult
{
    public string AppId { get; set; } = string.Empty;

    public string TimeStamp { get; set; } = string.Empty;

    public string NonceStr { get; set; } = string.Empty;

    public string Package { get; set; } = string.Empty;

    public string SignType { get; set; } = "HMAC-SHA256";

    public string PaySign { get; set; } = string.Empty;

    public string PrepayId { get; set; } = string.Empty;

    public string OutTradeNo { get; set; } = string.Empty;
}

public sealed class WeChatOrderQueryResult
{
    public bool IsSuccess { get; set; }

    public string OutTradeNo { get; set; } = string.Empty;

    public string TransactionId { get; set; } = string.Empty;

    public string TradeState { get; set; } = string.Empty;

    public string TradeStateDesc { get; set; } = string.Empty;

    public int TotalFeeFen { get; set; }
}

public sealed class WeChatPaymentNotifyResult
{
    public bool IsSuccess { get; set; }

    public string OutTradeNo { get; set; } = string.Empty;

    public string TransactionId { get; set; } = string.Empty;

    public string TradeState { get; set; } = string.Empty;

    public int TotalFeeFen { get; set; }
}
