using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

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
        string timestamp,
        string nonce,
        string signature,
        string serial,
        CancellationToken cancellationToken = default);
}

public sealed class WeChatPayService : IWeChatPayService
{
    private const string BaseUrl = "https://api.mch.weixin.qq.com";

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
        ValidateBaseConfiguration();

        if (request.TotalFeeFen <= 0)
        {
            throw new InvalidOperationException("支付金额必须大于 0。");
        }

        if (string.IsNullOrWhiteSpace(request.OpenId))
        {
            throw new InvalidOperationException("用户 openid 不能为空，无法发起微信 JSAPI 支付。");
        }

        if (string.IsNullOrWhiteSpace(request.OutTradeNo))
        {
            throw new InvalidOperationException("商户订单号不能为空。");
        }

        var payload = new
        {
            appid = _options.AppId,
            mchid = _options.MchId,
            description = request.Description,
            out_trade_no = request.OutTradeNo,
            notify_url = _options.NotifyUrl,
            attach = request.Attach,
            amount = new
            {
                total = request.TotalFeeFen,
                currency = "CNY"
            },
            payer = new
            {
                openid = request.OpenId
            }
        };

        var body = JsonSerializer.Serialize(payload, JsonOptions);
        const string requestPath = "/v3/pay/transactions/jsapi";
        var responseText = await SendSignedRequestAsync(HttpMethod.Post, requestPath, body, cancellationToken);
        var response = JsonSerializer.Deserialize<WeChatCreateTransactionResponse>(responseText, JsonOptions)
            ?? throw new InvalidOperationException("微信支付返回内容无法解析。");

        if (string.IsNullOrWhiteSpace(response.PrepayId))
        {
            throw new InvalidOperationException("微信支付未返回 prepay_id。");
        }

        var clientNonce = CreateNonce();
        var timeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var packageValue = $"prepay_id={response.PrepayId}";
        var paySign = SignForClient(timeStamp, clientNonce, packageValue);

        return new WeChatCreatePaymentResult
        {
            AppId = _options.AppId,
            TimeStamp = timeStamp,
            NonceStr = clientNonce,
            Package = packageValue,
            SignType = "RSA",
            PaySign = paySign,
            PrepayId = response.PrepayId,
            OutTradeNo = request.OutTradeNo
        };
    }

    public async Task<WeChatOrderQueryResult> QueryPaymentStatusAsync(
        string outTradeNo,
        CancellationToken cancellationToken = default)
    {
        ValidateBaseConfiguration();

        if (string.IsNullOrWhiteSpace(outTradeNo))
        {
            throw new InvalidOperationException("商户订单号不能为空。");
        }

        var encodedTradeNo = Uri.EscapeDataString(outTradeNo);
        var encodedMchId = Uri.EscapeDataString(_options.MchId);
        var requestPath = $"/v3/pay/transactions/out-trade-no/{encodedTradeNo}?mchid={encodedMchId}";

        var responseText = await SendSignedRequestAsync(HttpMethod.Get, requestPath, null, cancellationToken);
        var response = JsonSerializer.Deserialize<WeChatOrderQueryResponse>(responseText, JsonOptions)
            ?? throw new InvalidOperationException("微信查单返回内容无法解析。");

        return new WeChatOrderQueryResult
        {
            IsSuccess = string.Equals(response.TradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase),
            OutTradeNo = response.OutTradeNo ?? outTradeNo,
            TransactionId = response.TransactionId ?? string.Empty,
            TradeState = response.TradeState ?? string.Empty,
            TradeStateDesc = response.TradeStateDesc ?? string.Empty,
            TotalFeeFen = response.Amount?.Total ?? 0
        };
    }

    public Task<WeChatPaymentNotifyResult> ProcessPaymentNotificationAsync(
        string requestBody,
        string timestamp,
        string nonce,
        string signature,
        string serial,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateNotificationConfiguration();

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new InvalidOperationException("微信支付回调内容为空。");
        }

        if (!VerifyNotificationSignature(timestamp, nonce, requestBody, signature, serial))
        {
            throw new InvalidOperationException("微信支付回调验签失败。");
        }

        var notify = JsonSerializer.Deserialize<WeChatPayNotifyEnvelope>(requestBody, JsonOptions)
            ?? throw new InvalidOperationException("微信支付回调内容无法解析。");

        if (!string.Equals(notify.EventType, "TRANSACTION.SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"暂不处理的微信支付事件类型：{notify.EventType}");
        }

        if (notify.Resource is null)
        {
            throw new InvalidOperationException("微信支付回调缺少 resource。");
        }

        var plaintext = DecryptResource(
            notify.Resource.AssociatedData,
            notify.Resource.Nonce,
            notify.Resource.Ciphertext);

        var transaction = JsonSerializer.Deserialize<WeChatNotifyTransaction>(plaintext, JsonOptions)
            ?? throw new InvalidOperationException("微信支付回调解密内容无法解析。");

        return Task.FromResult(new WeChatPaymentNotifyResult
        {
            IsSuccess = string.Equals(transaction.TradeState, "SUCCESS", StringComparison.OrdinalIgnoreCase),
            OutTradeNo = transaction.OutTradeNo ?? string.Empty,
            TransactionId = transaction.TransactionId ?? string.Empty,
            TradeState = transaction.TradeState ?? string.Empty,
            TotalFeeFen = transaction.Amount?.Total ?? 0
        });
    }

    private async Task<string> SendSignedRequestAsync(
        HttpMethod method,
        string requestPath,
        string? body,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = CreateNonce();
        var actualBody = body ?? string.Empty;
        var authorization = BuildAuthorization(method.Method, requestPath, timestamp, nonce, actualBody);

        using var request = new HttpRequestMessage(method, $"{BaseUrl}{requestPath}");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw BuildWeChatException(responseText, (int)response.StatusCode);
        }

        return responseText;
    }

    private string BuildAuthorization(string method, string requestPath, string timestamp, string nonce, string body)
    {
        var message = $"{method}\n{requestPath}\n{timestamp}\n{nonce}\n{body}\n";
        var signature = SignWithMerchantPrivateKey(message);

        return
            $"WECHATPAY2-SHA256-RSA2048 mchid=\"{_options.MchId}\"," +
            $"nonce_str=\"{nonce}\"," +
            $"timestamp=\"{timestamp}\"," +
            $"serial_no=\"{_options.MerchantSerialNumber}\"," +
            $"signature=\"{signature}\"";
    }

    private string SignForClient(string timeStamp, string nonceStr, string packageValue)
    {
        var message = $"{_options.AppId}\n{timeStamp}\n{nonceStr}\n{packageValue}\n";
        return SignWithMerchantPrivateKey(message);
    }

    private string SignWithMerchantPrivateKey(string message)
    {
        using var rsa = LoadMerchantPrivateKey();
        var data = Encoding.UTF8.GetBytes(message);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    private bool VerifyNotificationSignature(
        string timestamp,
        string nonce,
        string body,
        string signature,
        string serial)
    {
        if (string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(body) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        using var certificate = LoadPlatformCertificate();

        if (!string.IsNullOrWhiteSpace(serial))
        {
            var actualSerial = certificate.SerialNumber?.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
            var expectedSerial = serial.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(actualSerial, expectedSerial, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is null)
        {
            return false;
        }

        var message = $"{timestamp}\n{nonce}\n{body}\n";
        var data = Encoding.UTF8.GetBytes(message);
        var signatureBytes = Convert.FromBase64String(signature);
        return rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private string DecryptResource(string? associatedData, string? nonce, string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(ciphertext))
        {
            throw new InvalidOperationException("微信支付回调缺少解密参数。");
        }

        var key = Encoding.UTF8.GetBytes(_options.ApiV3Key);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("微信支付 ApiV3Key 必须是 32 字节。");
        }

        var encryptedBytes = Convert.FromBase64String(ciphertext);
        if (encryptedBytes.Length < 17)
        {
            throw new InvalidOperationException("微信支付回调密文格式不正确。");
        }

        var cipherBytes = encryptedBytes[..^16];
        var tagBytes = encryptedBytes[^16..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(
            Encoding.UTF8.GetBytes(nonce),
            cipherBytes,
            tagBytes,
            plainBytes,
            string.IsNullOrEmpty(associatedData) ? null : Encoding.UTF8.GetBytes(associatedData));

        return Encoding.UTF8.GetString(plainBytes);
    }

    private RSA LoadMerchantPrivateKey()
    {
        var pem = ReadConfiguredContent(_options.PrivateKeyPem, _options.PrivateKeyPath);
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new InvalidOperationException("微信支付商户私钥未配置，请设置 WeChat:PrivateKeyPath 或 WeChat:PrivateKeyPem。");
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    private X509Certificate2 LoadPlatformCertificate()
    {
        var certificatePem = ReadConfiguredContent(_options.PlatformCertificatePem, _options.PlatformCertificatePath);
        if (string.IsNullOrWhiteSpace(certificatePem))
        {
            throw new InvalidOperationException("微信支付平台证书未配置，请设置 WeChat:PlatformCertificatePath 或 WeChat:PlatformCertificatePem。");
        }

        return X509Certificate2.CreateFromPem(certificatePem);
    }

    private static string ReadConfiguredContent(string inlineContent, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(inlineContent))
        {
            return inlineContent;
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return File.ReadAllText(filePath);
        }

        return string.Empty;
    }

    private void ValidateBaseConfiguration()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.AppId))
        {
            missing.Add("WeChat:AppId");
        }

        if (string.IsNullOrWhiteSpace(_options.MchId) || _options.MchId.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("WeChat:MchId");
        }

        if (string.IsNullOrWhiteSpace(_options.NotifyUrl) || _options.NotifyUrl.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("WeChat:NotifyUrl");
        }

        if (string.IsNullOrWhiteSpace(_options.MerchantSerialNumber) || _options.MerchantSerialNumber.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("WeChat:MerchantSerialNumber");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiV3Key) || _options.ApiV3Key.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("WeChat:ApiV3Key");
        }

        if (string.IsNullOrWhiteSpace(_options.PrivateKeyPem) && string.IsNullOrWhiteSpace(_options.PrivateKeyPath))
        {
            missing.Add("WeChat:PrivateKeyPath/PrivateKeyPem");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"微信支付配置不完整：{string.Join("、", missing)}");
        }
    }

    private void ValidateNotificationConfiguration()
    {
        ValidateBaseConfiguration();

        if (string.IsNullOrWhiteSpace(_options.PlatformCertificatePem) &&
            string.IsNullOrWhiteSpace(_options.PlatformCertificatePath))
        {
            throw new InvalidOperationException("微信支付平台证书未配置，无法校验回调签名。");
        }
    }

    private static Exception BuildWeChatException(string responseText, int statusCode)
    {
        try
        {
            var error = JsonSerializer.Deserialize<WeChatErrorResponse>(responseText, JsonOptions);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return new InvalidOperationException($"微信支付请求失败({statusCode})：{error.Code} - {error.Message}");
            }
        }
        catch
        {
        }

        return new InvalidOperationException($"微信支付请求失败({statusCode})：{responseText}");
    }

    private static string CreateNonce() => Guid.NewGuid().ToString("N");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class WeChatCreatePaymentRequest
{
    public string Description { get; set; } = string.Empty;

    public string OutTradeNo { get; set; } = string.Empty;

    public int TotalFeeFen { get; set; }

    public string OpenId { get; set; } = string.Empty;

    public string? Attach { get; set; }
}

public sealed class WeChatCreatePaymentResult
{
    public string AppId { get; set; } = string.Empty;

    public string TimeStamp { get; set; } = string.Empty;

    public string NonceStr { get; set; } = string.Empty;

    public string Package { get; set; } = string.Empty;

    public string SignType { get; set; } = "RSA";

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

internal sealed class WeChatCreateTransactionResponse
{
    public string? PrepayId { get; set; }
}

internal sealed class WeChatOrderQueryResponse
{
    public string? OutTradeNo { get; set; }

    public string? TransactionId { get; set; }

    public string? TradeState { get; set; }

    public string? TradeStateDesc { get; set; }

    public WeChatAmountResponse? Amount { get; set; }
}

internal sealed class WeChatPayNotifyEnvelope
{
    public string? EventType { get; set; }

    public WeChatNotifyResource? Resource { get; set; }
}

internal sealed class WeChatNotifyResource
{
    public string? AssociatedData { get; set; }

    public string? Nonce { get; set; }

    public string? Ciphertext { get; set; }
}

internal sealed class WeChatNotifyTransaction
{
    public string? OutTradeNo { get; set; }

    public string? TransactionId { get; set; }

    public string? TradeState { get; set; }

    public WeChatAmountResponse? Amount { get; set; }
}

internal sealed class WeChatAmountResponse
{
    public int Total { get; set; }
}

internal sealed class WeChatErrorResponse
{
    public string? Code { get; set; }

    public string? Message { get; set; }
}
