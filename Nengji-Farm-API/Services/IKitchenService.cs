using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IKitchenService
{
    /// <summary>
    /// �����¼������ user �����ֻ��ź����룩
    /// </summary>
    Task<KitchenLoginResponseDto> LoginAsync(string phoneNumber, string password, CancellationToken cancellationToken);

    /// <summary>
    /// ��ȡ���ն����б�
    /// type: 0=�����ͣ�1=�ѳ���
    /// </summary>
    Task<List<KitchenOrderListItemDto>> GetTodayOrderListAsync(int type = 0, CancellationToken cancellationToken = default);

    Task<(bool Success, string Message, object? Data)> CancelDishAsync(long detailId, CancellationToken ct);

    /// <summary>
    /// ��ȡ�������飨������Ʒ��ϸ��
    /// </summary>
    Task<KitchenOrderDetailDto> GetOrderDetailAsync(long orderId, CancellationToken cancellationToken);

    /// <summary>
    /// ��ǲ�ƷΪ�ѳ��ͣ����Ľӿڣ�
    /// </summary>
    Task<MarkDishFinishResponseDto> MarkDishFinishAsync(long dishOrderDetailsId, CancellationToken cancellationToken);

    /// <summary>
    /// ��ȡ����ͳ������
    /// </summary>
    Task<KitchenStatisticsDto> GetTodayStatisticsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// ���� ID ��ȡ������
    /// </summary>
    Task<KitchenLoginResponseDto?> GetUserByIdAsync(int userId, CancellationToken cancellationToken);
}