using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoApiController : ControllerBase
{
    [HttpGet("home")]
    public ActionResult<ApiResponse<object>> GetHome()
    {
        var data = new
        {
            swiperList = new[]
            {
                new { id = 1, image = "https://th.bing.com/th/id/OIP.7Mx2bUZRQOkkrDY8-4XGtQAAAA?o=7rm=3&rs=1&pid=ImgDetMain&o=7&rm=3" },
                new { id = 2, image = "https://th.bing.com/th/id/OIP.7Mx2bUZRQOkkrDY8-4XGtQAAAA?o=7rm=3&rs=1&pid=ImgDetMain&o=7&rm=3" },
                new { id = 3, image = "https://th.bing.com/th/id/OIP.7Mx2bUZRQOkkrDY8-4XGtQAAAA?o=7rm=3&rs=1&pid=ImgDetMain&o=7&rm=3" }
            },
            functionButtons = new[]
            {
                new { id = 1, name = "认购一亩田", color = "#4CAF50", path = "/pages/acre/acre" },
                new { id = 2, name = "农场优选", color = "#FF9800", path = "/pages/farm-goods/farm-goods" },
                new { id = 3, name = "点餐", color = "#F44336", path = "/pages/order/order" },
                new { id = 4, name = "活动中心", color = "#2196F3", path = "/pages/activity/activity" }
            },
            acreProjects = new[]
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
            },
            farmGoods = new[]
            {
                new { id = 1, name = "甜养玉米500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20sweet%20corn&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "软糯香甜", "颗粒饱满" }, stock = 464646 },
                new { id = 2, name = "农家土豆500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20potatoes&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "新鲜采摘", "农场直供" }, stock = 464646 },
                new { id = 3, name = "时令水果500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20apples%20and%20oranges&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "香甜多汁", "现摘现发" }, stock = 464646 },
                new { id = 4, name = "农家番茄500g", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20tomatoes&image_size=square", price = 8.9, originalPrice = 9.9, tags = new[] { "自然成熟", "口感鲜甜" }, stock = 464646 }
            },
            hotDishes = new[]
            {
                new { id = 1, name = "剁椒鱼头", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=spicy%20fish%20head%20dish&image_size=square", price = 68.9, tags = new[] { "月销10000份" } },
                new { id = 2, name = "农家小炒肉", image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=stir%20fried%20pork%20with%20pepper&image_size=square", price = 38.9, tags = new[] { "招牌热销" } }
            }
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
            1 => new { id = 1, name = "有机生菜", price = 30, image = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=fresh%20organic%20lettuce&image_size=square", detailImage = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=lettuce%20field&image_size=portrait_4_3", description = "有机生菜，无农药残留。", weight = "500g", storage = "冷藏" },
            2 => new { id = 2, name = "农家西红柿", price = 30, image = "", detailImage = "", description = "农家种植的西红柿。", weight = "500g", storage = "常温" },
            _ => new { id, name = "商品详情", price = 30, image = "", detailImage = "", description = "默认商品。", weight = "500g", storage = "冷藏" }
        };

        return ApiResponse<object>.Ok(item);
    }

    [HttpGet("acres")]
    public ActionResult<ApiResponse<object>> GetAcres()
    {
        var swiperList = new[]
        {
            new { id = 1, image = "https://th.bing.com/th/id/OIP.7Mx2bUZRQOkkrDY8-4XGtQAAAA?o=7rm=3&rs=1&pid=ImgDetMain&o=7&rm=3" },
            new { id = 2, image = "https://th.bing.com/th/id/OIP.7Mx2bUZRQOkkrDY8-4XGtQAAAA?o=7rm=3&rs=1&pid=ImgDetMain&o=7&rm=3" },
            new { id = 3, image = "https://th.bing.com/th/id/OIP.7Mx2bUZRQOkkrDY8-4XGtQAAAA?o=7rm=3&rs=1&pid=ImgDetMain&o=7&rm=3" }
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
            [1] = new { id = 1, name = "xxx田地", price = "99999元", image = "https://img.freepik.com/free-photo/yellow-field-with-lines_1127-3388.jpg", description = "本地块为标准型农业用地，适合认购体验。" },
            [2] = new { id = 2, name = "xxx田地", price = "99999元", image = "https://img.freepik.com/free-photo/agriculture-field-with-growing-crops_23-2148872538.jpg", description = "本地块为标准型农业用地，适合认购体验。" },
            [3] = new { id = 3, name = "xxx田地", price = "99999元", image = "https://img.freepik.com/free-photo/wheat-field_1127-3185.jpg", description = "本地块为标准型农业用地，适合认购体验。" }
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
            categories = new[] { new { id = "vegetables", name = "新鲜蔬菜" }, new { id = "meat", name = "肉类产品" } },
            goodsList = new { vegetables = new[] { new { id = 1, name = "有机生菜", image = "", price = 30, sold = 150, stock = 30 } } }
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
