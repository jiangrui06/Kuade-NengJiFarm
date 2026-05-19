using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Extensions.Options;

using ManageAPI.Options;

namespace ManageAPI.Services;

public interface IWeChatPayService
{
    Task<WeChatRefundResult> ProcessRefundAsync(WeChatRefundRequest request, CancellationToken cancellationToken = default);
}

public sealed class WeChatPayService : IWeChatPayService
{
    private const string BaseUrl = "https://api.mch.weixin.qq.com";
    private const string RefundPath = "/secapi/pay/refund";
    private const string DefaultSignType = "HMAC-SHA256";

    private readonly HttpClient _httpClient;
    private readonly WeChatPayOptions _options;
    private readonly ILogger<WeChatPayService> _logger;

    public WeChatPayService(
        IHttpClientFactory httpClientFactory,
        IOptions<WeChatPayOptions> options,
        ILogger<WeChatPayService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("WeChatSecApi");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WeChatRefundResult> ProcessRefundAsync(WeChatRefundRequest request, CancellationToken cancellationToken = default)
    {
        ValidateBaseConfiguration();

        if (request.TotalFeeFen <= 0 || request.RefundFeeFen <= 0)
        {
            throw new InvalidOperationException("退款金额必须大于0");
        }

        if (request.RefundFeeFen > request.TotalFeeFen)
        {
            throw new InvalidOperationException("退款金额不能超过订单总金额");
        }

        if (string.IsNullOrWhiteSpace(request.OutTradeNo) && string.IsNullOrWhiteSpace(request.TransactionId))
        {
            throw new InvalidOperationException("商户订单号和微信订单号不能同时为空");
        }

        var outRefundNo = request.OutRefundNo ?? $"{request.OutTradeNo ?? request.TransactionId}_RF{DateTime.Now:yyyyMMddHHmmss}";

        var signType = ResolveSignType();
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["appid"] = _options.AppId.Trim(),
            ["mch_id"] = _options.MchId.Trim(),
            ["nonce_str"] = CreateNonce(),
            ["sign_type"] = signType,
            ["out_refund_no"] = outRefundNo,
            ["total_fee"] = request.TotalFeeFen.ToString(CultureInfo.InvariantCulture),
            ["refund_fee"] = request.RefundFeeFen.ToString(CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(request.TransactionId))
        {
            values["transaction_id"] = request.TransactionId.Trim();
        }
        else
        {
            values["out_trade_no"] = request.OutTradeNo!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.RefundDesc))
        {
            values["refund_desc"] = request.RefundDesc.Trim();
        }

        values["sign"] = CreateSign(values, signType);

        var xml = BuildXml(values);
        _logger.LogInformation("正在请求微信退款 - OutTradeNo: {OutTradeNo}, RefundFee: {RefundFee}分", request.OutTradeNo, request.RefundFeeFen);

        string responseText;
        try
        {
            responseText = await PostXmlSecAsync(RefundPath, xml, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "微信退款HTTP请求失败 - OutTradeNo: {OutTradeNo}, TransactionId: {TransactionId}", request.OutTradeNo ?? "N/A", request.TransactionId ?? "N/A");
            throw new InvalidOperationException($"微信退款请求失败：{ex.Message}", ex);
        }

        var response = ParseXml(responseText);
        EnsureWeChatResultSuccess(response, "refund");

        var refundId = GetValue(response, "refund_id") ?? string.Empty;

        _logger.LogInformation("微信退款成功 - OutTradeNo: {OutTradeNo}, TransactionId: {TransactionId}, RefundId: {RefundId}", request.OutTradeNo, request.TransactionId, refundId);

        return new WeChatRefundResult
        {
            IsSuccess = true,
            RefundId = refundId,
            OutRefundNo = outRefundNo,
            OutTradeNo = request.OutTradeNo,
            TransactionId = request.TransactionId,
            TotalFeeFen = request.TotalFeeFen,
            RefundFeeFen = request.RefundFeeFen,
        };
    }

    private async Task<string> PostXmlSecAsync(string requestPath, string xml, CancellationToken cancellationToken)
    {
        using var content = new StringContent(xml, Encoding.UTF8, "text/xml");
        using var response = await _httpClient.PostAsync($"{BaseUrl}{requestPath}", content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"微信退款请求失败 (HTTP {(int)response.StatusCode}): {responseText}");
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

        if (string.Equals(signType, "MD5", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(stringSignTemp))).ToUpperInvariant();
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ApiV2Key.Trim()));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringSignTemp))).ToUpperInvariant();
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
            var root = document.Root ?? throw new InvalidOperationException("XML根节点缺失");

            return root.Elements()
                .GroupBy(x => x.Name.LocalName, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new InvalidOperationException("微信支付XML解析失败", ex);
        }
    }

    private void EnsureWeChatResultSuccess(IReadOnlyDictionary<string, string> values, string operation)
    {
        var returnCode = GetValue(values, "return_code");
        if (!string.Equals(returnCode, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"微信{operation}失败: {GetValue(values, "return_msg") ?? "未知错误"}");
        }

        var resultCode = GetValue(values, "result_code");
        if (!string.Equals(resultCode, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            var code = GetValue(values, "err_code") ?? "UNKNOWN";
            var description = GetValue(values, "err_code_des") ?? GetValue(values, "return_msg") ?? "未知错误";
            throw new InvalidOperationException($"微信{operation}失败: {code} - {description}");
        }
    }

    private void ValidateBaseConfiguration()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.AppId))
            missing.Add("WeChat:AppId");
        if (string.IsNullOrWhiteSpace(_options.MchId))
            missing.Add("WeChat:MchId");
        if (string.IsNullOrWhiteSpace(_options.ApiV2Key))
            missing.Add("WeChat:ApiV2Key");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"微信支付配置不完整: {string.Join(", ", missing)}");
        }
    }

    private string ResolveSignType()
    {
        return string.Equals(_options.SignType, "MD5", StringComparison.OrdinalIgnoreCase)
            ? "MD5"
            : DefaultSignType;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static string CreateNonce()
    {
        return Guid.NewGuid().ToString("N");
    }
}

public sealed class WeChatRefundRequest
{
    /// <summary>商户订单号（与 TransactionId 二选一）</summary>
    public string? OutTradeNo { get; set; }

    /// <summary>微信支付订单号（与 OutTradeNo 二选一，推荐）</summary>
    public string? TransactionId { get; set; }

    /// <summary>商户退款单号（不传则自动生成）</summary>
    public string? OutRefundNo { get; set; }

    /// <summary>订单总金额（单位：分）</summary>
    public int TotalFeeFen { get; set; }

    /// <summary>退款金额（单位：分）</summary>
    public int RefundFeeFen { get; set; }

    /// <summary>退款原因（选填）</summary>
    public string? RefundDesc { get; set; }
}

public sealed class WeChatRefundResult
{
    public bool IsSuccess { get; set; }
    public string RefundId { get; set; } = string.Empty;
    public string OutRefundNo { get; set; } = string.Empty;
    public string? OutTradeNo { get; set; }
    public string? TransactionId { get; set; }
    public int TotalFeeFen { get; set; }
    public int RefundFeeFen { get; set; }
}
