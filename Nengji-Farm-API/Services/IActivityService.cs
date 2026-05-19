using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IActivityService
{
    Task<(List<ActivityListItemDto> Records, int Total)> GetActivityListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<ActivityManageDetailDto?> GetActivityDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<long> CreateActivityAsync(CreateActivityDto dto, CancellationToken cancellationToken = default);

    Task<bool> UpdateActivityAsync(long id, UpdateActivityDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteActivityAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> DeleteActivityBatchAsync(long[] ids, CancellationToken cancellationToken = default);
}
