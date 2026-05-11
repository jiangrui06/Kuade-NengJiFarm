using WebAPI.Dtos;

namespace WebAPI.Services;

/// <summary>
/// 菜品管理服务接口
/// </summary>
public interface IDishService
{
    Task<(List<DishListItemDto> Records, int Total)> GetDishListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<DishDetailDto?> GetDishDetailAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateDishAsync(CreateDishDto dto, CancellationToken cancellationToken = default);

    Task<bool> UpdateDishAsync(UpdateDishDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteDishAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> DeleteDishBatchAsync(int[] ids, CancellationToken cancellationToken = default);
}