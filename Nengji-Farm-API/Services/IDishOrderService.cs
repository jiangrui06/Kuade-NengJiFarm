using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IDishOrderService
{
    Task<DishOrderListResponseDto> GetOrderListAsync(int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<DishOrderDetailResponseDto> GetOrderDetailAsync(string orderNo, CancellationToken cancellationToken = default);

    Task<DishOrderRefundResponse> RefundAsync(DishOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);
}
