using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;

namespace WebAPI.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/OrderDetails")]
public class OrderDetailsController : ControllerBase
{
    private static readonly List<OrderDocumentItem> Orders =
    [
        new()
        {
            Id = "1001",
            Status = "pending",
            StatusText = "待支付",
            CreateTime = "2026-03-25 18:30:00",
            PayTime = null,
            ShippingTime = null,
            CompleteTime = null,
            TotalPrice = 198.00m,
            ShippingAddress = new ShippingAddressResponse
            {
                Name = "张三",
                Phone = "13800138000",
                Address = "北京市朝阳区某某街道123号"
            },
            Items =
            [
                new OrderItemResponse
                {
                    Id = "1",
                    Name = "红烧肉",
                    Price = 68.00m,
                    Quantity = 1,
                    Image = "https://example.com/images/dish1.jpg"
                },
                new OrderItemResponse
                {
                    Id = "2",
                    Name = "清炒时蔬",
                    Price = 28.00m,
                    Quantity = 1,
                    Image = "https://example.com/images/dish2.jpg"
                }
            ],
            PaymentMethod = null,
            TransactionId = null
        },
        new()
        {
            Id = "1002",
            Status = "shipping",
            StatusText = "待收货",
            CreateTime = "2026-03-24 10:00:00",
            PayTime = "2026-03-24 10:05:00",
            ShippingTime = "2026-03-24 14:30:00",
            CompleteTime = null,
            TotalPrice = 88.00m,
            ShippingAddress = new ShippingAddressResponse
            {
                Name = "李四",
                Phone = "13900139000",
                Address = "广东省深圳市南山区科技园1号"
            },
            Items =
            [
                new OrderItemResponse
                {
                    Id = "3",
                    Name = "番茄炒蛋",
                    Price = 32.00m,
                    Quantity = 1,
                    Image = "https://example.com/images/dish3.jpg"
                },
                new OrderItemResponse
                {
                    Id = "4",
                    Name = "小炒黄牛肉",
                    Price = 56.00m,
                    Quantity = 1,
                    Image = "https://example.com/images/dish4.jpg"
                }
            ],
            PaymentMethod = "wechat",
            TransactionId = "wx202603241005000000000001"
        },
        new()
        {
            Id = "1003",
            Status = "refund",
            StatusText = "Refunding",
            CreateTime = "2026-03-23 09:20:00",
            PayTime = "2026-03-23 09:25:00",
            ShippingTime = null,
            CompleteTime = null,
            TotalPrice = 126.00m,
            ShippingAddress = new ShippingAddressResponse
            {
                Name = "Wang Wu",
                Phone = "13700137000",
                Address = "Shanghai Pudong New Area No. 88"
            },
            Items =
            [
                new OrderItemResponse
                {
                    Id = "5",
                    Name = "Organic Lettuce",
                    Price = 36.00m,
                    Quantity = 1,
                    Image = "https://example.com/images/dish5.jpg"
                },
                new OrderItemResponse
                {
                    Id = "6",
                    Name = "Fresh Corn",
                    Price = 45.00m,
                    Quantity = 2,
                    Image = "https://example.com/images/dish6.jpg"
                }
            ],
            PaymentMethod = "wechat",
            TransactionId = "wx202603230925000000000002"
        },
        new()
        {
            Id = "1004",
            Status = "aftersale",
            StatusText = "After-sale",
            CreateTime = "2026-03-22 16:40:00",
            PayTime = "2026-03-22 16:45:00",
            ShippingTime = "2026-03-23 08:30:00",
            CompleteTime = null,
            TotalPrice = 219.00m,
            ShippingAddress = new ShippingAddressResponse
            {
                Name = "Zhao Liu",
                Phone = "13600136000",
                Address = "Guangzhou Tianhe District No. 66"
            },
            Items =
            [
                new OrderItemResponse
                {
                    Id = "7",
                    Name = "Tomato Scrambled Eggs",
                    Price = 39.00m,
                    Quantity = 1,
                    Image = "https://example.com/images/dish7.jpg"
                },
                new OrderItemResponse
                {
                    Id = "8",
                    Name = "Braised Pork",
                    Price = 90.00m,
                    Quantity = 2,
                    Image = "https://example.com/images/dish8.jpg"
                }
            ],
            PaymentMethod = "wechat",
            TransactionId = "wx202603221645000000000003"
        }
    ];

    [HttpGet]
    public IActionResult List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "createTime",
        [FromQuery] string? sortOrder = "desc")
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : pageSize;

        IEnumerable<OrderDocumentItem> query = Orders;

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);
            query = query.Where(x => string.Equals(x.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase));
        }

        var byPrice = string.Equals(sortBy, "price", StringComparison.OrdinalIgnoreCase);
        var asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        query = byPrice
            ? asc ? query.OrderBy(x => x.TotalPrice) : query.OrderByDescending(x => x.TotalPrice)
            : asc ? query.OrderBy(x => x.CreateTime) : query.OrderByDescending(x => x.CreateTime);

        var total = query.Count();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.Id,
                status = x.Status,
                statusText = x.StatusText,
                createTime = x.CreateTime,
                totalPrice = x.TotalPrice,
                items = x.Items
            })
            .ToList();

        return Ok(ApiResult.Success(new
        {
            orders = items,
            total,
            page,
            pageSize,
            hasMore = page * pageSize < total
        }));
    }

    [HttpGet("{id}")]
    public IActionResult Detail(string id)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        return Ok(ApiResult.Success(new
        {
            order = new
            {
                id = order.Id,
                status = order.Status,
                statusText = order.StatusText,
                createTime = order.CreateTime,
                payTime = order.PayTime,
                shippingTime = order.ShippingTime,
                completeTime = order.CompleteTime,
                totalPrice = order.TotalPrice,
                shippingAddress = order.ShippingAddress,
                items = order.Items,
                paymentMethod = order.PaymentMethod,
                transactionId = order.TransactionId
            }
        }));
    }

    [HttpPost("{id}/pay")]
    public IActionResult Pay(string id, [FromBody] OrderDetailsPayRequest? request)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        if (!string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(ApiResult.Fail("订单状态错误", 409));
        }

        order.Status = "paid";
        order.StatusText = "已支付";
        order.PayTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        order.PaymentMethod = request?.PaymentMethod ?? "wechat";
        order.TransactionId = $"wx{DateTime.Now:yyyyMMddHHmmss}000000000000";

        return Ok(ApiResult.Success(new
        {
            orderId = order.Id,
            status = order.Status,
            statusText = order.StatusText,
            paymentInfo = new
            {
                prepayId = $"wx{DateTime.Now:yyyyMMddHHmmss}000000000000",
                timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString(),
                nonceStr = "abcdefg123456",
                package = $"prepay_id=wx{DateTime.Now:yyyyMMddHHmmss}000000000000",
                signType = "MD5",
                paySign = Guid.NewGuid().ToString("N")
            }
        }));
    }

    [HttpPost("{id}/cancel")]
    public IActionResult Cancel(string id, [FromBody] OrderDetailsCancelRequest? request)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        if (!string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(ApiResult.Fail("订单状态错误", 409));
        }

        order.Status = "cancelled";
        order.StatusText = "已取消";

        return Ok(ApiResult.Success(new
        {
            orderId = order.Id,
            status = order.Status,
            statusText = order.StatusText
        }));
    }

    [HttpPost("{id}/confirm")]
    public IActionResult Confirm(string id)
    {
        var order = Orders.FirstOrDefault(x => x.Id == id);
        if (order is null)
        {
            return Ok(ApiResult.Fail("订单不存在", 404));
        }

        if (!string.Equals(order.Status, "shipping", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(ApiResult.Fail("订单状态错误", 409));
        }

        order.Status = "completed";
        order.StatusText = "已完成";
        order.CompleteTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return Ok(ApiResult.Success(new
        {
            orderId = order.Id,
            status = order.Status,
            statusText = order.StatusText
        }));
    }

    public sealed class OrderDetailsPayRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal PayAmount { get; set; }
    }

    public sealed class OrderDetailsCancelRequest
    {
        public string? Reason { get; set; }
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "待付款" => "pending",
            "pendingpayment" => "pending",
            "paid" => "paid",
            "待发货" => "paid",
            "待收货" => "shipping",
            "receiving" => "shipping",
            "退款" => "refund",
            "refunding" => "refund",
            "售后" => "aftersale",
            "aftersale" => "aftersale",
            "aftersales" => "aftersale",
            "aftersaleorder" => "aftersale",
            "afterservice" => "aftersale",
            "after-sale" => "aftersale",
            "after_sale" => "aftersale",
            "completed" => "completed",
            "已完成" => "completed",
            "cancelled" => "cancelled",
            "已取消" => "cancelled",
            _ => status.Trim().ToLowerInvariant()
        };
    }

    private sealed class OrderDocumentItem
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string CreateTime { get; set; } = string.Empty;
        public string? PayTime { get; set; }
        public string? ShippingTime { get; set; }
        public string? CompleteTime { get; set; }
        public decimal TotalPrice { get; set; }
        public ShippingAddressResponse ShippingAddress { get; set; } = new();
        public List<OrderItemResponse> Items { get; set; } = [];
        public string? PaymentMethod { get; set; }
        public string? TransactionId { get; set; }
    }

    private sealed class ShippingAddressResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    private sealed class OrderItemResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;
    }
}
