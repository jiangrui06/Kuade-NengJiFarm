using System.Data.Common;
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/pay")]
public class PayController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public PayController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("methods")]
    public IActionResult GetMethods()
    {
        return Ok(ApiResult.Success(new[]
        {
            new
            {
                id = 1,
                name = "微信支付",
                icon = "wechat-pay",
                description = "推荐使用微信支付，安全快捷"
            },
            new
            {
                id = 2,
                name = "余额支付",
                icon = "wallet",
                description = "使用账户余额完成支付"
            }
        }));
    }

    [AllowAnonymous]
    [HttpGet("info")]
    public async Task<IActionResult> GetInfo([FromQuery] long? orderId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = TryGetCurrentUserId();
            var order = await LoadOrderInfoAsync(orderId, userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("未找到待支付订单", 404));
            }

            var orderItems = await LoadOrderItemsAsync(order.OrderId, cancellationToken);
            var discountAmount = Math.Max(0, order.TotalAmount - order.ActualAmount);

            return Ok(ApiResult.Success(new
            {
                orderId = order.OrderId,
                orderNumber = order.OrderNumber,
                totalAmount = order.TotalAmount,
                actualAmount = order.ActualAmount,
                discountAmount,
                discountInfo = discountAmount > 0 ? "收益优惠" : "暂无优惠",
                paymentStatus = order.PaymentStatus,
                paymentMethod = order.PaymentMethod,
                paymentTime = order.PaymentTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                userInfo = new
                {
                    name = string.IsNullOrWhiteSpace(order.UserName) ? order.ContactPerson : order.UserName,
                    phone = MaskPhone(string.IsNullOrWhiteSpace(order.UserPhone) ? order.ContactNumber : order.UserPhone)
                },
                addressInfo = new
                {
                    contactPerson = order.ContactPerson,
                    contactNumber = MaskPhone(order.ContactNumber),
                    shippingAddress = order.ShippingAddress
                },
                orderItems
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取支付信息失败：{ex.Message}"));
        }
    }

    [AllowAnonymous]
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmPayRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.OrderId <= 0 || (request.PaymentMethod != 1 && request.PaymentMethod != 2))
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var userId = TryGetCurrentUserId();
            var order = await LoadOrderInfoAsync(request.OrderId, userId, cancellationToken);
            if (order is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            if (order.PaymentStatus == 1)
            {
                return Ok(ApiResult.Success(new
                {
                    orderId = order.OrderId,
                    paymentStatus = 1,
                    paymentTime = order.PaymentTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    paymentAmount = order.ActualAmount,
                    paymentMethod = order.PaymentMethod == 0 ? request.PaymentMethod : order.PaymentMethod
                }, "支付成功"));
            }

            await UpdatePaymentAsync(request.OrderId, request.PaymentMethod, cancellationToken);
            var status = await LoadPaymentStatusAsync(request.OrderId, userId, cancellationToken);
            if (status is null)
            {
                return Ok(ApiResult.Fail("支付结果查询失败", 500));
            }

            return Ok(ApiResult.Success(new
            {
                orderId = status.OrderId,
                paymentStatus = status.PaymentStatus,
                paymentTime = status.PaymentTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                paymentAmount = status.PaymentAmount,
                paymentMethod = status.PaymentMethod
            }, "支付成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"确认支付失败：{ex.Message}"));
        }
    }

    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] long orderId, CancellationToken cancellationToken)
    {
        try
        {
            if (orderId <= 0)
            {
                return Ok(ApiResult.Fail("orderId 参数不正确", 400));
            }

            var userId = TryGetCurrentUserId();
            var status = await LoadPaymentStatusAsync(orderId, userId, cancellationToken);
            if (status is null)
            {
                return Ok(ApiResult.Fail("订单不存在", 404));
            }

            return Ok(ApiResult.Success(new
            {
                orderId = status.OrderId,
                paymentStatus = status.PaymentStatus,
                paymentTime = status.PaymentTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                paymentAmount = status.PaymentAmount,
                paymentMethod = status.PaymentMethod
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"查询支付状态失败：{ex.Message}"));
        }
    }

    private async Task<OrderPayInfo?> LoadOrderInfoAsync(long? orderId, int? userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                o.order_id,
                o.order_number,
                o.total_order_amount,
                o.actual_payment,
                o.payment_status,
                o.payment_time,
                o.payment_methods,
                o.contact_person,
                o.contact_number,
                o.shipping_address,
                u.wx_name,
                u.phone_number,
                sa.contact_name,
                sa.province,
                sa.city,
                sa.municipal_district,
                sa.town,
                sa.house_number
            FROM orders o
            LEFT JOIN User u ON u.user_id = o.user_id
            LEFT JOIN shipping_address sa ON sa.address_id = o.address_id
            /**where**/
            /**order**/
            LIMIT 1;
            """;

        var conditions = new List<string>();
        if (orderId.HasValue)
        {
            conditions.Add("o.order_id = @orderId");
        }
        else if (userId.HasValue)
        {
            conditions.Add("o.user_id = @userId");
        }

        var orderBy = orderId.HasValue
            ? "ORDER BY o.order_id DESC"
            : "ORDER BY (CASE WHEN o.payment_status = 0 THEN 0 ELSE 1 END), o.order_creation_time DESC, o.order_id DESC";

        var finalSql = sql
            .Replace("/**where**/", conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : string.Empty)
            .Replace("/**order**/", orderBy);

        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = finalSql;
        AddParameter(command, "@orderId", orderId);
        AddParameter(command, "@userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var contactName = GetString(reader, "contact_name");
        var province = GetString(reader, "province");
        var city = GetString(reader, "city");
        var district = GetString(reader, "municipal_district");
        var town = GetString(reader, "town");
        var house = GetString(reader, "house_number");
        var addressText = string.Concat(province, city, district, town, house);

        return new OrderPayInfo
        {
            OrderId = GetInt64(reader, "order_id"),
            OrderNumber = GetString(reader, "order_number"),
            TotalAmount = GetDecimal(reader, "total_order_amount"),
            ActualAmount = GetDecimal(reader, "actual_payment"),
            PaymentStatus = GetInt32(reader, "payment_status"),
            PaymentMethod = GetInt32(reader, "payment_methods"),
            PaymentTime = GetNullableDateTime(reader, "payment_time"),
            ContactPerson = string.IsNullOrWhiteSpace(contactName) ? GetString(reader, "contact_person") : contactName,
            ContactNumber = GetString(reader, "contact_number"),
            ShippingAddress = string.IsNullOrWhiteSpace(addressText) ? GetString(reader, "shipping_address") : addressText,
            UserName = GetString(reader, "wx_name"),
            UserPhone = GetString(reader, "phone_number")
        };
    }

    private async Task<List<object>> LoadOrderItemsAsync(long orderId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                od.commodity_id,
                COALESCE(c.product_name, CONCAT('商品', od.commodity_id)) AS product_name,
                od.unit_price,
                od.actual_unit_price,
                od.purchase_quantity,
                od.subtotal_amount
            FROM order_details od
            LEFT JOIN commodity c ON c.commodity_id = od.commodity_id
            WHERE od.order_id = @orderId
            ORDER BY od.order_details_id ASC;
            """;

        var items = new List<object>();
        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@orderId", orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var price = GetDecimal(reader, "unit_price");
            var actualPrice = GetDecimal(reader, "actual_unit_price");
            var count = GetInt32(reader, "purchase_quantity");
            items.Add(new
            {
                commodityId = GetInt32(reader, "commodity_id"),
                name = GetString(reader, "product_name"),
                price,
                actualPrice,
                count,
                subtotal = GetDecimal(reader, "subtotal_amount")
            });
        }

        return items;
    }

    private async Task<PayStatusInfo?> LoadPaymentStatusAsync(long orderId, int? userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                o.order_id,
                o.payment_status,
                o.payment_time,
                o.actual_payment,
                o.payment_methods
            FROM orders o
            WHERE o.order_id = @orderId
            /**user**/
            LIMIT 1;
            """;

        var userFilter = userId.HasValue ? "AND o.user_id = @userId" : string.Empty;
        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql.Replace("/**user**/", userFilter);
        AddParameter(command, "@orderId", orderId);
        AddParameter(command, "@userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PayStatusInfo
        {
            OrderId = GetInt64(reader, "order_id"),
            PaymentStatus = GetInt32(reader, "payment_status"),
            PaymentTime = GetNullableDateTime(reader, "payment_time"),
            PaymentAmount = GetDecimal(reader, "actual_payment"),
            PaymentMethod = GetInt32(reader, "payment_methods")
        };
    }

    private async Task UpdatePaymentAsync(long orderId, int paymentMethod, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE orders
            SET
                payment_status = 1,
                payment_methods = @paymentMethod,
                payment_time = @paymentTime,
                order_status = CASE WHEN order_status = 0 THEN 1 ELSE order_status END
            WHERE order_id = @orderId AND payment_status <> 1;
            """;

        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@orderId", orderId);
        AddParameter(command, "@paymentMethod", paymentMethod);
        AddParameter(command, "@paymentTime", DateTime.Now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private int? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static int GetInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static long GetInt64(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static decimal GetDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? GetNullableDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7)
        {
            return phone;
        }

        return $"{phone[..3]}****{phone[^4..]}";
    }

    private sealed class OrderPayInfo
    {
        public long OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public int PaymentStatus { get; set; }
        public int PaymentMethod { get; set; }
        public DateTime? PaymentTime { get; set; }
        public string ContactPerson { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
    }

    private sealed class PayStatusInfo
    {
        public long OrderId { get; set; }
        public int PaymentStatus { get; set; }
        public DateTime? PaymentTime { get; set; }
        public decimal PaymentAmount { get; set; }
        public int PaymentMethod { get; set; }
    }

    public sealed class ConfirmPayRequest
    {
        public long OrderId { get; set; }
        public int PaymentMethod { get; set; }
        public string Remark { get; set; } = string.Empty;
    }
}
