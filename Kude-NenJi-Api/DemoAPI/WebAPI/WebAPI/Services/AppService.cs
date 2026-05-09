using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;

namespace WebAPI.Services;

public class AppService : IAppService
{
    private static readonly string[] CategoryColors =
    [
        "#4E8B3A",
        "#FF8A3D",
        "#2F7D8C",
        "#C66B3D",
        "#D94F70"
    ];

    private static readonly List<GoodsSummaryDto> DefaultHotDishes =
    [
        new GoodsSummaryDto
        {
            Id = 1001,
            Name = "剁椒鱼头",
            Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20fish%20head%20dish&image_size=square",
            Price = 68m,
            OriginalPrice = 68m,
            Stock = 99,
            Tags = ["月销1000份"]
        },
        new GoodsSummaryDto
        {
            Id = 1002,
            Name = "农家小炒肉",
            Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=stir%20fried%20pork%20with%20pepper&image_size=square",
            Price = 38m,
            OriginalPrice = 38m,
            Stock = 99,
            Tags = ["招牌热销"]
        }
    ];

    private readonly AppDbContext _dbContext;

    public AppService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HomeIndexDto> GetHomeIndexAsync(CancellationToken cancellationToken = default)
    {
        var farmGoods = await BuildGoodsSummariesAsync(
            _dbContext.Commodities
                .AsNoTracking()
                .Where(x => (x.CommodityStatusId ?? 0) == 1)
                .OrderByDescending(x => x.CommodityId)
                .Take(6),
            cancellationToken);

        var hotDishRows = await _dbContext.Dishes
            .AsNoTracking()
            .Where(x => x.Status == 1)
            .OrderByDescending(x => x.DishSold)
            .ThenByDescending(x => x.DishId)
            .Take(6)
            .ToListAsync(cancellationToken);

        var hotDishes = hotDishRows.Select(x => new GoodsSummaryDto
        {
            Id = x.DishId,
            Name = x.DishName,
            Image = x.ImageUrl,
            Price = x.DishPrice,
            OriginalPrice = x.DishPrice,
            Stock = x.DishRemainingQuantity,
            Tags = string.IsNullOrWhiteSpace(x.AttributeName) ? [] : [x.AttributeName]
        }).ToList();

        if (hotDishes.Count == 0)
        {
            hotDishes = DefaultHotDishes;
        }

        return new HomeIndexDto
        {
            SwiperList = farmGoods.Take(3).Select((x, index) => new SwiperItemDto
            {
                Id = index + 1,
                Image = x.Image
            }).ToList(),
            FunctionButtons =
            [
                new FunctionButtonDto { Id = 1, Name = "认购一亩田", Color = "#4E8B3A", Path = "/pages/acre/acre" },
                new FunctionButtonDto { Id = 2, Name = "农场优选", Color = "#FF8A3D", Path = "/pages/farm-goods/farm-goods" },
                new FunctionButtonDto { Id = 3, Name = "热销菜品", Color = "#2F7D8C", Path = "/pages/order/order" },
                new FunctionButtonDto { Id = 4, Name = "活动中心", Color = "#C66B3D", Path = "/pages/activity/activity" }
            ],
            FarmGoods = farmGoods,
            HotDishes = hotDishes
        };
    }

    public async Task<FarmGoodsIndexDto> GetFarmGoodsIndexAsync(CancellationToken cancellationToken = default)
    {
        var categories = await BuildCategoriesAsync(cancellationToken);
        var goods = await BuildGoodsSummariesAsync(
            _dbContext.Commodities
                .AsNoTracking()
                .Where(x => (x.CommodityStatusId ?? 0) == 1)
                .OrderByDescending(x => x.CommodityId)
                .Take(12),
            cancellationToken);

        return new FarmGoodsIndexDto
        {
            SwiperList = goods.Take(3).Select((x, index) => new SwiperItemDto
            {
                Id = index + 1,
                Image = x.Image
            }).ToList(),
            Categories = categories,
            TodayGoods = goods.Take(6).ToList(),
            HotGoods = goods.Skip(6).Take(6).ToList()
        };
    }

    public async Task<PagedGoodsDto> GetGoodsByCategoryAsync(int categoryId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => (x.CommodityStatusId ?? 0) == 1 && x.CategoryId == categoryId)
            .OrderByDescending(x => x.CommodityId);

        return await BuildPagedGoodsAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedGoodsDto> SearchGoodsAsync(string keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        keyword = keyword.Trim();

        var query = _dbContext.Commodities
            .AsNoTracking()
            .Where(x => (x.CommodityStatusId ?? 0) == 1
                && (x.ProductName.Contains(keyword) || (x.SpecDescription ?? string.Empty).Contains(keyword)))
            .OrderByDescending(x => x.CommodityId);

        return await BuildPagedGoodsAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<GoodsDetailDto?> GetGoodsDetailAsync(int goodsId, CancellationToken cancellationToken = default)
    {
        var commodity = await _dbContext.Commodities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CommodityId == goodsId && (x.CommodityStatusId ?? 0) == 1, cancellationToken);

        if (commodity is null)
        {
            return null;
        }

        var detailImage = await _dbContext.CommodityImages
            .AsNoTracking()
            .Where(x => x.CommodityId == goodsId)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .Select(x => x.Url)
            .FirstOrDefaultAsync(cancellationToken);
        var tags = await GetCommodityTagsAsync([commodity.CommodityId], cancellationToken);

        return new GoodsDetailDto
        {
            Id = commodity.CommodityId,
            Name = commodity.ProductName,
            Price = 0m,
            Image = commodity.ImageUrl ?? string.Empty,
            DetailImage = detailImage ?? commodity.ImageUrl ?? string.Empty,
            Description = commodity.SpecDescription ?? string.Empty,
            Weight = commodity.Quantity.HasValue && commodity.Quantity > 0 ? $"{commodity.Quantity}g" : string.Empty,
            Storage = "常温",
            Stock = commodity.InStock ?? 0,
            Tags = tags.GetValueOrDefault(commodity.CommodityId, [])
        };
    }

    public async Task<CartListDto> GetCartListAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cartItems = await _dbContext.ShippingCarts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.JoinTime)
            .ToListAsync(cancellationToken);

        if (cartItems.Count == 0)
        {
            await EnsureDemoCartItemsAsync(userId, cancellationToken);
            cartItems = await _dbContext.ShippingCarts
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.JoinTime)
                .ToListAsync(cancellationToken);
        }

        var commodityIds = cartItems.Select(x => x.CommodityId).Distinct().ToList();
        var commodities = await _dbContext.Commodities
            .AsNoTracking()
            .Where(x => commodityIds.Contains(x.CommodityId))
            .ToDictionaryAsync(x => x.CommodityId, cancellationToken);
        var tags = await GetCommodityTagsAsync(commodityIds, cancellationToken);

        return new CartListDto
        {
            CartList = cartItems
                .Where(x => commodities.ContainsKey(x.CommodityId))
                .Select(x =>
                {
                    var goods = commodities[x.CommodityId];
                    return new CartItemDto
                    {
                        Id = x.ShippingCartId,
                        GoodsId = x.CommodityId,
                        Name = goods.ProductName,
                        Image = goods.ImageUrl ?? string.Empty,
                        Tag = tags.GetValueOrDefault(x.CommodityId, []).FirstOrDefault() ?? "包邮",
                        Price = ResolveCommodityPrice(goods),
                        Count = x.CartQuantity,
                        Checked = false
                    };
                })
                .ToList()
        };
    }

    public async Task AddCartItemAsync(int userId, CartAddRequest request, CancellationToken cancellationToken = default)
    {
        var goods = await _dbContext.Commodities
            .FirstOrDefaultAsync(x => x.CommodityId == request.GoodsId && (x.CommodityStatusId ?? 0) == 1, cancellationToken);

        if (goods is null)
        {
            throw new BusinessException("商品不存在", 404);
        }

        var existing = await _dbContext.ShippingCarts
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CommodityId == request.GoodsId, cancellationToken);

        if (existing is null)
        {
            _dbContext.ShippingCarts.Add(new ShippingCart
            {
                UserId = userId,
                CommodityId = request.GoodsId,
                CartQuantity = request.Count,
                JoinTime = DateTime.UtcNow
            });
        }
        else
        {
            existing.CartQuantity += request.Count;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCartItemAsync(int userId, CartUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.ShippingCarts
            .FirstOrDefaultAsync(x => x.ShippingCartId == request.CartId && x.UserId == userId, cancellationToken);

        if (cart is null)
        {
            throw new BusinessException("购物车商品不存在", 1003);
        }

        cart.CartQuantity = request.Count;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCartItemAsync(int userId, int cartId, CancellationToken cancellationToken = default)
    {
        var cart = await _dbContext.ShippingCarts
            .FirstOrDefaultAsync(x => x.ShippingCartId == cartId && x.UserId == userId, cancellationToken);

        if (cart is null)
        {
            return;
        }

        _dbContext.ShippingCarts.Remove(cart);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        var carts = await _dbContext.ShippingCarts
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (carts.Count == 0)
        {
            return;
        }

        _dbContext.ShippingCarts.RemoveRange(carts);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> CreateOrderAsync(int userId, CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var address = await _dbContext.ShippingAddresses
            .FirstOrDefaultAsync(x => x.AddressId == request.AddressId && x.UserId == userId, cancellationToken);

        if (address is null)
        {
            throw new BusinessException("收货地址不存在", 404);
        }

        var cartItems = await _dbContext.ShippingCarts
            .Where(x => x.UserId == userId && request.CartIds.Contains(x.ShippingCartId))
            .ToListAsync(cancellationToken);

        if (cartItems.Count == 0)
        {
            throw new BusinessException("购物车商品不存在", 1003);
        }

        var commodityIds = cartItems.Select(x => x.CommodityId).Distinct().ToList();
        var commodities = await _dbContext.Commodities
            .Where(x => commodityIds.Contains(x.CommodityId))
            .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

        var totalAmount = request.TotalPrice <= 0m ? 0m : request.TotalPrice;
        var now = DateTime.UtcNow;
        var order = new OrderEntity
        {
            UserId = userId,
            OrderNumber = GenerateOrderNumber(),
            ActualPayment = totalAmount,
            TotalOrderAmount = totalAmount,
            OrderType = 1,
            OrderStatus = 0,
            PaymentStatus = 0,
            DeliveryMethods = 1,
            ShippingAddress = BuildAddressText(address),
            AddressId = address.AddressId,
            ContactPerson = address.ContactName,
            ContactNumber = string.Empty,
            OrderCreationTime = now,
            PaymentTime = now,
            PaymentMethods = 0
        };

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var cartItem in cartItems)
        {
            commodities.TryGetValue(cartItem.CommodityId, out var commodity);
            _dbContext.OrderDetails.Add(new OrderDetail
            {
                OrderId = order.OrderId,
                CommodityId = cartItem.CommodityId,
                DishName = commodity?.ProductName ?? string.Empty,
                ActualUnitPrice = 0m,
                PurchaseQuantity = cartItem.CartQuantity,
                SubtotalAmount = 0m,
                Taste = string.Empty,
                MealStatus = 0
            });

            if (commodity is not null && commodity.InStock.HasValue && commodity.InStock.Value >= cartItem.CartQuantity)
            {
                commodity.InStock -= cartItem.CartQuantity;
            }
        }

        _dbContext.ShippingCarts.RemoveRange(cartItems);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return order.OrderId;
    }

    public async Task<OrderListDto> GetOrderListAsync(int userId, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var statusCode = MapOrderStatus(status);
            query = query.Where(x => x.OrderStatus == statusCode);
        }

        var total = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(x => x.OrderCreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new OrderListDto
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            OrderList = orders.Select(x => new OrderListItemDto
            {
                Id = x.OrderId,
                OrderNo = x.OrderNumber,
                TotalPrice = x.TotalOrderAmount,
                Status = MapOrderStatusText(x.OrderStatus),
                CreateTime = x.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList()
        };
    }

    public async Task<OrderDetailDto?> GetOrderDetailAsync(int userId, long orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var address = await _dbContext.ShippingAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AddressId == order.AddressId, cancellationToken);

        var details = await _dbContext.OrderDetails
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .ToListAsync(cancellationToken);

        var commodityIds = details.Select(x => x.CommodityId).Distinct().ToList();
        var commodities = await _dbContext.Commodities
            .AsNoTracking()
            .Where(x => commodityIds.Contains(x.CommodityId))
            .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

        return new OrderDetailDto
        {
            Id = order.OrderId,
            OrderNo = order.OrderNumber,
            TotalPrice = order.TotalOrderAmount,
            Status = MapOrderStatusText(order.OrderStatus),
            CreateTime = order.OrderCreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
            Address = new OrderAddressDto
            {
                Name = address?.ContactName ?? order.ContactPerson,
                Phone = order.ContactNumber,
                Address = address is null ? order.ShippingAddress : BuildAddressText(address)
            },
            GoodsList = details
                .Where(x => commodities.ContainsKey(x.CommodityId))
                .Select(x =>
                {
                    var commodity = commodities[x.CommodityId];
                    return new OrderGoodsDto
                    {
                        Id = commodity.CommodityId,
                        Name = commodity.ProductName,
                        Image = commodity.ImageUrl ?? string.Empty,
                        Price = x.ActualUnitPrice,
                        Count = x.PurchaseQuantity
                    };
                })
                .ToList()
        };
    }

    public async Task<bool> CancelOrderAsync(int userId, long orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);

        if (order is null)
        {
            return false;
        }

        order.OrderStatus = 4;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new UserProfileDto
            {
                Id = x.UserId,
                Nickname = x.WxName,
                Avatar = x.WxImage,
                Phone = x.PhoneNumber,
                Email = string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> UpdateUserProfileAsync(int userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (user is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Nickname))
        {
            user.WxName = request.Nickname.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Avatar))
        {
            user.WxImage = request.Avatar.Trim();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<AddressDto>> GetAddressesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var addresses = await _dbContext.ShippingAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AddressId)
            .ToListAsync(cancellationToken);

        return addresses.Select((x, index) => new AddressDto
        {
            Id = x.AddressId,
            Name = x.ContactName,
            Phone = string.Empty,
            Province = x.Province,
            City = x.City,
            District = x.MunicipalDistrict,
            Address = $"{x.Town}{x.HouseNumber}",
            IsDefault = index == 0
        }).ToList();
    }

    public async Task<int> CreateAddressAsync(int userId, SaveAddressRequest request, CancellationToken cancellationToken = default)
    {
        var (town, houseNumber) = SplitAddress(request.Address);
        var address = new ShippingAddress
        {
            UserId = userId,
            ContactName = request.Name,
            Province = request.Province,
            City = request.City,
            MunicipalDistrict = request.District,
            Town = town,
            HouseNumber = houseNumber
        };

        _dbContext.ShippingAddresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return address.AddressId;
    }

    public async Task<bool> UpdateAddressAsync(int userId, SaveAddressRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Id.HasValue)
        {
            return false;
        }

        var address = await _dbContext.ShippingAddresses
            .FirstOrDefaultAsync(x => x.AddressId == request.Id.Value && x.UserId == userId, cancellationToken);

        if (address is null)
        {
            return false;
        }

        var (town, houseNumber) = SplitAddress(request.Address);
        address.ContactName = request.Name;
        address.Province = request.Province;
        address.City = request.City;
        address.MunicipalDistrict = request.District;
        address.Town = town;
        address.HouseNumber = houseNumber;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAddressAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var address = await _dbContext.ShippingAddresses
            .FirstOrDefaultAsync(x => x.AddressId == id && x.UserId == userId, cancellationToken);

        if (address is null)
        {
            return false;
        }

        _dbContext.ShippingAddresses.Remove(address);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PagedGoodsDto> BuildPagedGoodsAsync(IQueryable<Commodity> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var pagedQuery = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return new PagedGoodsDto
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            GoodsList = await BuildGoodsSummariesAsync(pagedQuery, cancellationToken)
        };
    }

    private async Task<List<CategoryDto>> BuildCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => (x.CategoryStatus ?? 0) == 1)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return categories.Select((x, index) => new CategoryDto
        {
            Id = x.Id,
            Name = x.CategoryName,
            Icon = string.Empty,
            Color = CategoryColors[index % CategoryColors.Length]
        }).ToList();
    }

    private async Task<List<GoodsSummaryDto>> BuildGoodsSummariesAsync(IQueryable<Commodity> query, CancellationToken cancellationToken)
    {
        var items = await query.ToListAsync(cancellationToken);
        var ids = items.Select(x => x.CommodityId).Distinct().ToList();
        var tags = await GetCommodityTagsAsync(ids, cancellationToken);

        return items.Select(x => new GoodsSummaryDto
        {
            Id = x.CommodityId,
            Name = x.ProductName,
            Image = x.ImageUrl ?? string.Empty,
            Price = 0m,
            OriginalPrice = 0m,
            Stock = x.InStock ?? 0,
            Tags = tags.GetValueOrDefault(x.CommodityId, [])
        }).ToList();
    }

    private async Task<Dictionary<int, List<string>>> GetCommodityTagsAsync(IReadOnlyCollection<int> commodityIds, CancellationToken cancellationToken)
    {
        if (commodityIds.Count == 0)
        {
            return [];
        }

        var tagPairs = await (
            from relation in _dbContext.CommodityTagRelations.AsNoTracking()
            join tag in _dbContext.Tags.AsNoTracking() on relation.TagId equals tag.TagId
            where commodityIds.Contains(relation.CommodityId)
            select new
            {
                relation.CommodityId,
                tag.TagName
            })
            .ToListAsync(cancellationToken);

        return tagPairs
            .GroupBy(x => x.CommodityId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.TagName).Distinct().ToList());
    }

    private static string GenerateOrderNumber()
    {
        return $"{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static int MapOrderStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" => 0,
            "paid" => 1,
            "shipped" => 2,
            "completed" => 3,
            "cancelled" => 4,
            _ => 0
        };
    }

    private static string MapOrderStatusText(int status)
    {
        return status switch
        {
            0 => "pending",
            1 => "paid",
            2 => "shipped",
            3 => "completed",
            4 => "cancelled",
            _ => "pending"
        };
    }

    private static string BuildAddressText(ShippingAddress address)
    {
        return $"{address.Province}{address.City}{address.MunicipalDistrict}{address.Town}{address.HouseNumber}";
    }

    private async Task EnsureDemoCartItemsAsync(int userId, CancellationToken cancellationToken)
    {
        var demoGoods = await _dbContext.Commodities
            .AsNoTracking()
            .Where(x => (x.CommodityStatusId ?? 0) == 1)
            .OrderBy(x => x.CommodityId)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (demoGoods.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var demoItems = demoGoods.Select((goods, index) => new ShippingCart
        {
            UserId = userId,
            CommodityId = goods.CommodityId,
            CartQuantity = index == 0 ? 1 : 2,
            JoinTime = now.AddMinutes(-index)
        });

        _dbContext.ShippingCarts.AddRange(demoItems);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static decimal ResolveCommodityPrice(Commodity goods)
    {
        return goods.ProductName switch
        {
            "有机生菜" => 30m,
            "农家西红柿" => 18.8m,
            "土猪肉" => 48m,
            "土鸡蛋" => 16.8m,
            "新鲜牛奶" => 12.8m,
            "农家大米" => 39.9m,
            _ => 19.9m
        };
    }

    private static (string Town, string HouseNumber) SplitAddress(string address)
    {
        address = address.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            return (string.Empty, string.Empty);
        }

        if (address.Length <= 50)
        {
            return (string.Empty, address);
        }

        return (address[..50], address[50..]);
    }
}
