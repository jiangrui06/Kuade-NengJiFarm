namespace ManageAPI.Options;

public sealed class WeChatPayOptions
{
    public const string SectionName = "WeChat";

    public string AppId { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string MchId { get; set; } = string.Empty;

    public string NotifyUrl { get; set; } = string.Empty;

    public string ApiV2Key { get; set; } = string.Empty;

    public string SignType { get; set; } = "HMAC-SHA256";

    public string MerchantSerialNumber { get; set; } = string.Empty;

    public string ApiV3Key { get; set; } = string.Empty;

    public string PrivateKeyPath { get; set; } = string.Empty;

    public string PrivateKeyPem { get; set; } = string.Empty;

    public string PlatformCertificatePath { get; set; } = string.Empty;

    public string PlatformCertificatePem { get; set; } = string.Empty;

    /// <summary>
    /// 退款证书文件路径（.p12 格式）
    /// </summary>
    public string RefundCertPath { get; set; } = string.Empty;

    /// <summary>
    /// 退款证书密码（通常为商户号 MchId）
    /// </summary>
    public string RefundCertPassword { get; set; } = string.Empty;
}
