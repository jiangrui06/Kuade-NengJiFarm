namespace WebAPI.Options;

public sealed class WeChatPayOptions
{
    public const string SectionName = "WeChat";

    public string AppId { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string MchId { get; set; } = string.Empty;

    public string NotifyUrl { get; set; } = string.Empty;

    public string MerchantSerialNumber { get; set; } = string.Empty;

    public string ApiV3Key { get; set; } = string.Empty;

    public string PrivateKeyPath { get; set; } = string.Empty;

    public string PrivateKeyPem { get; set; } = string.Empty;

    public string PlatformCertificatePath { get; set; } = string.Empty;

    public string PlatformCertificatePem { get; set; } = string.Empty;
}
