using WebAPI.Dtos;

namespace WebAPI.Services;

/// <summary>
/// 餐桌管理服务接口
/// </summary>
public interface IDiningTableService
{
    Task<(List<DiningTableListItemDto> Records, int Total)> GetTableListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<DiningTableDetailDto?> GetTableDetailAsync(string tableNo, CancellationToken cancellationToken = default);

    Task<DiningTableDetailDto?> CreateTableAsync(CreateDiningTableDto dto, CancellationToken cancellationToken = default);

    Task<bool> UpdateTableAsync(UpdateDiningTableDto dto, CancellationToken cancellationToken = default);

    Task<DiningTableDetailDto?> UpdateTableStatusAsync(UpdateTableStatusDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteTableAsync(string tableNo, CancellationToken cancellationToken = default);

    Task<bool> DeleteTableBatchAsync(string[] tableNos, CancellationToken cancellationToken = default);
}