using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IActivityOrderService
{
    Task<(List<ActivityOrderListItemDto> Records, int Total)> GetOrderListAsync(
        int pageNum, int pageSize, string? keyword, int? statusId, CancellationToken cancellationToken = default);

    Task<ActivityOrderFullDetailDto?> GetOrderDetailAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>通过订单号查询订单详情</summary>
    Task<ActivityOrderFullDetailDto?> GetOrderDetailAsync(string orderNo, CancellationToken cancellationToken = default);

    Task<bool> VerifyOrderDetailAsync(long activityOrderDetailsId, CancellationToken cancellationToken = default);

    /// <summary>通过主订单 ID 找到第一个未核销的明细进行核销</summary>
    Task<(bool Success, string Message)> VerifyByOrderIdAsync(long orderId, CancellationToken cancellationToken = default);

    /// <summary>通过订单号找到第一个未核销的明细进行核销</summary>
    Task<(bool Success, string Message)> VerifyByOrderNoAsync(string orderNo, CancellationToken cancellationToken = default);

    Task<ActivityOrderRefundResponse> RefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);

    /// <summary>处理退款（确认退款）—— 将 pending 状态的退款标记为 completed，订单改为已退款</summary>
    Task<ActivityOrderRefundResponse> ProcessRefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);

    Task<ActivityOrderRejectResponse> RejectRefundAsync(ActivityOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);
}
