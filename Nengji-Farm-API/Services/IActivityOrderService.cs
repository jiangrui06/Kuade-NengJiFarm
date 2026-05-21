using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IActivityOrderService
{
    Task<(List<ActivityOrderListItemDto> Records, int Total)> GetOrderListAsync(
        int pageNum, int pageSize, string? keyword, int? statusId, CancellationToken cancellationToken = default);

    Task<ActivityOrderFullDetailDto?> GetOrderDetailAsync(long orderId, CancellationToken cancellationToken = default);

    Task<bool> VerifyOrderDetailAsync(long activityOrderDetailsId, CancellationToken cancellationToken = default);

    Task<ActivityOrderRefundResponse> RefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);

    Task<ActivityOrderRejectResponse> RejectRefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);
}
