namespace WebAPI.Services;

public interface IPointsService
{
    /// <summary>获取用户积分总览</summary>
    Task<PointsSummaryDto?> GetSummaryAsync(int userId, CancellationToken ct = default);

    /// <summary>获取积分流水</summary>
    Task<PointsRecordListDto> GetRecordsAsync(int userId, string? type, int page, int pageSize, CancellationToken ct = default);

    /// <summary>消费积分入账（10元=1分）</summary>
    Task EarnPointsAsync(int userId, string orderNo, decimal amount, CancellationToken ct = default);

    /// <summary>积分兑换商品</summary>
    Task<PointsExchangeResultDto> ExchangeAsync(int userId, int commodityId, int quantity, CancellationToken ct = default);

    /// <summary>获取兑换记录</summary>
    Task<PointsExchangeListDto> GetExchangeRecordsAsync(int userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>查询兑换详情</summary>
    Task<PointsExchangeDetailDto?> GetExchangeDetailAsync(string orderNo, int userId, CancellationToken ct = default);

    /// <summary>取消积分兑换（仅待核销状态可取消，积分退回）</summary>
    Task<PointsCancelResultDto> CancelExchangeAsync(string orderNo, int userId, CancellationToken ct = default);

    /// <summary>退款时扣除已发放积分</summary>
    Task DeductPointsAsync(int userId, string orderNo, decimal amount, string description, CancellationToken ct = default);
}

public class PointsSummaryDto
{
    public int TotalPoints { get; set; }
    public int EarnedPoints { get; set; }
    public int SpentPoints { get; set; }
    public int TodayEarned { get; set; }
}

public class PointsRecordListDto
{
    public List<PointsRecordItemDto> List { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class PointsRecordItemDto
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Time { get; set; } = string.Empty;
}

public class PointsExchangeResultDto
{
    public long ExchangeId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public int PointsSpent { get; set; }
    public int PointsRemaining { get; set; }
    public string QrcodeUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string StatusText { get; set; } = "待核销";
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public class PointsExchangeDetailDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int PointsSpent { get; set; }
    public int PointsRemaining { get; set; }
    public string QrcodeUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string? VerifyTime { get; set; }
}

public class PointsExchangeListDto
{
    public List<PointsExchangeItemDto> List { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class PointsExchangeItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
}

public class PointsCancelResultDto
{
    public string OrderNo { get; set; } = string.Empty;
    public int PointsReturned { get; set; }
    public int PointsRemaining { get; set; }
}
