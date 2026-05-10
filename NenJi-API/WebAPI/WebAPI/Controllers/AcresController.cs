using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[Route("api/acres")]
public class AcresController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInventoryStatsService _inventoryStatsService;

    public AcresController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
    }

    [HttpGet("index")]
    public async Task<ActionResult<ApiResult>> GetPageData(CancellationToken cancellationToken)
    {
        var projects = await LoadProjectsSafeAsync(cancellationToken);
        var swiperList = await LoadSwiperListSafeAsync(cancellationToken);
        var items = projects.Select(MapListItem).ToList();

        return Ok(ApiResult.Success(new
        {
            swiperList,
            list = items,
            items
        }));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult>> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadProjectsSafeAsync(cancellationToken);
        var swiperList = await LoadSwiperListSafeAsync(cancellationToken);
        var filteredItems = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            filteredItems = filteredItems.Where(x => x.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var resultItems = filteredItems.ToList();
        var result = new AcreListResponseDto
        {
            PageIndex = pageIndex <= 0 ? 1 : pageIndex,
            PageSize = pageSize <= 0 ? 10 : pageSize,
            Total = resultItems.Count,
            Items = resultItems
        };

        return Ok(ApiResult.Success(new
        {
            pageIndex = result.PageIndex,
            pageSize = result.PageSize,
            total = result.Total,
            swiperList,
            list = result.Items,
            items = result.Items
        }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult>> GetDetail(string id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var goodsId))
        {
            return Ok(ApiResult.Fail("id 参数不正确", 400));
        }

        var commodity = await _dbContext.Commodities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CommodityId == goodsId && (x.ProductStatus ?? 0) == 1, cancellationToken);
        if (commodity is null)
        {
            return Ok(ApiResult.Fail("认养项目不存在", 404));
        }

        var detailRows = await _dbContext.CommodityImages
            .AsNoTracking()
            .Where(x => x.CommodityId == goodsId)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var images = detailRows
            .Select(x => NormalizeImageUrl(x.Url))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
        var mainImage = NormalizeImageUrl(commodity.ImageUrl) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(mainImage) && images.All(x => !x.Equals(mainImage, StringComparison.OrdinalIgnoreCase)))
        {
            images.Insert(0, mainImage);
        }

        var tags = await (
            from relation in _dbContext.CommodityTagRelations.AsNoTracking()
            join tag in _dbContext.Tags.AsNoTracking() on relation.TagId equals tag.TagId
            where relation.CommodityId == goodsId
            select tag.TagName
        ).Distinct().ToListAsync(cancellationToken);
        var stats = (await _inventoryStatsService.GetCommodityStatsAsync([goodsId], cancellationToken)).GetValueOrDefault(goodsId);
        var price = commodity.UnitPrice ?? 0m;
        var stock = stats?.Stock ?? (commodity.InStock ?? 0);
        var sold = stats?.Sold ?? Math.Max(0, commodity.Quantity ?? 0);
        var detailImage = images.FirstOrDefault() ?? mainImage;

        return Ok(ApiResult.Success(new
        {
            id = commodity.CommodityId.ToString(),
            name = commodity.ProductName,
            price,
            originalPrice = commodity.OriginalPrice ?? price,
            image = mainImage,
            mainImage,
            main_image = mainImage,
            detailImage,
            detail_image = detailImage,
            detailImages = images,
            detail_images = images,
            description = commodity.SpecDescription ?? string.Empty,
            desc = commodity.SpecDescription ?? string.Empty,
            weight = commodity.WeightText ?? string.Empty,
            storage = commodity.StorageCondition ?? string.Empty,
            videoUrl = string.Empty,
            sold,
            stock,
            tags,
            swiperList = images.Select((image, index) => new { id = index + 1, image }).ToList()
        }));
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost("{id}/adopt")]
    public async Task<ActionResult<ApiResult>> Adopt(string id, [FromBody] object? body, CancellationToken cancellationToken)
    {
        if (!long.TryParse(id, out var acreId))
        {
            return Ok(ApiResult.Fail("id 参数不正确", 400));
        }

        var userId = ResolveCurrentUserId();

        // 查找认购项目
        var project = await _dbContext.AcreProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AcreProjectId == acreId && x.Status == 1, cancellationToken);

        if (project is null)
        {
            return Ok(ApiResult.Fail("认养项目不存在", 404));
        }

        // 获取用户默认地址
        var address = await _dbContext.ShippingAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.AddressId)
            .FirstOrDefaultAsync(cancellationToken);

        if (address is null)
        {
            return Ok(ApiResult.Fail("请先添加收货地址", 400));
        }

        var now = DateTime.Now;
        var quantity = 1;
        var totalAmount = project.Price * quantity;

        var order = new CommodityOrder
        {
            OrderNo = GenerateAcreOrderNo(),
            WxPayNo = null,
            TotalAmount = totalAmount,
            TotalQuantity = quantity,
            OrderStatusId = 1,
            UserId = userId,
            CreateTime = now,
            AddressId = address.AddressId,
            TrackingNumber = null,
            TrackingTypeId = null
        };

        _dbContext.CommodityOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.CommodityOrderDetails.Add(new CommodityOrderDetail
        {
            OrderId = order.OrderId,
            CommodityId = (int)acreId,
            UnitPrice = project.Price,
            Quantity = quantity,
            SubtotalAmount = totalAmount,
            StatusId = 1
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            acreId,
            adopted = true,
            id = order.OrderNo,
            orderId = order.OrderNo,
            orderNumber = order.OrderNo,
            totalPrice = totalAmount,
            message = "success"
        }));
    }

    [HttpGet("{id}/logs")]
    public async Task<ActionResult<ApiResult>> Logs(string id, CancellationToken cancellationToken)
    {
        try
        {
            // acre_project_logs 表尚未创建，返回空数组
            var result = new AcreLogsResponseDto
            {
                Logs = []
            };

            return Ok(ApiResult.Success(result));
        }
        catch
        {
            return Ok(ApiResult.Success(new AcreLogsResponseDto { Logs = [] }));
        }
    }

    private async Task<List<AcreDto>> LoadProjectsAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.AcreProjects
            .AsNoTracking()
            .Where(x => x.Status == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AcreProjectId)
            .Select(x => new
            {
                x.AcreProjectId,
                x.Name,
                x.Price,
                x.ImageUrl,
                x.Description
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new AcreDto
        {
            Id = x.AcreProjectId.ToString(),
            Name = x.Name,
            Status = "available",
            Price = FormatPrice(x.Price),
            Image = NormalizeImageUrl(x.ImageUrl) ?? string.Empty,
            Description = x.Description
        }).ToList();
    }

    private async Task<List<AcreDto>> LoadProjectsSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await LoadProjectsAsync(cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<object>> LoadSwiperListAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Carousels
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.Position == "acres")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => new
            {
                id = x.CarouselId,
                image = x.ImageUrl,
                title = x.Title,
                linkUrl = x.LinkUrl ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => (object)new
        {
            id = x.id,
            image = NormalizeImageUrl(x.image) ?? string.Empty,
            title = x.title,
            linkUrl = x.linkUrl
        }).ToList();
    }

    private async Task<List<object>> LoadSwiperListSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await LoadSwiperListAsync(cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    private static object MapListItem(AcreDto acre)
    {
        return new
        {
            id = acre.Id,
            name = acre.Name,
            description = acre.Description,
            price = acre.Price,
            image = acre.Image,
            status = acre.Status
        };
    }

    private int ResolveCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }

    private static string GenerateAcreOrderNo()
    {
        return $"ACRE{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static string FormatPrice(decimal price)
    {
        return $"¥{price:0.##}/亩";
    }

    private string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var trimmed = imageUrl.Trim();

        // 如果已经是完整的 URL，直接返回
        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // 处理本地文件名，拼接完整的 API 访问路径
        trimmed = trimmed.TrimStart('/', '\\');
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var ext = Path.GetExtension(trimmed).ToLowerInvariant();

        // 视频文件
        if (ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".mkv" || ext == ".wmv")
        {
            return $"{baseUrl}/api/file/video/{trimmed}";
        }

        // 默认作为图片处理
        return $"{baseUrl}/api/file/image/{trimmed}";
    }
}
