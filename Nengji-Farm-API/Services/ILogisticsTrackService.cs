namespace WebAPI.Services;

public interface ILogisticsTrackService
{
    Task<LogisticsTrackResult> QueryAsync(LogisticsTrackQuery query, CancellationToken cancellationToken = default);
}

public sealed class LogisticsTrackQuery
{
    public string CompanyType { get; set; } = string.Empty;

    public string TrackingNumber { get; set; } = string.Empty;

    public string? PhoneNo { get; set; }

    public string? OrderNumber { get; set; }

    public int? TrackingType { get; set; }

    public int Top { get; set; } = 2000;
}

public sealed class LogisticsTrackResult
{
    public string CompanyType { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string TrackingNumber { get; set; } = string.Empty;

    public string? OrderNumber { get; set; }

    public string CurrentStatus { get; set; } = string.Empty;

    public string? CurrentRemark { get; set; }

    public string? CurrentTime { get; set; }

    public string? CurrentAddress { get; set; }

    public int Total { get; set; }

    public IReadOnlyList<LogisticsTrackNode> DataList { get; set; } = Array.Empty<LogisticsTrackNode>();
}

public sealed class LogisticsTrackNode
{
    public string? Remark { get; set; }

    public string? OperationTime { get; set; }

    public string? RouteAddress { get; set; }
}
