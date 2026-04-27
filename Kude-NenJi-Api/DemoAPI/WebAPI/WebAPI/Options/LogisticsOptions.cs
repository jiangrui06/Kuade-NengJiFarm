namespace WebAPI.Options;

public sealed class LogisticsOptions
{
    public const string SectionName = "Logistics";

    public SfLogisticsOptions SF { get; set; } = new();

    public JdLogisticsOptions JD { get; set; } = new();
}

public sealed class SfLogisticsOptions
{
    public string ServiceUrl { get; set; } = "https://sfapi.sf-express.com/std/service";

    public string PartnerId { get; set; } = string.Empty;

    public string CheckWord { get; set; } = string.Empty;

    public string FromCode { get; set; } = string.Empty;

    public string VersionNo { get; set; } = string.Empty;

    public string Language { get; set; } = "0";

    public int DefaultTrackingType { get; set; } = 1;
}

public sealed class JdLogisticsOptions
{
    public string ServiceUrl { get; set; } = string.Empty;

    public string AppKey { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string CustomerCode { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;
}
