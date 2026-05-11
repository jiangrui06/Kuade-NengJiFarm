using WebAPI.Dtos.Kitchen;

namespace WebAPI.Services;

public interface IKitchenService
{
    /// <summary>
    /// 后厨登录（基于 user 表的手机号和密码）
    /// </summary>
    Task<KitchenLoginResponseDto> LoginAsync(string phoneNumber, string password, CancellationToken cancellationToken);

    /// <summary>
    /// 获取今日订单列表
    /// type: 0=待出餐，1=已出餐
    /// </summary>
    Task<List<KitchenOrderListItemDto>> GetTodayOrderListAsync(int type = 0, CancellationToken cancellationToken = default);

    Task<(bool Success, string Message, object? Data)> CancelDishAsync(int detailId, CancellationToken ct);

    /// <summary>
    /// 获取订单详情（包含菜品明细）
    /// </summary>
    Task<KitchenOrderDetailDto> GetOrderDetailAsync(long orderId, CancellationToken cancellationToken);

    /// <summary>
    /// 标记菜品为已出餐（核心接口）
    /// </summary>
    Task<MarkDishFinishResponseDto> MarkDishFinishAsync(long dishOrderDetailsId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取今日统计数据
    /// </summary>
    Task<KitchenStatisticsDto> GetTodayStatisticsAsync(CancellationToken cancellationToken);
}