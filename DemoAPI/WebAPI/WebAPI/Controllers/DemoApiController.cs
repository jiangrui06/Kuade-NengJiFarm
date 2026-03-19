using System.Linq;

using Microsoft.AspNetCore.Mvc;

using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoApiController : ControllerBase
{
    [HttpGet("home")]
    public ActionResult<ApiResponse<object>> GetHome([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : pageSize;

        var allFarmGoods = new object[]
        {
            new { id = 1, name = "甜养玉米500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20sweet%20corn&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "软糯香甜", "颗粒饱满" }, stock = 464646 },
            new { id = 2, name = "农家土豆500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20potatoes&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "新鲜采摘", "农场直供" }, stock = 464646 },
            new { id = 3, name = "时令水果500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20apples%20and%20oranges&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "香甜多汁", "现摘现发" }, stock = 464646 },
            new { id = 4, name = "农家番茄500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "自然成熟", "口感鲜甜" }, stock = 464646 },
            new { id = 5, name = "有机生菜500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square", price = 12.8, originalPrice = 14.8, tags = new[] { "有机种植", "新鲜直达" }, stock = 100 },
            new { id = 6, name = "甜脆玉米500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20sweet%20corn%20blue%20background&image_size=square", price = 8.8, originalPrice = 11.8, tags = new[] { "农场优选", "香甜软糯" }, stock = 200 },
            new { id = 7, name = "农家西红柿500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes%20holding%20board&image_size=square", price = 9.9, originalPrice = 12.0, tags = new[] { "自然成熟", "口感鲜甜" }, stock = 150 },
            new { id = 8, name = "有机黄瓜500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20cucumber&image_size=square", price = 7.9, originalPrice = 9.9, tags = new[] { "清脆爽口", "现摘现发" }, stock = 180 },
            new { id = 9, name = "紫薯500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=purple%20sweet%20potato&image_size=square", price = 11.9, originalPrice = 13.9, tags = new[] { "香甜软糯", "营养丰富" }, stock = 120 },
            new { id = 10, name = "香菇300g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20shiitake%20mushrooms&image_size=square", price = 13.9, originalPrice = 15.9, tags = new[] { "肉厚鲜香", "煲汤推荐" }, stock = 90 },
            new { id = 11, name = "水果玉米4根", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fruit%20corn&image_size=square", price = 16.9, originalPrice = 18.9, tags = new[] { "即食清甜", "农场直供" }, stock = 88 },
            new { id = 12, name = "新鲜西蓝花500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20broccoli&image_size=square", price = 10.9, originalPrice = 12.9, tags = new[] { "鲜嫩脆爽", "轻食推荐" }, stock = 76 }
        };

        var allHotDishes = new object[]
        {
            new { id = 1, name = "剁椒鱼头", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20fish%20head%20dish&image_size=square", price = 68.9, tags = new[] { "月销10000份" } },
            new { id = 2, name = "农家小炒肉", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=stir%20fried%20pork%20with%20pepper&image_size=square", price = 38.9, tags = new[] { "招牌热销" } },
            new { id = 3, name = "酸菜鱼", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=pickled%20fish%20dish&image_size=square", price = 58.9, tags = new[] { "人气推荐" } },
            new { id = 4, name = "辣子鸡", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20chicken%20dish&image_size=square", price = 42.9, tags = new[] { "下饭必点" } },
            new { id = 5, name = "红烧肉", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=braised%20pork%20dish&image_size=square", price = 48.9, tags = new[] { "经典家常" } },
            new { id = 6, name = "番茄炒蛋", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=tomato%20egg%20dish&image_size=square", price = 18.9, tags = new[] { "家常热菜" } },
            new { id = 7, name = "干锅花菜", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=dry%20pot%20cauliflower&image_size=square", price = 26.9, tags = new[] { "香辣过瘾" } },
            new { id = 8, name = "玉米排骨汤", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=corn%20pork%20rib%20soup&image_size=square", price = 36.9, tags = new[] { "鲜甜暖胃" } },
            new { id = 9, name = "香煎豆腐", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=pan%20fried%20tofu&image_size=square", price = 19.9, tags = new[] { "外焦里嫩" } },
            new { id = 10, name = "青椒牛肉", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=beef%20with%20green%20pepper&image_size=square", price = 46.9, tags = new[] { "鲜香嫩滑" } },
            new { id = 11, name = "黄焖鸡", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=braised%20chicken%20pot&image_size=square", price = 32.9, tags = new[] { "酱香浓郁" } },
            new { id = 12, name = "清蒸鲈鱼", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=steamed%20sea%20bass&image_size=square", price = 78.9, tags = new[] { "鲜嫩少刺" } }
        };

        var farmGoods = allFarmGoods
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var hotDishes = allHotDishes
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var data = new
        {
            swiperList = page == 1
                ? new[]
                {
                    new { id = 1, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 2, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 3, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" }
                }
                : Array.Empty<object>(),

            functionButtons = page == 1
                ? new[]
                {
                    new { id = 1, name = "认购一亩田", color = "#4CAF50", path = "/pages/acre/acre" },
                    new { id = 2, name = "农场优选", color = "#FF9800", path = "/pages/farm-goods/farm-goods" },
                    new { id = 3, name = "点餐", color = "#F44336", path = "/pages/order/order" },
                    new { id = 4, name = "活动中心", color = "#2196F3", path = "/pages/activity/activity" }
                }
                : Array.Empty<object>(),

            acreProjects = page == 1
                ? new[]
                {
                    new
                    {
                        id = 1,
                        name = "认购一亩田",
                        description = "新型农场推出的共享农业体验项目。",
                        price = 99999,
                        image = "https://img.freepik.com/free-photo/yellow-field-with-lines_1127-3388.jpg"
                    },
                    new
                    {
                        id = 2,
                        name = "某某农场",
                        description = "新型农场推出的共享农业体验项目。",
                        price = 88888,
                        image = "https://img.freepik.com/free-photo/agriculture-field-with-growing-crops_23-2148872538.jpg"
                    }
                }
                : Array.Empty<object>(),

            farmGoods = farmGoods,
            hotDishes = hotDishes,
            hasMore = (page * pageSize) < Math.Max(allFarmGoods.Length, allHotDishes.Length)
        };

        return ApiResponse<object>.Ok(data);
    }

    [HttpGet("goods")]
    public ActionResult<ApiResponse<object>> GetGoods([FromQuery] string category = "all")
    {
        var categoryGoods = new Dictionary<string, object[]>
        {
            ["all"] =
            [
                new { id = 1, name = "白糯玉米 800g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square", price = 9.4, tags = new[] { "农场直供", "新鲜采摘" } },
                new { id = 2, name = "白糯玉米 800g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20corn%20on%20the%20cob&image_size=square", price = 9.4, tags = new[] { "农场直供", "新鲜采摘" } }
            ],
            ["new"] =
            [
                new { id = 7, name = "新品玉米 800g", image = "", price = 10.4, tags = new[] { "农场直供", "新品上市" } }
            ]
        };

        if (!categoryGoods.TryGetValue(category, out var goods))
        {
            goods = categoryGoods["all"];
        }

        return ApiResponse<object>.Ok(new { category, items = goods });
    }

    [HttpGet("goods/{id}")]
    public ActionResult<ApiResponse<object>> GetGoodsById(int id)
    {
        var item = id switch
        {
            1 => new
            {
                id = 1,
                name = "有机生菜",
                price = 30,
                image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square",
                detailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=lettuce%20field&image_size=portrait_4_3",
                description = "有机生菜，无农药残留。",
                weight = "500g",
                storage = "冷藏"
            },
            2 => new
            {
                id = 2,
                name = "农家西红柿",
                price = 30,
                image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square",
                detailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=tomato%20field&image_size=portrait_4_3",
                description = "农家种植的西红柿。",
                weight = "500g",
                storage = "常温"
            },
            _ => new
            {
                id = 3,
                name = "直升机",
                price = 30,
                image = "https://img2.baidu.com/it/u=1977433049,53820872&fm=253&fmt=auto&app=138&f=JPEG?w=342&h=608",
                detailImage = "https://img2.baidu.com/it/u=1977433049,53820872&fm=253&fmt=auto&app=138&f=JPEG?w=342&h=608",
                description = "默认商品。",
                weight = "500g",
                storage = "冷藏"
            }
        };

        return ApiResponse<object>.Ok(item);
    }

    [HttpGet("acres")]
    public ActionResult<ApiResponse<object>> GetAcres()
    {
        var swiperList = new[]
        {
            new { id = 1, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
            new { id = 2, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
            new { id = 3, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" }
        };

        var list = new[]
        {
            new { id = 1, name = "xxx田地", description = "认购一亩田...", price = "¥99999", image = "https://img.freepik.com/free-photo/yellow-field-with-lines_1127-3388.jpg" },
            new { id = 2, name = "xxx田地", description = "认购一亩田...", price = "¥99999", image = "https://img.freepik.com/free-photo/agriculture-field-with-growing-crops_23-2148872538.jpg" },
            new { id = 3, name = "xxx田地", description = "认购一亩田...", price = "¥99999", image = "https://img.freepik.com/free-photo/wheat-field_1127-3185.jpg" }
        };

        return ApiResponse<object>.Ok(new
        {
            swiperList,
            list,
            items = list
        });
    }

    [HttpGet("acres/{id}")]
    public ActionResult<ApiResponse<object>> GetAcreById(int id)
    {
        var details = new Dictionary<int, object>
        {
            [1] = new
            {
                id = 1,
                name = "xxx田地",
                price = "99999元",
                image = "https://img.freepik.com/free-photo/yellow-field-with-lines_1127-3388.jpg",
                description = "本地块为标准型农业用地，适合认购体验。",
                swiperList = new[]
                {
                    new { id = 1, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 2, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 3, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" }
                }
            },
            [2] = new
            {
                id = 2,
                name = "xxx田地",
                price = "99999元",
                image = "https://img.freepik.com/free-photo/agriculture-field-with-growing-crops_23-2148872538.jpg",
                description = "本地块为标准型农业用地，适合认购体验。",
                swiperList = new[]
                {
                    new { id = 1, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 2, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 3, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" }
                }
            },
            [3] = new
            {
                id = 3,
                name = "xxx田地",
                price = "99999元",
                image = "https://img.freepik.com/free-photo/wheat-field_1127-3185.jpg",
                description = "本地块为标准型农业用地，适合认购体验。",
                swiperList = new[]
                {
                    new { id = 1, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 2, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" },
                    new { id = 3, image = "https://img0.baidu.com/it/u=3670860447,3495259318&fm=253&fmt=auto&app=120&f=JPEG?w=667&h=500" }
                }
            }
        };

        if (!details.TryGetValue(id, out var acre))
        {
            acre = details[1];
        }

        return ApiResponse<object>.Ok(acre);
    }

    [HttpPost("acres/{id}/adopt")]
    public ActionResult<ApiResponse<object>> DemoAdopt(int id, [FromBody] object body)
    {
        return ApiResponse<object>.Ok(new { id, adopted = true });
    }

    [HttpGet("activities")]
    public ActionResult<ApiResponse<object>> GetActivities()
    {
        var activities = new[]
        {
            new { id = 1, title = "农家研学活动报名中", price = "门票: 10-20 元", date = "2025.2.25-2025.3.6", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=children%20playing%20football%20on%20farm&image_size=landscape_16_9" },
            new { id = 2, title = "采摘活动报名中", price = "门票: 10-50 元", date = "2025.2.25-2025.3.6", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20lettuce%20field&image_size=landscape_16_9" }
        };

        return ApiResponse<object>.Ok(activities);
    }

    [HttpGet("cart")]
    public ActionResult<ApiResponse<object>> GetCart()
    {
        var cart = new
        {
            cartList = new[]
            {
                new { id = 1, name = "白糯玉米 800g", image = "/images/activity-active.png", tag = "包邮", price = 69.99, count = 1, @checked = false },
                new { id = 2, name = "黄花鱼", image = "/images/activity-active.png", tag = "顺丰包邮", price = 169.00, count = 1, @checked = false }
            },
            totalPrice = "0.00"
        };

        return ApiResponse<object>.Ok(cart);
    }

    [HttpPost("cart/items")]
    public ActionResult<ApiResponse<object>> AddCartItem([FromBody] object body)
    {
        return ApiResponse<object>.Ok(new { id = 1 });
    }

    [HttpPut("cart/items/{id}")]
    public ActionResult<ApiResponse<object>> UpdateCartItem(int id, [FromBody] object body)
    {
        return ApiResponse<object>.Ok(null);
    }

    [HttpDelete("cart/items/{id}")]
    public ActionResult<ApiResponse<object>> DeleteCartItem(int id)
    {
        return ApiResponse<object>.Ok(null);
    }

    [HttpDelete("cart")]
    public ActionResult<ApiResponse<object>> ClearCart()
    {
        return ApiResponse<object>.Ok(null);
    }

    [HttpGet("orders")]
    public ActionResult<ApiResponse<object>> GetOrders()
    {
        var data = new
        {
            categories = new[]
            {
                new { id = "vegetables", name = "新鲜蔬菜" },
                new { id = "meat", name = "肉类产品" },
                new { id = "eggs", name = "禽蛋产品" },
                new { id = "dairy", name = "乳制品" },
                new { id = "staple", name = "主食" }
            },
            goodsList = new
            {
                vegetables = new[]
                {
                    new { id = 1, name = "有机生菜", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square", price = 30, sold = 150, stock = 30 },
                    new { id = 2, name = "农家西红柿", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square", price = 30, sold = 200, stock = 30 },
                    new { id = 3, name = "新鲜黄瓜", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20cucumbers&image_size=square", price = 30, sold = 180, stock = 30 }
                },
                meat = new[]
                {
                    new { id = 4, name = "土猪肉", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20pork%20meat&image_size=square", price = 30, sold = 100, stock = 30 },
                    new { id = 5, name = "农家土鸡", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20chicken&image_size=square", price = 30, sold = 80, stock = 30 }
                },
                eggs = new[]
                {
                    new { id = 6, name = "土鸡蛋", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20eggs&image_size=square", price = 30, sold = 300, stock = 30 },
                    new { id = 7, name = "鸭蛋", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20duck%20eggs&image_size=square", price = 30, sold = 150, stock = 30 },
                    new { id = 8, name = "鹅蛋", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20goose%20eggs&image_size=square", price = 30, sold = 50, stock = 30 }
                },
                dairy = new[]
                {
                    new { id = 9, name = "新鲜牛奶", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20milk&image_size=square", price = 30, sold = 200, stock = 30 },
                    new { id = 10, name = "农家酸奶", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20yogurt&image_size=square", price = 30, sold = 180, stock = 30 }
                },
                staple = new[]
                {
                    new { id = 11, name = "农家大米", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20rice&image_size=square", price = 30, sold = 250, stock = 30 },
                    new { id = 12, name = "手工面条", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=homemade%20noodles&image_size=square", price = 30, sold = 150, stock = 30 }
                }
            }
        };

        return ApiResponse<object>.Ok(data);
    }

    [HttpPost("orders")]
    public ActionResult<ApiResponse<object>> CreateOrder([FromBody] object body)
    {
        return ApiResponse<object>.Ok(new { orderId = 1001 });
    }

    [HttpGet("profile")]
    public ActionResult<ApiResponse<UserDto>> GetDemoProfile()
    {
        var user = new UserDto { Id = Guid.NewGuid(), NickName = "游客", AvatarUrl = "", PhoneNumber = "" };
        return ApiResponse<UserDto>.Ok(user);
    }
}