using WebAPI.Dtos;

namespace WebAPI.Services;

public interface IAppService
{
    Task<HomeIndexDto> GetHomeIndexAsync(CancellationToken cancellationToken = default);

    Task<FarmGoodsIndexDto> GetFarmGoodsIndexAsync(CancellationToken cancellationToken = default);

    Task<PagedGoodsDto> GetGoodsByCategoryAsync(int categoryId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedGoodsDto> SearchGoodsAsync(string keyword, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<GoodsDetailDto?> GetGoodsDetailAsync(int goodsId, CancellationToken cancellationToken = default);

    Task<CartListDto> GetCartListAsync(int userId, CancellationToken cancellationToken = default);

    Task AddCartItemAsync(int userId, CartAddRequest request, CancellationToken cancellationToken = default);

    Task UpdateCartItemAsync(int userId, CartUpdateRequest request, CancellationToken cancellationToken = default);

    Task DeleteCartItemAsync(int userId, int cartId, CancellationToken cancellationToken = default);

    Task ClearCartAsync(int userId, CancellationToken cancellationToken = default);

    Task<long> CreateOrderAsync(int userId, CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task<OrderListDto> GetOrderListAsync(int userId, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<OrderDetailDto?> GetOrderDetailAsync(int userId, long orderId, CancellationToken cancellationToken = default);

    Task<bool> CancelOrderAsync(int userId, long orderId, CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> UpdateUserProfileAsync(int userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default);

    Task<List<AddressDto>> GetAddressesAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> CreateAddressAsync(int userId, SaveAddressRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAddressAsync(int userId, SaveAddressRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAddressAsync(int userId, int id, CancellationToken cancellationToken = default);
}
