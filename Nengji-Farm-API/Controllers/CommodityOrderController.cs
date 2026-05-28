using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/commodity-order")]
public class CommodityOrderController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";
    private readonly AppDbContext _dbContext;
    private readonly IInventoryService _inventoryService;

    public CommodityOrderController(AppDbContext dbContext, IInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateCommodityOrderRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.Items.Count == 0)
        {
            return Ok(ApiResult.Fail("请求参数不正确", 400));
        }

        var deliveryMethod = NormalizeDeliveryMethod(request.DeliveryMethod);
        var userId = ResolveCurrentUserId();

        ShippingAddress? address = null;
        if (deliveryMethod == "express")
        {
            if (request.AddressId <= 0)
                return Ok(ApiResult.Fail("快递配送请填写收货地址", 400));

            address = await ResolveShippingAddressAsync(userId, request.AddressId, cancellationToken);
            if (address is null)
                return Ok(ApiResult.Fail("收货地址不存在", 400));
        }

        var items = request.Items
            .Where(x => int.TryParse(x.Id, out var pid) && pid > 0 && x.Quantity > 0)
            .Select(x => new { CommodityId = int.Parse(x.Id), Quantity = Math.Max(1, x.Quantity) })
            .ToList();
        if (items.Count == 0)
        {
            return Ok(ApiResult.Fail("items 不能为空", 400));
        }

        var commodityIds = items.Select(x => x.CommodityId).Distinct().ToList();
        var commodities = await _dbContext.Commodities
            .Where(x => commodityIds.Contains(x.CommodityId) && (x.ProductStatus ?? 0) == 1)
            .ToListAsync(cancellationToken);
        var commodityMap = commodities.ToDictionary(x => x.CommodityId);

        if (commodityMap.Count == 0 || items.Any(x => !commodityMap.ContainsKey(x.CommodityId)))
        {
            return Ok(ApiResult.Fail("commodity not found", 404));
        }

        var normalizedItems = items.Select(x =>
        {
            var unitPrice = commodityMap.TryGetValue(x.CommodityId, out var row) ? row.UnitPrice : null;
            var price = unitPrice.HasValue && unitPrice.Value > 0 ? unitPrice.Value : 0m;
            return new { x.CommodityId, Price = price, x.Quantity, Name = commodityMap[x.CommodityId].ProductName };
        }).ToList();

        if (normalizedItems.Any(x => x.Price <= 0))
        {
            return Ok(ApiResult.Fail("商品价格异常", 409));
        }

        var totalAmount = normalizedItems.Sum(x => x.Price * x.Quantity);
        var now = DateTime.Now;
        var verifyCode = deliveryMethod == "pickup" ? GenerateVerifyCode() : null;

        var order = new CommodityOrder
        {
            OrderNo = GenerateCommodityOrderNo(),
            WxPayNo = null,
            TotalAmount = totalAmount,
            TotalQuantity = normalizedItems.Sum(x => x.Quantity),
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now,
            AddressId = address?.AddressId,
            TrackingNumber = null,
            TrackingTypeId = null,
            DeliveryMethod = deliveryMethod,
            VerifyCode = verifyCode
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 原子扣减库存
        var deductResult = await _inventoryService.DeductBatchAsync(
            ProductType.Commodity,
            normalizedItems.Select(x => (x.CommodityId, x.Quantity, x.Name)).ToList());
        if (!deductResult.Success)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Ok(ApiResult.Fail(deductResult.ErrorMessage!, 409));
        }

        _dbContext.CommodityOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in normalizedItems)
        {
            _dbContext.CommodityOrderDetails.Add(new CommodityOrderDetail
            {
                OrderId = order.OrderId,
                CommodityId = item.CommodityId,
                GoodsName = item.Name,
                ImageUrl = commodityMap[item.CommodityId].ImageUrl ?? string.Empty,
                UnitPrice = item.Price,
                Quantity = item.Quantity,
                SubtotalAmount = item.Price * item.Quantity,
                StatusId = 1
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            id = order.OrderNo,
            orderId = order.OrderNo,
            orderNumber = order.OrderNo,
            orderType = "goods",
            status = "pending",
            deliveryMethod,
            verifyCode,
            totalPrice = order.TotalAmount,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        }));
    }

    private int ResolveCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private async Task<ShippingAddress?> ResolveShippingAddressAsync(int userId, int addressId, CancellationToken cancellationToken)
    {
        var query = _dbContext.ShippingAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var matched = await query.FirstOrDefaultAsync(x => x.AddressId == addressId, cancellationToken);
        if (matched is not null)
        {
            return matched;
        }

        return await query
            .OrderByDescending(x => EF.Property<bool>(x, DefaultFlagProperty))
            .ThenByDescending(x => x.AddressId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string GenerateCommodityOrderNo()
    {
        return $"GOODS{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static string NormalizeDeliveryMethod(string? method)
    {
        var m = (method ?? "express").Trim().ToLowerInvariant();
        return m is "pickup" or "self_pickup" or "self-pickup" or "到店自取" or "自取" ? "pickup" : "express";
    }

    private static string GenerateVerifyCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new char[12];
        for (int i = 0; i < 12; i++)
            code[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(code);
    }

    public sealed class CreateCommodityOrderRequest
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int AddressId { get; set; }
        public List<CreateCommodityOrderItem> Items { get; set; } = [];
        public string? DeliveryMethod { get; set; }
    }

    public sealed class CreateCommodityOrderItem
    {
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Id { get; set; } = string.Empty;

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Quantity { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal Price { get; set; }
    }
}

