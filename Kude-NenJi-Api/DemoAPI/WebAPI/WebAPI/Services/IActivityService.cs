using WebAPI.Dtos;

namespace WebAPI.Services;

/// <summary>
/// 活动/券品管理服务接口
/// </summary>
public interface IActivityService
{
    /// <summary>
    /// 获取活动分页列表
    /// </summary>
    Task<(List<ActivitySummaryDto> Records, int Total)> GetActivityListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取活动详情
    /// </summary>
    Task<ActivityDetailDto?> GetActivityDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 活动报名
    /// </summary>
    ActivityRegisterResponse RegisterActivity(long activityId);

    /// <summary>
    /// 获取所有活动（按分类）
    /// </summary>
    Task<Dictionary<string, List<ActivitySummaryDto>>> GetAllActivitiesAsync(
        CancellationToken cancellationToken = default);
}