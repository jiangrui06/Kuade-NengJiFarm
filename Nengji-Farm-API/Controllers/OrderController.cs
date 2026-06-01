using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/order")]
public class OrderController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";
    private readonly AppDbContext _dbContext;
    private readonly IInventoryStatsService _inventoryStatsService;
    private readonly IInventoryService _inventoryService;

    public OrderController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService, IInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// 获取当前可用桌台列表（点餐页公共接口，无需登录）
    /// </summary>
    [AllowAnonymous]
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables(CancellationToken cancellationToken)
    {
        try
        {
            var tables = await _dbContext.DiningTables
                .AsNoTracking()
                .Where(x => x.TableStatusId != 3 && x.TableStatusId != 2) // 过滤停用+删除
                .OrderBy(x => x.DiningTableId)
                .Select(x => new
                {
                    id = x.DiningTableId,
                    name = FormatTableName(x.TableNo),
                    status = "occupied",
                    statusText = "使用中",
                    statusId = x.TableStatusId
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(tables));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail("获取桌台列表失败：" + ex.Message));
        }
    }

    /// <summary>
    /// 获取所有桌台二维码列表（无需认证）
    /// </summary>
    [AllowAnonymous]
    [HttpGet("table-qrcodes")]
    public async Task<IActionResult> GetTableQrCodes(CancellationToken cancellationToken)
    {
        var tables = await _dbContext.DiningTables
            .AsNoTracking()
            .OrderBy(x => x.DiningTableId)
            .ToListAsync(cancellationToken);

        if (tables.Count == 0)
        {
            return Ok(ApiResult.Fail("暂无桌台数据", 404));
        }

        var result = new List<object>();
        foreach (var table in tables)
        {
            var url = BuildTableQrUrl(table.DiningTableId);
            var qrBase64 = GenerateQrCodeBase64(url);

            result.Add(new
            {
                tableId = table.DiningTableId,
                tableNo = table.TableNo,
                tableName = FormatTableName(table.TableNo),
                url,
                qrCode = qrBase64
            });
        }

        return Ok(ApiResult.Success(result));
    }

    /// <summary>
    /// 获取单个桌台二维码图片（无需认证，直接返回 PNG）
    /// </summary>
    [AllowAnonymous]
    [HttpGet("table-qrcode/{tableId:int}")]
    public async Task<IActionResult> GetTableQrCode(int tableId, CancellationToken cancellationToken)
    {
        if (tableId <= 0)
        {
            return Ok(ApiResult.Fail("桌台 ID 不正确", 400));
        }

        var table = await _dbContext.DiningTables
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DiningTableId == tableId, cancellationToken);

        if (table is null)
        {
            return Ok(ApiResult.Fail("桌台不存在", 404));
        }

        var url = BuildTableQrUrl(table.DiningTableId);
        var qrBytes = GenerateQrCodeBytes(url);

        return File(qrBytes, "image/png");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetPageData(
        [FromQuery] string categoryId = "vegetables",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var categories = await LoadMenuCategoriesAsync(cancellationToken);
        var currentCategory = categories.Any(x => x.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase))
            ? categoryId
            : categories.FirstOrDefault()?.Id ?? "vegetables";
        var categoryMap = categories.ToDictionary(x => x.CategoryId, x => x.Id);

        var query = _dbContext.Commodities.AsNoTracking().Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1);
        var commodities = await query.OrderBy(x => x.CategoryId).ThenBy(x => x.CommodityId).ToListAsync(cancellationToken);
        var stats = await _inventoryStatsService.GetCommodityStatsAsync(commodities.Select(x => x.CommodityId), cancellationToken);
        var unitMap = await _dbContext.Units.AsNoTracking().Where(u => u.IsEnabled == 1).ToDictionaryAsync(u => u.UnitId, u => u.UnitName, cancellationToken);

        var goods = commodities
            .Where(x => categoryMap.TryGetValue(x.CategoryId, out var key) && key == currentCategory)
            .Select(x => BuildMenuGoods(x, stats.GetValueOrDefault(x.CommodityId), x.UnitId.HasValue ? unitMap.GetValueOrDefault(x.UnitId.Value) : null))
            .ToList();
        var total = goods.Count;
        var pageGoods = goods.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(ApiResult.Success(new
        {
            currentCategory,
            categories = categories.Select(x => new { id = x.Id, name = x.Name }),
            goodsList = pageGoods,
            page,
            pageSize,
            total,
            hasMore = page * pageSize < total
        }));
    }

    [AllowAnonymous]
    [HttpGet("getOrderData")]
    public async Task<IActionResult> GetOrderData(CancellationToken cancellationToken)
    {
        var categories = await LoadMenuCategoriesAsync(cancellationToken);
        var categoryMap = categories.ToDictionary(x => x.CategoryId, x => x.Id);
        var commodities = await _dbContext.Commodities.AsNoTracking().Where(x => x.IsDelete == 0 && (x.ProductStatus ?? 0) == 1).ToListAsync(cancellationToken);
        var stats = await _inventoryStatsService.GetCommodityStatsAsync(commodities.Select(x => x.CommodityId), cancellationToken);
        var unitMap = await _dbContext.Units.AsNoTracking().Where(u => u.IsEnabled == 1).ToDictionaryAsync(u => u.UnitId, u => u.UnitName, cancellationToken);
        var goodsList = commodities
            .GroupBy(x => categoryMap.TryGetValue(x.CategoryId, out var key) ? key : $"category-{x.CategoryId}")
            .ToDictionary(g => g.Key, g => g.Select(x => BuildMenuGoods(x, stats.GetValueOrDefault(x.CommodityId), x.UnitId.HasValue ? unitMap.GetValueOrDefault(x.UnitId.Value) : null)).ToList());

        return Ok(ApiResult.Success(new { data = new { data = new { categories, goodsList } } }));
    }

    [AllowAnonymous]
    [HttpPost("updateGoodsQuantity")]
    public IActionResult UpdateGoodsQuantity([FromBody] UpdateGoodsQuantityRequest? request)
    {
        return Ok(ApiResult.Success(new { updated = request?.Updates?.Count ?? 0 }));
    }

    [HttpGet("status-list")]
    public IActionResult GetStatusList()
    {
        return Ok(ApiResult.Success(new[]
        {
            new { value = "all", label = "All" },
            new { value = "pending", label = "Pending" },
            new { value = "paid", label = "Paid" },
            new { value = "shipping", label = "Shipping" },
            new { value = "completed", label = "Completed" },
            new { value = "cancelled", label = "Cancelled" }
        }));
    }

    [HttpPost("create")]
    [HttpPost("getOrderData/create")]
    [HttpPost("create-payment-order")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Ok(ApiResult.Fail("request body is required", 400));
        }

        var userId = GetCurrentUserId();
        if (IsCommodityOrderRequest(request))
        {
            return await CreateCommodityOrderAsync(userId, request, cancellationToken);
        }

        if (IsFoodOrderRequest(request))
        {
            return await CreateDishOrderAsync(userId, request, cancellationToken);
        }

        request.SourceType = "goods";
        return await CreateCommodityOrderAsync(userId, request, cancellationToken);
    }

    [HttpGet("list")]
    [HttpGet("getOrderData/list")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);
        var userId = GetCurrentUserId();
        var query = _dbContext.CommodityOrders.AsNoTracking().Where(x => x.UserId == userId);
        query = ApplyOrderStatusFilter(query, status);
        var total = await query.CountAsync(cancellationToken);
        var orders = await query.OrderByDescending(x => x.CreateTime).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var itemMap = await LoadCommodityOrderItemsAsync(orders.Select(x => x.OrderId).ToList(), cancellationToken);

        var list = orders.Select(x => BuildCommodityOrderListItem(x, itemMap)).ToList();

        return Ok(ApiResult.Success(new
        {
            orders = list,
            total,
            page,
            pageSize,
            hasMore = page * pageSize < total
        }));
    }

    [HttpGet("info")]
    [HttpGet("detail")]
    [HttpGet("getOrderData/detail")]
    [HttpGet("getOrderData/{orderId:long}")]
    public async Task<IActionResult> Detail([FromQuery] long orderId, [FromRoute] long routeOrderId, CancellationToken cancellationToken)
    {
        orderId = orderId > 0 ? orderId : routeOrderId;
        if (orderId <= 0)
        {
            return Ok(ApiResult.Fail("orderId is invalid", 400));
        }

        var userId = GetCurrentUserId();
        var commodityOrder = await _dbContext.CommodityOrders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
        if (commodityOrder is not null)
        {
            var itemMap = await LoadCommodityOrderItemsAsync([commodityOrder.OrderId], cancellationToken);
            var orderView = BuildCommodityOrderListItem(commodityOrder, itemMap);
            return Ok(ApiResult.Success(new { totalAmount = commodityOrder.TotalAmount, order = orderView }));
        }

        var dishOrder = await _dbContext.DishOrders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
        if (dishOrder is not null)
        {
            await EnsureDishStatusCacheAsync();
            return Ok(ApiResult.Success(new { totalAmount = dishOrder.TotalAmount, order = BuildDishOrderListItem(dishOrder) }));
        }

        var activityOrder = await _dbContext.ActivityOrders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
        if (activityOrder is not null)
        {
            return Ok(ApiResult.Success(new { totalAmount = activityOrder.TotalAmount, order = BuildActivityOrderListItem(activityOrder) }));
        }

        return Ok(ApiResult.Fail("order not found", 404));
    }

    [HttpPut("cancel")]
    [HttpPost("{id:long}/cancel")]
    [HttpPost("getOrderData/cancel/{id:long}")]
    public async Task<IActionResult> Cancel(long id, [FromBody] CancelOrderRequest? request, CancellationToken cancellationToken)
    {
        var orderId = request?.OrderId > 0 ? request.OrderId : id;
        if (orderId <= 0)
        {
            return Ok(ApiResult.Fail("orderId is invalid", 400));
        }

        var userId = GetCurrentUserId();
        var commodityOrder = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
        if (commodityOrder is not null)
        {
            if (commodityOrder.OrderStatusId != 1)
            {
                return Ok(ApiResult.Fail("paid order cannot be cancelled", 409));
            }

            // 恢复商品库存
            var details = await _dbContext.CommodityOrderDetails
                .Where(x => x.OrderId == commodityOrder.OrderId)
                .ToListAsync(cancellationToken);
            foreach (var d in details)
            {
                await _inventoryService.RestoreAsync(ProductType.Commodity, d.CommodityId, d.Quantity);
            }

            commodityOrder.OrderStatusId = 5;
            await SyncCommodityDetailStatusAsync(commodityOrder.OrderId, 5, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success(new { orderId = commodityOrder.OrderId.ToString(), status = "cancelled" }));
        }

        var dishOrder = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
        if (dishOrder is not null)
        {
            if (dishOrder.OrderStatusId != 1)
            {
                return Ok(ApiResult.Fail("paid order cannot be cancelled", 409));
            }

            // 恢复菜品库存
            var dishDetails = await _dbContext.DishOrderDetails
                .Where(x => x.DishOrderId == dishOrder.OrderId)
                .ToListAsync(cancellationToken);
            foreach (var d in dishDetails)
            {
                await _inventoryService.RestoreAsync(ProductType.Dish, d.DishId, d.Quantity);
            }

            dishOrder.OrderStatusId = 4;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await TryFreeTableAsync(dishOrder.DiningTableId, cancellationToken);
            return Ok(ApiResult.Success(new { orderId = dishOrder.OrderId.ToString(), status = "cancelled" }));
        }

        var activityOrder = await _dbContext.ActivityOrders.FirstOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, cancellationToken);
        if (activityOrder is not null)
        {
            if (activityOrder.OrderStatusId != 1)
            {
                return Ok(ApiResult.Fail("paid order cannot be cancelled", 409));
            }
            activityOrder.OrderStatusId = 4;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success(new { orderId = activityOrder.OrderId.ToString(), status = "cancelled" }));
        }

        return Ok(ApiResult.Fail("order not found", 404));
    }

    [HttpPost("{id:long}/pay")]
    [HttpPost("getOrderData/pay/{id:long}")]
    public async Task<IActionResult> Pay(long id, [FromBody] PayOrderRequest? request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var commodityOrder = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == userId, cancellationToken);
        if (commodityOrder is not null)
        {
            if (commodityOrder.OrderStatusId != 1)
                return Ok(ApiResult.Success(new { orderId = commodityOrder.OrderId.ToString(), status = "paid", statusText = "Paid" }));
            var paidStatusId = commodityOrder.DeliveryMethod == "pickup" ? 8 : 2;
            commodityOrder.OrderStatusId = paidStatusId;
            commodityOrder.WxPayNo = $"MOCK_{DateTime.Now:yyyyMMddHHmmssfff}";
            await SyncCommodityDetailStatusAsync(commodityOrder.OrderId, paidStatusId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success(new { orderId = commodityOrder.OrderId.ToString(), status = "paid", statusText = "Paid" }));
        }

        var dishOrder = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == userId, cancellationToken);
        if (dishOrder is not null)
        {
            if (dishOrder.OrderStatusId != 1)
                return Ok(ApiResult.Success(new { orderId = dishOrder.OrderId.ToString(), status = "paid", statusText = "Paid" }));
            dishOrder.OrderStatusId = 2;
            dishOrder.WxPayNo = $"MOCK_{DateTime.Now:yyyyMMddHHmmssfff}";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success(new { orderId = dishOrder.OrderId.ToString(), status = "paid", statusText = "Paid" }));
        }

        return Ok(ApiResult.Fail("order not found", 404));
    }

    [HttpPost("getOrderData/confirm/{id:long}")]
    public async Task<IActionResult> Confirm(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var commodityOrder = await _dbContext.CommodityOrders.FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == userId, cancellationToken);
        if (commodityOrder is not null)
        {
            if (commodityOrder.OrderStatusId is 2 or 3)
            {
                commodityOrder.OrderStatusId = 4;
                await SyncCommodityDetailStatusAsync(commodityOrder.OrderId, 4, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return Ok(ApiResult.Success(new { orderId = commodityOrder.OrderNo, orderNumber = commodityOrder.OrderNo, orderNo = commodityOrder.OrderNo, status = "completed", statusText = "Completed" }));
        }

        var dishOrder = await _dbContext.DishOrders.FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == userId, cancellationToken);
        if (dishOrder is not null)
        {
            if (dishOrder.OrderStatusId is 2)
            {
                dishOrder.OrderStatusId = 3;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await TryFreeTableAsync(dishOrder.DiningTableId, cancellationToken);
            }
            return Ok(ApiResult.Success(new { orderId = dishOrder.OrderNo, orderNumber = dishOrder.OrderNo, orderNo = dishOrder.OrderNo, status = "completed", statusText = "Completed" }));
        }

        return Ok(ApiResult.Fail("order not found", 404));
    }

    private async Task<List<MenuCategoryItem>> LoadMenuCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Categories.AsNoTracking().Where(x => (x.CategoryStatusId ?? 0) == 1).OrderBy(x => x.SortOrder ?? int.MaxValue).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return rows.Select(x => new MenuCategoryItem
        {
            CategoryId = x.Id,
            Id = MapCategoryKey(x.Id, x.CategoryName),
            Name = x.CategoryName
        }).ToList();
    }

    private static string MapCategoryKey(int categoryId, string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("meat")) return "meat";
        if (lower.Contains("egg")) return "eggs";
        if (lower.Contains("milk") || lower.Contains("dairy")) return "dairy";
        if (lower.Contains("rice") || lower.Contains("staple")) return "staple";
        return categoryId switch
        {
            1 => "vegetables",
            2 => "meat",
            3 => "eggs",
            4 => "dairy",
            5 => "staple",
            _ => $"category-{categoryId}"
        };
    }

    private object BuildMenuGoods(Commodity commodity, CommodityInventoryStats? stats, string? unitName = null)
    {
        var image = NormalizeMediaUrl(commodity.ImageUrl);
        var spec = GoodsController.BuildSpec(commodity.WeightText, unitName);
        var description = GoodsController.ExtractDescription(commodity.SpecDescription, spec);
        return new
        {
            id = commodity.CommodityId.ToString(),
            name = commodity.ProductName,
            price = commodity.UnitPrice ?? 0m,
            image,
            detailImage = image,
            spec,
            description,
            type = commodity.CategoryId == 5 ? "acre" : "normal",
            sold = stats?.Sold ?? Math.Max(0, commodity.Quantity ?? 0),
            stock = stats?.Stock ?? (commodity.InStock ?? 0)
        };
    }

    private async Task<IActionResult> CreateCommodityOrderAsync(int userId, CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var items = await ResolveOrderItemsAsync(userId, request, cancellationToken);
        if (items.Count == 0)
        {
            return Ok(ApiResult.Fail("items cannot be empty", 400));
        }

        var addressInput = request.Address ?? request.AddressAlias ?? new ConfirmOrderAddress();
        var address = await ResolveAddressAsync(userId, request, addressInput, cancellationToken);
        if (address is null)
        {
            return Ok(ApiResult.Fail("address is required", 400));
        }

        var itemGroups = items
            .Where(x => x.GoodsId > 0 && x.Quantity > 0)
            .GroupBy(x => x.GoodsId)
            .Select(x => new NormalizedOrderItem
            {
                GoodsId = x.Key,
                Quantity = x.Sum(i => i.Quantity)
            })
            .ToList();

        if (itemGroups.Count == 0)
        {
            return Ok(ApiResult.Fail("items cannot be empty", 400));
        }

        var commodityIds = itemGroups.Select(x => x.GoodsId).ToList();
        var commodities = await _dbContext.Commodities
            .Where(x => x.IsDelete == 0 && commodityIds.Contains(x.CommodityId) && (x.ProductStatus ?? 0) == 1)
            .ToListAsync(cancellationToken);
        var commodityMap = commodities.ToDictionary(x => x.CommodityId);

        if (commodityMap.Count != itemGroups.Count)
        {
            return Ok(ApiResult.Fail("commodity not found", 404));
        }

        // 从数据库读取商品名称和价格，填充 itemGroups
        foreach (var item in itemGroups)
        {
            var commodity = commodityMap[item.GoodsId];
            var price = commodity.UnitPrice ?? 0m;
            if (price <= 0m)
            {
                return Ok(ApiResult.Fail("commodity price is invalid", 409));
            }

            item.Name = commodity.ProductName;
            item.Price = price;
            item.Image = NormalizeMediaUrl(commodity.ImageUrl);
        }

        var now = DateTime.Now;
        var order = new CommodityOrder
        {
            OrderNo = GenerateCommodityOrderNumber(),
            WxPayNo = null,
            TotalAmount = itemGroups.Sum(x => x.Price * x.Quantity),
            TotalQuantity = itemGroups.Sum(x => x.Quantity),
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now,
            AddressId = address.AddressId,
            TrackingNumber = null,
            TrackingTypeId = null
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 原子扣减库存（在事务内，任一失败回滚）
        var deductResult = await _inventoryService.DeductBatchAsync(
            ProductType.Commodity,
            itemGroups.Select(x => (x.GoodsId, x.Quantity, x.Name)).ToList());
        if (!deductResult.Success)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Ok(ApiResult.Fail(deductResult.ErrorMessage!, 409));
        }

        _dbContext.CommodityOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in itemGroups)
        {
            _dbContext.CommodityOrderDetails.Add(new CommodityOrderDetail
            {
                OrderId = order.OrderId,
                CommodityId = item.GoodsId,
                GoodsName = item.Name,
                ImageUrl = item.Image,
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
            orderNo = order.OrderNo,
            orderType = "goods",
            status = "pending",
            totalPrice = order.TotalAmount,
            amount = order.TotalAmount,
            quantity = order.TotalQuantity,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        }, "order created"));
    }

    private async Task<List<NormalizedOrderItem>> ResolveOrderItemsAsync(int userId, CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var rawItems = request.Items.Count > 0 ? request.Items : request.ItemsAlias;
        if (rawItems.Count > 0)
        {
            return rawItems.Select(x => new NormalizedOrderItem
            {
                GoodsId = ParseNumericId(FirstNonEmpty(ReadJsonValue(x.Id), ReadJsonValue(x.IdAlias))),
                Name = FirstNonEmpty(x.Name, x.NameAlias),
                Price = x.Price > 0 ? x.Price : x.UnitPriceAlias ?? 0m,
                Quantity = Math.Max(1, x.Quantity > 0 ? x.Quantity : x.CountAlias ?? 1),
                Image = FirstNonEmpty(x.Image, x.ImageAlias)
            }).Where(x => x.GoodsId > 0).ToList();
        }

        var cartIds = request.MergedCartIds;
        if (cartIds.Count > 0)
        {
            var carts = await _dbContext.ShippingCarts.AsNoTracking().Where(x => x.UserId == userId && cartIds.Contains(x.ShippingCartId)).ToListAsync(cancellationToken);
            var commodityIds = carts.Where(x => x.CommodityId.HasValue).Select(x => x.CommodityId!.Value).Distinct().ToList();
            var commodityMap = await _dbContext.Commodities.AsNoTracking().Where(x => x.IsDelete == 0 && commodityIds.Contains(x.CommodityId)).ToDictionaryAsync(x => x.CommodityId, cancellationToken);
            return carts.Where(x => x.CommodityId.HasValue && commodityMap.ContainsKey(x.CommodityId.Value)).Select(x =>
            {
                var commodity = commodityMap[x.CommodityId!.Value];
                return new NormalizedOrderItem
                {
                    GoodsId = commodity.CommodityId,
                    Name = commodity.ProductName,
                    Price = commodity.UnitPrice ?? 0m,
                    Quantity = Math.Max(1, x.CartQuantity),
                    Image = NormalizeMediaUrl(commodity.ImageUrl)
                };
            }).ToList();
        }

        var goodsId = request.GoodsId > 0 ? request.GoodsId : request.GoodsIdAlias;
        if (goodsId > 0)
        {
            var commodity = await _dbContext.Commodities.AsNoTracking().FirstOrDefaultAsync(x => x.IsDelete == 0 && x.CommodityId == goodsId, cancellationToken);
            if (commodity is null)
            {
                return [];
            }

            return [new NormalizedOrderItem
            {
                GoodsId = commodity.CommodityId,
                Name = commodity.ProductName,
                Price = commodity.UnitPrice ?? request.TotalPrice,
                Quantity = Math.Max(1, request.Count > 0 ? request.Count : request.Quantity),
                Image = NormalizeMediaUrl(commodity.ImageUrl)
            }];
        }

        return [];
    }

    private async Task<ShippingAddress?> ResolveAddressAsync(int userId, CreateOrderRequest request, ConfirmOrderAddress addressInput, CancellationToken cancellationToken)
    {
        var addressId = request.AddressId > 0 ? request.AddressId : request.AddressIdAlias;
        if (addressId <= 0)
        {
            addressId = addressInput.AddressId > 0 ? addressInput.AddressId : ParseNumericId(addressInput.Id);
        }

        var query = _dbContext.ShippingAddresses.AsNoTracking().Where(x => x.UserId == userId);
        if (addressId > 0)
        {
            var matched = await query.FirstOrDefaultAsync(x => x.AddressId == addressId, cancellationToken);
            if (matched is not null)
            {
                return matched;
            }
        }

        return await query.OrderByDescending(x => EF.Property<bool>(x, DefaultFlagProperty)).ThenByDescending(x => x.AddressId).FirstOrDefaultAsync(cancellationToken);
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("unauthorized");
    }

    private static string NormalizeMediaUrl(string? raw) => MediaUrlHelper.Normalize(raw);

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static int ParseNumericId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        if (int.TryParse(raw, out var id)) return id;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out id) ? id : 0;
    }

    private static string ReadJsonValue(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };
    }

    private static string GenerateCommodityOrderNumber()
    {
        return $"GOODS{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static string GenerateDishOrderNumber()
    {
        return $"DISH{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static bool IsCommodityOrderRequest(CreateOrderRequest request)
    {
        var sourceType = FirstNonEmpty(request.SourceType, request.SourceTypeAlias).ToLowerInvariant();
        return sourceType is "goods" or "commodity" or "cart";
    }

    private static bool IsFoodOrderRequest(CreateOrderRequest request)
    {
        var sourceType = FirstNonEmpty(request.SourceType, request.SourceTypeAlias).ToLowerInvariant();
        return sourceType is "food" or "dish";
    }

    private async Task<IActionResult> CreateDishOrderAsync(int userId, CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var items = await ResolveOrderItemsAsync(userId, request, cancellationToken);
        if (items.Count == 0)
        {
            return Ok(ApiResult.Fail("items cannot be empty", 400));
        }

        // 验证菜品存在并获取最新价格
        var dishIds = items.Select(x => x.GoodsId).Distinct().ToList();
        var dishes = await _dbContext.Dishes
            .Where(x => x.IsDelete == 0 && dishIds.Contains(x.DishId) && x.Status == 1)
            .ToDictionaryAsync(x => x.DishId, cancellationToken);

        if (dishes.Count != dishIds.Count || items.Any(x => !dishes.ContainsKey(x.GoodsId)))
        {
            return Ok(ApiResult.Fail("dish not found", 404));
        }

        // 用数据库价格覆盖请求中的价格
        foreach (var item in items)
        {
            if (dishes.TryGetValue(item.GoodsId, out var dish))
            {
                item.Price = dish.DishPrice;
                item.Name = dish.DishName;
            }
        }

        var totalAmount = items.Sum(x => x.Price * x.Quantity);
        if (totalAmount <= 0)
        {
            return Ok(ApiResult.Fail("totalPrice is invalid", 400));
        }

        var now = DateTime.Now;
        var dishOrder = new DishOrder
        {
            OrderNo = GenerateDishOrderNumber(),
            WxPayNo = null,
            TotalAmount = totalAmount,
            TotalQuantity = items.Sum(x => x.Quantity),
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now,
            DiningTableId = request.EffectiveTableId,
            Remark = request.Remark
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 原子扣减菜品库存
        var deductResult = await _inventoryService.DeductBatchAsync(
            ProductType.Dish,
            items.Select(x => (x.GoodsId, x.Quantity, x.Name)).ToList());
        if (!deductResult.Success)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Ok(ApiResult.Fail(deductResult.ErrorMessage!, 409));
        }

        _dbContext.DishOrders.Add(dishOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in items)
        {
            _dbContext.DishOrderDetails.Add(new DishOrderDetail
            {
                DishOrderId = dishOrder.OrderId,
                DishId = item.GoodsId,
                GoodsName = item.Name,
                UnitPrice = item.Price,
                Quantity = item.Quantity,
                SubtotalAmount = item.Price * item.Quantity,
                StatusId = 1
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await UpdateTableOccupiedAsync(dishOrder.DiningTableId, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            orderNo = dishOrder.OrderNo,
            status = "pending",
            orderStatus = "pending",
            totalPrice = totalAmount,
            amount = totalAmount,
            quantity = items.Sum(x => x.Quantity),
            createTime = dishOrder.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            remark = dishOrder.Remark
        }, "order created"));
    }

    private static IQueryable<CommodityOrder> ApplyOrderStatusFilter(IQueryable<CommodityOrder> query, string? status)
    {
        return (status ?? "all").Trim().ToLowerInvariant() switch
        {
            "pending" or "pending_payment" => query.Where(x => x.OrderStatusId == 1),
            "paid" => query.Where(x => x.OrderStatusId == 2),
            "shipping" or "shipped" => query.Where(x => x.OrderStatusId == 3),
            "completed" => query.Where(x => x.OrderStatusId == 4 || x.OrderStatusId == 9),
            "cancelled" => query.Where(x => x.OrderStatusId == 5),
            _ => query
        };
    }

    private async Task<Dictionary<long, List<object>>> LoadCommodityOrderItemsAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        var details = await _dbContext.CommodityOrderDetails
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId))
            .ToListAsync(cancellationToken);

        var commodityIds = details.Select(x => x.CommodityId).Distinct().ToList();
        var commodityMap = commodityIds.Count == 0
            ? new Dictionary<int, Commodity>()
            : await _dbContext.Commodities.AsNoTracking()
                .Where(x => x.IsDelete == 0 && commodityIds.Contains(x.CommodityId))
                .ToDictionaryAsync(x => x.CommodityId, cancellationToken);

        return details.GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => g.Select(x =>
        {
            commodityMap.TryGetValue(x.CommodityId, out var commodity);
            var name = !string.IsNullOrEmpty(x.GoodsName) ? x.GoodsName
                : commodity?.ProductName ?? $"Goods {x.CommodityId}";
            var image = !string.IsNullOrEmpty(x.ImageUrl) ? x.ImageUrl
                : NormalizeMediaUrl(commodity?.ImageUrl);
            return (object)new
            {
                id = x.CommodityId.ToString(),
                name,
                price = x.UnitPrice,
                quantity = x.Quantity,
                image,
                type = commodity?.CategoryId == 5 ? "acre" : "normal",
                statusId = x.StatusId ?? 1,
                status = MapDetailStatusValue(x.StatusId ?? 1)
            };
        }).ToList());
    }

    private static string MapDetailStatusValue(int statusId) => statusId switch
    {
        1 => "pending",
        2 => "paid",
        3 => "shipping",
        4 => "completed",
        5 => "cancelled",
        6 => "refunding",
        7 => "refunded",
        _ => "unknown"
    };

    private async Task SyncCommodityDetailStatusAsync(long orderId, int statusId, CancellationToken ct)
    {
        var details = await _dbContext.CommodityOrderDetails
            .Where(x => x.OrderId == orderId)
            .ToListAsync(ct);
        foreach (var d in details)
        {
            d.StatusId = statusId;
        }
    }

    private static object BuildCommodityOrderListItem(CommodityOrder order, IReadOnlyDictionary<long, List<object>> itemMap)
    {
        return new
        {
            id = order.OrderId.ToString(),
            orderId = order.OrderId.ToString(),
            orderNumber = order.OrderNo,
            orderNo = order.OrderNo,
            status = MapCommodityStatusValue(order.OrderStatusId),
            statusText = MapCommodityStatusText(order.OrderStatusId),
            orderStatusId = order.OrderStatusId,
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            totalQuantity = order.TotalQuantity,
            orderType = "goods",
            transactionId = order.WxPayNo,
            items = itemMap.TryGetValue(order.OrderId, out var items) ? items : []
        };
    }

    private object BuildDishOrderListItem(DishOrder order)
    {
        return new
        {
            id = order.OrderId.ToString(),
            orderId = order.OrderId.ToString(),
            orderNumber = order.OrderNo,
            status = MapDishStatusValue(order.OrderStatusId),
            statusText = GetDishStatusText(order.OrderStatusId),
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            totalQuantity = order.TotalQuantity,
            orderType = "food",
            transactionId = order.WxPayNo,
            diningTableId = order.DiningTableId,
            remark = order.Remark
        };
    }

    private static object BuildActivityOrderListItem(ActivityOrder order)
    {
        return new
        {
            id = order.OrderId.ToString(),
            orderId = order.OrderId.ToString(),
            orderNumber = order.OrderNo,
            status = MapActivityStatusValue(order.OrderStatusId),
            statusText = MapActivityStatusText(order.OrderStatusId),
            createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            totalPrice = order.TotalAmount,
            totalAmount = order.TotalAmount,
            totalQuantity = order.TotalQuantity,
            orderType = "activity",
            transactionId = order.WxPayNo
        };
    }

    private static string MapCommodityStatusValue(int statusId)
    {
        return statusId switch
        {
            1 => "pending",
            2 => "paid",
            3 => "shipping",
            4 => "completed",
            5 => "cancelled",
            8 => "verify_pending",
            9 => "completed",
            _ => "unknown"
        };
    }

    private static string MapCommodityStatusText(int statusId)
    {
        return statusId switch
        {
            1 => "Pending Payment",
            2 => "Paid",
            3 => "Shipping",
            4 => "Completed",
            5 => "Cancelled",
            8 => "Pending Verification",
            9 => "Completed",
            _ => "Unknown"
        };
    }

    private static Dictionary<int, string>? _dishStatusTextCache;
    private static readonly object _dishStatusCacheLock = new();

    private async Task EnsureDishStatusCacheAsync()
    {
        if (_dishStatusTextCache != null) return;
        var statuses = await _dbContext.DishOrderStatuses.AsNoTracking().ToListAsync();
        lock (_dishStatusCacheLock)
        {
            _dishStatusTextCache ??= statuses.ToDictionary(x => x.OrderStatusId, x => x.StatusName);
        }
    }

    private string GetDishStatusText(int statusId) =>
        _dishStatusTextCache?.TryGetValue(statusId, out var text) == true ? text : "未知";

    private static string MapDishStatusValue(int statusId)
    {
        return statusId switch
        {
            1 => "pending",
            2 => "paid",
            3 => "completed",
            4 => "cancelled",
            _ => "unknown"
        };
    }

    private static string MapActivityStatusValue(int statusId)
    {
        return statusId switch
        {
            1 => "pending",
            2 => "verify_pending",
            3 => "verified",
            4 => "cancelled",
            _ => "unknown"
        };
    }

    private static string MapActivityStatusText(int statusId)
    {
        return statusId switch
        {
            1 => "Pending Payment",
            2 => "Pending Verification",
            3 => "Verified",
            4 => "Cancelled",
            _ => "Unknown"
        };
    }

    public sealed class UpdateGoodsQuantityRequest
    {
        public Dictionary<int, int> Updates { get; set; } = [];
    }

    public sealed class CreateOrderRequest
    {
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Quantity { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal TotalPrice { get; set; }
        public string Remark { get; set; } = string.Empty;
        public ConfirmOrderAddress? Address { get; set; }
        public List<ConfirmOrderItemRequest> Items { get; set; } = [];

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int AddressId { get; set; }
        public List<int> CartIds { get; set; } = [];

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int GoodsId { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Count { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long TableId { get; set; }

        [JsonPropertyName("source_type")]
        public string? SourceTypeAlias { get; set; }

        [JsonPropertyName("source_name")]
        public string? SourceNameAlias { get; set; }

        [JsonPropertyName("address_info")]
        public ConfirmOrderAddress? AddressAlias { get; set; }

        [JsonPropertyName("item_list")]
        public List<ConfirmOrderItemRequest> ItemsAlias { get; set; } = [];

        [JsonPropertyName("address_id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int AddressIdAlias { get; set; }

        [JsonPropertyName("cart_ids")]
        public List<int> CartIdsAlias { get; set; } = [];

        [JsonPropertyName("goods_id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int GoodsIdAlias { get; set; }

        [JsonPropertyName("table_id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long TableIdAlias { get; set; }

        [JsonIgnore]
        public long EffectiveTableId => TableId > 0 ? TableId : TableIdAlias;

        [JsonIgnore]
        public List<int> MergedCartIds => CartIds.Count > 0 ? CartIds : CartIdsAlias;
    }

    public sealed class ConfirmOrderAddress
    {
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Id { get; set; } = string.Empty;

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int AddressId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("address_id")]
        public int AddressIdAlias
        {
            get => AddressId;
            set => AddressId = value;
        }
    }

    public sealed class ConfirmOrderItemRequest
    {
        public JsonElement? Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal Price { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("item_id")]
        public JsonElement? IdAlias { get; set; }

        [JsonPropertyName("item_name")]
        public string? NameAlias { get; set; }

        [JsonPropertyName("unit_price")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? UnitPriceAlias { get; set; }

        [JsonPropertyName("count")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? CountAlias { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageAlias { get; set; }
    }

    public sealed class CancelOrderRequest
    {
        public long OrderId { get; set; }
    }

    public sealed class PayOrderRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal PayAmount { get; set; }
    }

    private sealed class MenuCategoryItem
    {
        public int CategoryId { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NormalizedOrderItem
    {
        public int GoodsId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Image { get; set; } = string.Empty;
    }

    private static string FormatTableName(string tableNo)
    {
        var digits = string.Concat((tableNo ?? string.Empty).Where(char.IsDigit));
        return string.IsNullOrEmpty(digits) ? tableNo : $"{digits.TrimStart('0')}号桌";
    }

    private static string BuildTableQrUrl(long tableId)
    {
        var queryValue = $"tableId={tableId}&secret=^mFIT!xzJ@j55QN%R^4yZ0vx";
        var encodedQuery = Uri.EscapeDataString(queryValue);
        return $"weixin://dl/business/?appid=wx986e22f241e13ba2&path=subpkg/order/order&query={encodedQuery}";
    }

    private static string GenerateQrCodeBase64(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        var bytes = qrCode.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private static byte[] GenerateQrCodeBytes(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(20);
    }

    private async Task UpdateTableOccupiedAsync(long diningTableId, CancellationToken cancellationToken)
    {
        var table = await _dbContext.DiningTables.FindAsync(new object[] { diningTableId }, cancellationToken);
        if (table != null)
        {
            table.TableStatusId = 1;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task TryFreeTableAsync(long diningTableId, CancellationToken cancellationToken)
    {
        var hasActiveOrders = await _dbContext.DishOrders
            .AnyAsync(x => x.DiningTableId == diningTableId
                && (x.OrderStatusId == 1 || x.OrderStatusId == 2), cancellationToken);

        if (!hasActiveOrders)
        {
            var table = await _dbContext.DiningTables.FindAsync(new object[] { diningTableId }, cancellationToken);
            if (table != null)
            {
                table.TableStatusId = 1;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

}
