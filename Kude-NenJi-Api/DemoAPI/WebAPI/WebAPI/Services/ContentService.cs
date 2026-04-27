using WebAPI.Dtos;

namespace WebAPI.Services;

public class ContentService : IContentService
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, List<ActivitySummaryDto>> _activities;
    private readonly List<ActivityDetailDto> _activityDetails;
    private readonly OrderMenuDataDto _orderMenuData;
    private readonly Dictionary<int, OrderGoodsDetailDto> _orderGoodsDetails;
    private readonly Dictionary<int, Dictionary<int, int>> _orderCarts = [];

    public ContentService()
    {
        _activities = BuildActivities();
        _activityDetails = BuildActivityDetails();
        _orderMenuData = BuildOrderMenuData();
        _orderGoodsDetails = BuildOrderGoodsDetails();
    }

    public Task<ActivityListDto> GetActivitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ActivityListDto
        {
            Activities = _activities.ToDictionary(
                x => x.Key,
                x => x.Value.Select(item => new ActivitySummaryDto
                {
                    Id = item.Id,
                    Title = item.Title,
                    Price = item.Price,
                    Date = item.Date,
                    Image = item.Image,
                    CategoryName = item.CategoryName
                }).ToList())
        });
    }

    public Task<ActivityDetailDto?> GetActivityDetailAsync(int activityId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_activityDetails.FirstOrDefault(x => x.Id == activityId));
    }

    public Task<OrderMenuDataDto> GetOrderMenuDataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_orderMenuData);
    }

    public Task<OrderGoodsDetailDto?> GetOrderGoodsDetailAsync(int goodsId, CancellationToken cancellationToken = default)
    {
        _orderGoodsDetails.TryGetValue(goodsId, out var detail);
        return Task.FromResult(detail);
    }

    public Task<OrderCartDto> GetOrderCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            return Task.FromResult(BuildOrderCartDto(userId));
        }
    }

    public Task<OrderCartDto> AddToOrderCartAsync(int userId, OrderCartAddRequest request, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            var goods = GetRequiredGoods(request.GoodsId);
            var cart = GetOrCreateCart(userId);
            cart.TryGetValue(request.GoodsId, out var currentQuantity);
            var nextQuantity = currentQuantity + request.Count;

            if (nextQuantity > goods.Stock)
            {
                throw new InvalidOperationException("商品库存不足");
            }

            cart[request.GoodsId] = nextQuantity;
            return Task.FromResult(BuildOrderCartDto(userId));
        }
    }

    public Task<OrderCartDto> UpdateOrderCartAsync(int userId, OrderCartUpdateRequest request, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            var goods = GetRequiredGoods(request.GoodsId);
            var cart = GetOrCreateCart(userId);

            if (request.Quantity <= 0)
            {
                cart.Remove(request.GoodsId);
                return Task.FromResult(BuildOrderCartDto(userId));
            }

            if (request.Quantity > goods.Stock)
            {
                throw new InvalidOperationException("商品库存不足");
            }

            cart[request.GoodsId] = request.Quantity;
            return Task.FromResult(BuildOrderCartDto(userId));
        }
    }

    public Task<OrderCartDto> ClearOrderCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _orderCarts.Remove(userId);
            return Task.FromResult(new OrderCartDto());
        }
    }

    public Task<SubmitMealOrderResponse> SubmitMealOrderAsync(int userId, SubmitMealOrderRequest request, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            var cart = GetOrCreateCart(userId);
            if (cart.Count == 0)
            {
                throw new InvalidOperationException("购物车为空");
            }

            var cartDto = BuildOrderCartDto(userId);
            _orderCarts.Remove(userId);

            return Task.FromResult(new SubmitMealOrderResponse
            {
                OrderNo = $"M{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}",
                ItemCount = cartDto.CartCount,
                TotalPrice = cartDto.TotalPrice
            });
        }
    }

    private Dictionary<int, int> GetOrCreateCart(int userId)
    {
        if (!_orderCarts.TryGetValue(userId, out var cart))
        {
            cart = [];
            _orderCarts[userId] = cart;
        }

        return cart;
    }

    private OrderGoodsDetailDto GetRequiredGoods(int goodsId)
    {
        if (!_orderGoodsDetails.TryGetValue(goodsId, out var goods))
        {
            throw new InvalidOperationException("商品不存在");
        }

        return goods;
    }

    private OrderCartDto BuildOrderCartDto(int userId)
    {
        if (!_orderCarts.TryGetValue(userId, out var cart) || cart.Count == 0)
        {
            return new OrderCartDto();
        }

        var items = cart
            .Where(x => _orderGoodsDetails.ContainsKey(x.Key))
            .Select(x =>
            {
                var goods = _orderGoodsDetails[x.Key];
                return new OrderCartItemDto
                {
                    GoodsId = goods.Id,
                    Name = goods.Name,
                    Image = goods.Image,
                    Price = goods.Price,
                    Quantity = x.Value,
                    Stock = goods.Stock
                };
            })
            .ToList();

        return new OrderCartDto
        {
            Items = items,
            CartCount = items.Sum(x => x.Quantity),
            TotalPrice = items.Sum(x => x.Price * x.Quantity)
        };
    }

    private static Dictionary<string, List<ActivitySummaryDto>> BuildActivities()
    {
        var all = new List<ActivitySummaryDto>
        {
            new() { Id = 1, Title = "农家研学活动报名中", Price = "¥20", Date = "2025.2.25-2025.3.6", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20playing%20football%20on%20farm&image_size=landscape_16_9" },
            new() { Id = 2, Title = "采摘活动报名中", Price = "¥50", Date = "2025.2.25-2025.3.6", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20lettuce%20field&image_size=landscape_16_9" },
            new() { Id = 3, Title = "草莓采摘体验", Price = "¥30", Date = "2025.3.1-2025.4.30", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=strawberry%20picking&image_size=landscape_16_9" },
            new() { Id = 4, Title = "葡萄采摘节", Price = "¥50", Date = "2025.7.1-2025.8.31", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=grape%20picking&image_size=landscape_16_9" },
            new() { Id = 5, Title = "农场露营体验", Price = "¥120", Date = "2025.4.1-2025.10.31", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20camping%20tent&image_size=landscape_16_9" },
            new() { Id = 6, Title = "篝火露营晚会", Price = "¥180", Date = "2025.5.1-2025.9.30", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=camping%20with%20campfire&image_size=landscape_16_9" }
        };

        return new Dictionary<string, List<ActivitySummaryDto>>
        {
            ["all"] = all,
            ["picking"] = all.Where(x => x.Id is 2 or 3 or 4).ToList(),
            ["camping"] = all.Where(x => x.Id is 5 or 6).ToList()
        };
    }

    private static List<ActivityDetailDto> BuildActivityDetails()
    {
        return
        [
            new ActivityDetailDto
            {
                Id = 1,
                Title = "农家研学活动报名中",
                Price = "¥20",
                Date = "2025.2.25-2025.3.6",
                Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20playing%20football%20on%20farm&image_size=landscape_16_9",
                Description = "通过农场劳动、采摘体验和农产品制作，让孩子在自然里学习农业知识。",
                Location = "农场优选生态农场",
                People = "限 50 人",
                Content = "1. 农场参观\n2. 采摘体验\n3. 动物喂养\n4. 农产品制作\n5. 农耕体验",
                Images =
                [
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20playing%20football%20on%20farm&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20feeding%20farm%20animals&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20picking%20vegetables&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20learning%20farming&image_size=square"
                ]
            },
            new ActivityDetailDto
            {
                Id = 2,
                Title = "采摘活动报名中",
                Price = "¥50",
                Date = "2025.2.25-2025.3.6",
                Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20lettuce%20field&image_size=landscape_16_9",
                Description = "适合亲子和家庭参与的采摘活动，可现场采摘时令蔬果并购买带回家。",
                Location = "农场优选生态农场",
                People = "不限",
                Content = "1. 采摘体验\n2. 农场参观\n3. 农产品购买\n4. 休闲野餐",
                Images =
                [
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20lettuce%20field&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=people%20picking%20vegetables&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20fresh%20produce&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=family%20picnic%20on%20farm&image_size=square"
                ]
            },
            new ActivityDetailDto
            {
                Id = 3,
                Title = "草莓采摘体验",
                Price = "¥30",
                Date = "2025.3.1-2025.4.30",
                Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=strawberry%20picking&image_size=landscape_16_9",
                Description = "春季热门活动，在草莓大棚中采摘新鲜草莓，适合亲子和情侣体验。",
                Location = "农场优选草莓基地",
                People = "不限",
                Content = "1. 草莓采摘\n2. 草莓品尝\n3. 草莓购买\n4. 草莓 DIY",
                Images =
                [
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=strawberry%20picking&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20strawberries&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20picking%20strawberries&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=strawberry%20diy&image_size=square"
                ]
            },
            new ActivityDetailDto
            {
                Id = 4,
                Title = "葡萄采摘节",
                Price = "¥50",
                Date = "2025.7.1-2025.8.31",
                Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=grape%20picking&image_size=landscape_16_9",
                Description = "夏季葡萄节活动，可品尝多个品种的新鲜葡萄和体验葡萄园采摘。",
                Location = "农场优选葡萄基地",
                People = "不限",
                Content = "1. 葡萄采摘\n2. 葡萄品尝\n3. 葡萄购买\n4. 葡萄酒品鉴",
                Images =
                [
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=grape%20picking&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20grapes&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=grape%20vineyard&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=wine%20tasting&image_size=square"
                ]
            },
            new ActivityDetailDto
            {
                Id = 5,
                Title = "农场露营体验",
                Price = "¥120",
                Date = "2025.4.1-2025.10.31",
                Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20camping%20tent&image_size=landscape_16_9",
                Description = "在农场草地上搭帐篷露营，夜晚赏星空，清晨体验农场早餐和晨露采摘。",
                Location = "农场优选露营基地",
                People = "限 30 人",
                Content = "1. 帐篷搭建\n2. 篝火晚会\n3. 星空观测\n4. 农场早餐\n5. 晨露采摘",
                Images =
                [
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20camping%20tent&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=campfire%20at%20night&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=stargazing&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20breakfast&image_size=square"
                ]
            },
            new ActivityDetailDto
            {
                Id = 6,
                Title = "篝火露营晚会",
                Price = "¥180",
                Date = "2025.5.1-2025.9.30",
                Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=camping%20with%20campfire&image_size=landscape_16_9",
                Description = "夜间篝火、烧烤和露营结合的户外活动，适合多人结伴参加。",
                Location = "农场优选露营基地",
                People = "限 30 人",
                Content = "1. 帐篷搭建\n2. 篝火晚会\n3. 烧烤盛宴\n4. 星空观测\n5. 农场早餐\n6. 农场参观",
                Images =
                [
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=camping%20with%20campfire&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=bbq%20party&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=people%20singing%20around%20campfire&image_size=square",
                    "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=farm%20camping%20morning&image_size=square"
                ]
            }
        ];
    }

    private static OrderMenuDataDto BuildOrderMenuData()
    {
        return new OrderMenuDataDto
        {
            Categories =
            [
                new() { Id = "vegetables", Name = "新鲜蔬菜" },
                new() { Id = "meat", Name = "肉类产品" },
                new() { Id = "eggs", Name = "禽蛋产品" },
                new() { Id = "dairy", Name = "乳制品" },
                new() { Id = "staple", Name = "主食" }
            ],
            GoodsList = new Dictionary<string, List<MenuGoodsDto>>
            {
                ["vegetables"] =
                [
                    new() { Id = 1, Name = "有机生菜", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square", Price = 30, Sold = 150, Stock = 30 },
                    new() { Id = 2, Name = "农家西红柿", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square", Price = 30, Sold = 200, Stock = 30 },
                    new() { Id = 3, Name = "新鲜黄瓜", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20cucumbers&image_size=square", Price = 30, Sold = 180, Stock = 30 }
                ],
                ["meat"] =
                [
                    new() { Id = 4, Name = "土猪肉", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20pork%20meat&image_size=square", Price = 30, Sold = 100, Stock = 30 },
                    new() { Id = 5, Name = "农家土鸡", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20chicken&image_size=square", Price = 30, Sold = 80, Stock = 30 }
                ],
                ["eggs"] =
                [
                    new() { Id = 6, Name = "土鸡蛋", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20eggs&image_size=square", Price = 30, Sold = 300, Stock = 30 },
                    new() { Id = 7, Name = "鸭蛋", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20duck%20eggs&image_size=square", Price = 30, Sold = 150, Stock = 30 },
                    new() { Id = 8, Name = "鹅蛋", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20goose%20eggs&image_size=square", Price = 30, Sold = 50, Stock = 30 }
                ],
                ["dairy"] =
                [
                    new() { Id = 9, Name = "新鲜牛奶", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20milk&image_size=square", Price = 30, Sold = 200, Stock = 30 },
                    new() { Id = 10, Name = "农家酸奶", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20yogurt&image_size=square", Price = 30, Sold = 180, Stock = 30 }
                ],
                ["staple"] =
                [
                    new() { Id = 11, Name = "农家大米", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20rice&image_size=square", Price = 30, Sold = 250, Stock = 30 },
                    new() { Id = 12, Name = "手工面条", Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20noodles&image_size=square", Price = 30, Sold = 150, Stock = 30 }
                ]
            }
        };
    }

    private static Dictionary<int, OrderGoodsDetailDto> BuildOrderGoodsDetails()
    {
        return new Dictionary<int, OrderGoodsDetailDto>
        {
            [1] = new() { Id = 1, Name = "有机生菜", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=lettuce%20field&image_size=portrait_4_3", Description = "有机生菜，无农药、无化肥，适合沙拉和清炒。", Weight = "500g", Storage = "冷藏", Sold = 150, Stock = 30 },
            [2] = new() { Id = 2, Name = "农家西红柿", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=tomato%20field&image_size=portrait_4_3", Description = "自然成熟西红柿，酸甜可口。", Weight = "500g", Storage = "常温", Sold = 200, Stock = 30 },
            [3] = new() { Id = 3, Name = "新鲜黄瓜", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20cucumbers&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=cucumber%20field&image_size=portrait_4_3", Description = "清脆爽口，适合凉拌与直接食用。", Weight = "500g", Storage = "冷藏", Sold = 180, Stock = 30 },
            [4] = new() { Id = 4, Name = "土猪肉", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20pork%20meat&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=pig%20farm&image_size=portrait_4_3", Description = "农家散养土猪肉，肥瘦相间。", Weight = "500g", Storage = "冷冻", Sold = 100, Stock = 30 },
            [5] = new() { Id = 5, Name = "农家土鸡", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20chicken&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=chicken%20farm&image_size=portrait_4_3", Description = "肉质紧实，适合煲汤和红烧。", Weight = "1kg", Storage = "冷冻", Sold = 80, Stock = 30 },
            [6] = new() { Id = 6, Name = "土鸡蛋", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20eggs&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=chicken%20laying%20eggs&image_size=portrait_4_3", Description = "蛋黄饱满，营养丰富。", Weight = "10枚", Storage = "冷藏", Sold = 300, Stock = 30 },
            [7] = new() { Id = 7, Name = "鸭蛋", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20duck%20eggs&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=ducks%20on%20farm&image_size=portrait_4_3", Description = "个大饱满，适合腌制和做蛋羹。", Weight = "10枚", Storage = "冷藏", Sold = 150, Stock = 30 },
            [8] = new() { Id = 8, Name = "鹅蛋", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20goose%20eggs&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=geese%20on%20farm&image_size=portrait_4_3", Description = "个头大，适合煎蛋和蛋羹。", Weight = "5枚", Storage = "冷藏", Sold = 50, Stock = 30 },
            [9] = new() { Id = 9, Name = "新鲜牛奶", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20milk&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=cows%20on%20farm&image_size=portrait_4_3", Description = "农场鲜奶，口感醇厚。", Weight = "500ml", Storage = "冷藏", Sold = 200, Stock = 30 },
            [10] = new() { Id = 10, Name = "农家酸奶", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20yogurt&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=yogurt%20making&image_size=portrait_4_3", Description = "口感醇厚，酸甜适中。", Weight = "500g", Storage = "冷藏", Sold = 180, Stock = 30 },
            [11] = new() { Id = 11, Name = "农家大米", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20rice&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=rice%20field&image_size=portrait_4_3", Description = "颗粒饱满，香糯可口。", Weight = "1kg", Storage = "常温", Sold = 250, Stock = 30 },
            [12] = new() { Id = 12, Name = "手工面条", Price = 30, Image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20noodles&image_size=square", DetailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=making%20noodles%20by%20hand&image_size=portrait_4_3", Description = "劲道爽滑，适合汤面和拌面。", Weight = "500g", Storage = "常温", Sold = 150, Stock = 30 }
        };
    }
}
