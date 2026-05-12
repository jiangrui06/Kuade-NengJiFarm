using ManageAPI.Dtos;

namespace ManageAPI.Services;

public interface IDishOrderService
{
    Task<DishOrderListResponseDto> GetOrderListAsync(int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<DishOrderDetailResponseDto> GetOrderDetailAsync(string orderNo, CancellationToken cancellationToken = default);
}
