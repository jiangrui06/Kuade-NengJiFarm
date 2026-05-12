using ManageAPI.Dtos;

namespace ManageAPI.Services;

public interface IProductOrderService
{
    Task<ProductOrderListResponseDto> GetOrderListAsync(int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<ProductOrderDetailResponseDto> GetOrderDetailAsync(string orderNo, CancellationToken cancellationToken = default);

    Task UpdateOrderStatusAsync(UpdateProductOrderStatusDto dto, CancellationToken cancellationToken = default);
}
