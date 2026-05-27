using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IDishOrderService
{
    Task<DishOrderListResponseDto> GetOrderListAsync(int pageNum, int pageSize, string? keyword, int? statusId, CancellationToken cancellationToken = default);

    Task<DishOrderDetailResponseDto> GetOrderDetailAsync(string orderNo, CancellationToken cancellationToken = default);

    Task<DishOrderRefundResponse> RefundAsync(DishOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);

    /// <summary>申请退款（进入退款中状态，Status=pending）</summary>
    Task<DishOrderRefundResponse> RefundRequestAsync(DishOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);

    /// <summary>确认退款（调用微信退款，Status=completed）</summary>
    Task<DishOrderRefundResponse> RefundProcessAsync(DishOrderRefundRequest request, string operatorName, CancellationToken cancellationToken = default);

    /// <summary>驳回退款（恢复订单状态，Status=rejected）</summary>
    Task<DishOrderRefundResponse> RefundRejectAsync(DishOrderRefundRejectRequest request, string operatorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驳回退款（管理员主动操作）：已完成/待出餐 → 微信退款 → 已取消
    /// </summary>
    Task<DishOrderRefundResponse> RejectRefundAsync(string orderNo, string? rejectReason, string operatorName, CancellationToken cancellationToken = default);
}
