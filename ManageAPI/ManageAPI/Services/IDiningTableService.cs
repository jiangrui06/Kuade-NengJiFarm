using ManageAPI.Dtos;

namespace ManageAPI.Services;

public interface IDiningTableService
{
    Task<(List<DiningTableListItemDto> Records, int Total)> GetListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<long> CreateAsync(CreateDiningTableDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
