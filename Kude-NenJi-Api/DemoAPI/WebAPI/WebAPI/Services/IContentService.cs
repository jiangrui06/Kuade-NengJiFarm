using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IContentService
{
    Task<ActivityListDto> GetActivitiesAsync(CancellationToken cancellationToken = default);

    Task<ActivityDetailDto?> GetActivityDetailAsync(int activityId, CancellationToken cancellationToken = default);

    Task<OrderMenuDataDto> GetOrderMenuDataAsync(CancellationToken cancellationToken = default);

    Task<OrderGoodsDetailDto?> GetOrderGoodsDetailAsync(int goodsId, CancellationToken cancellationToken = default);

    Task<OrderCartDto> GetOrderCartAsync(int userId, CancellationToken cancellationToken = default);

    Task<OrderCartDto> AddToOrderCartAsync(int userId, OrderCartAddRequest request, CancellationToken cancellationToken = default);

    Task<OrderCartDto> UpdateOrderCartAsync(int userId, OrderCartUpdateRequest request, CancellationToken cancellationToken = default);

    Task<OrderCartDto> ClearOrderCartAsync(int userId, CancellationToken cancellationToken = default);

    Task<SubmitMealOrderResponse> SubmitMealOrderAsync(int userId, SubmitMealOrderRequest request, CancellationToken cancellationToken = default);
}
