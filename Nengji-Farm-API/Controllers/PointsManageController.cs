using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities.Manage;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/back-points")]
public class PointsManageController : ControllerBase
{
    private readonly ManageAppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PointsManageController> _logger;

    public PointsManageController(ManageAppDbContext dbContext, IWebHostEnvironment env, ILogger<PointsManageController> logger)
    {
        _dbContext = dbContext;
        _env = env;
        _logger = logger;
    }

    // ==================== 积分商品管理 ====================

    /// <summary>
    /// 积分商品列表（管理端，包含所有状态）
    /// </summary>
    [HttpGet("goods/list")]
    public async Task<IActionResult> GetGoodsList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.PointsCommodities.AsNoTracking()
                .Where(c => c.IsDelete == 0);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(c => c.Name.Contains(kw));
            }

            var total = await query.CountAsync(cancellationToken);

            var list = await query
                .OrderByDescending(c => c.Id)
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    pointsPrice = c.PointsPrice,
                    stock = c.Stock,
                    statusId = c.StatusId,
                    image = MediaHelper.NormalizeImageUrl(c.ImageUrl),
                    description = c.Description ?? string.Empty,
                    createTime = c.CreateTime.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                list,
                total,
                pageNum,
                pageSize,
                pages = (total + pageSize - 1) / pageSize
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取积分商品列表失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取积分商品列表失败", 500));
        }
    }

    /// <summary>
    /// 积分商品详情
    /// </summary>
    [HttpGet("goods/detail")]
    public async Task<IActionResult> GetGoodsDetail(
        [FromQuery] int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                return Ok(ApiResult.Fail("参数不正确", 400));

            var goods = await _dbContext.PointsCommodities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.IsDelete == 0, cancellationToken);

            if (goods is null)
                return Ok(ApiResult.Fail("商品不存在", 404));

            var images = await _dbContext.PointsCommodityImages
                .AsNoTracking()
                .Where(i => i.PointsCommodityId == id && i.MaterialType == 1)
                .OrderBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                id = goods.Id,
                name = goods.Name,
                pointsPrice = goods.PointsPrice,
                stock = goods.Stock,
                statusId = goods.StatusId,
                image = MediaHelper.NormalizeImageUrl(goods.ImageUrl),
                images = images.Select(MediaHelper.NormalizeImageUrl).ToList(),
                description = goods.Description ?? string.Empty
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取积分商品详情失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取积分商品详情失败", 500));
        }
    }

    /// <summary>
    /// 新增积分商品
    /// </summary>
    [HttpPost("goods/add")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateGoods(CancellationToken cancellationToken = default)
    {
        try
        {
            string name, description, imageUrl;
            int pointsPrice, stock, statusId;
            List<string>? imagesList = null;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                name = form["name"].FirstOrDefault() ?? string.Empty;
                description = form["description"].FirstOrDefault() ?? string.Empty;
                pointsPrice = int.TryParse(form["pointsPrice"].FirstOrDefault(), out var pp) ? pp : 0;
                stock = int.TryParse(form["stock"].FirstOrDefault(), out var s) ? s : 0;
                statusId = int.TryParse(form["statusId"].FirstOrDefault(), out var sid) ? sid : 1;
                var imageFile = form.Files.GetFile("image");
                imageUrl = await MediaHelper.SaveFileAsync(imageFile, _env.WebRootPath);
            }
            else
            {
                var dto = await Request.ReadFromJsonAsync<CreatePointsGoodsDto>(cancellationToken: cancellationToken);
                if (dto is null)
                    return Ok(ApiResult.Fail("请求参数不能为空", 400));
                name = dto.Name;
                description = dto.Description ?? string.Empty;
                pointsPrice = dto.PointsPrice;
                stock = dto.Stock;
                statusId = dto.StatusId;
                imageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath);
                imagesList = dto.Images;
            }

            if (string.IsNullOrWhiteSpace(name))
                return Ok(ApiResult.Fail("商品名称不能为空", 400));

            var goods = new PointsCommodity
            {
                Name = name,
                Description = description,
                PointsPrice = pointsPrice,
                Stock = stock,
                StatusId = statusId,
                ImageUrl = imageUrl,
                IsDelete = 0,
                CreateTime = DateTime.Now
            };

            _dbContext.PointsCommodities.Add(goods);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 保存轮播图（仅 JSON 路径）
            if (!Request.HasFormContentType && imagesList?.Count > 0)
            {
                var images = imagesList
                    .Select((url, idx) => new PointsCommodityImage
                        {
                            PointsCommodityId = goods.Id,
                            ImageUrl = url,
                            SortOrder = idx,
                            MaterialType = 1,
                            CreateTime = DateTime.Now,
                            Type = "image"
                        })
                    .ToList();

                _dbContext.PointsCommodityImages.AddRange(images);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(ApiResult.Success(new { id = goods.Id }, "创建成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("创建积分商品失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail($"创建失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 编辑积分商品
    /// </summary>
    [HttpPut("goods/edit")]
    [HttpPost("goods/edit")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UpdateGoods(CancellationToken cancellationToken = default)
    {
        try
        {
            int id; string name, description, imageUrl;
            int pointsPrice, stock, statusId;
            List<string>? imagesList = null;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                id = int.TryParse(form["id"].FirstOrDefault(), out var i) ? i : 0;
                name = form["name"].FirstOrDefault() ?? string.Empty;
                description = form["description"].FirstOrDefault() ?? string.Empty;
                pointsPrice = int.TryParse(form["pointsPrice"].FirstOrDefault(), out var pp) ? pp : 0;
                stock = int.TryParse(form["stock"].FirstOrDefault(), out var s) ? s : 0;
                statusId = int.TryParse(form["statusId"].FirstOrDefault(), out var sid) ? sid : 1;
                var imageFile = form.Files.GetFile("image");
                imageUrl = imageFile is not null ? await MediaHelper.SaveFileAsync(imageFile, _env.WebRootPath) : string.Empty;
            }
            else
            {
                var dto = await Request.ReadFromJsonAsync<UpdatePointsGoodsDto>(cancellationToken: cancellationToken);
                if (dto is null)
                    return Ok(ApiResult.Fail("请求参数不能为空", 400));
                id = dto.Id;
                name = dto.Name;
                description = dto.Description ?? string.Empty;
                pointsPrice = dto.PointsPrice;
                stock = dto.Stock;
                statusId = dto.StatusId;
                imageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath);
                imagesList = dto.Images;
            }

            if (id <= 0 || string.IsNullOrWhiteSpace(name))
                return Ok(ApiResult.Fail("参数不正确", 400));

            var goods = await _dbContext.PointsCommodities
                .FirstOrDefaultAsync(c => c.Id == id && c.IsDelete == 0, cancellationToken);

            if (goods is null)
                return Ok(ApiResult.Fail("商品不存在", 404));

            goods.Name = name;
            goods.Description = description;
            goods.PointsPrice = pointsPrice;
            goods.Stock = stock;
            goods.StatusId = statusId;
            if (!string.IsNullOrEmpty(imageUrl))
                goods.ImageUrl = imageUrl;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // 替换轮播图（仅 JSON 路径）
            if (!Request.HasFormContentType && imagesList is not null)
            {
                var oldImages = await _dbContext.PointsCommodityImages
                    .Where(i => i.PointsCommodityId == id)
                    .ToListAsync(cancellationToken);
                _dbContext.PointsCommodityImages.RemoveRange(oldImages);

                var newImages = imagesList
                    .Select((url, idx) => new PointsCommodityImage
                    {
                        PointsCommodityId = goods.Id,
                        ImageUrl = url,
                        SortOrder = idx,
                        MaterialType = 1,
                        CreateTime = DateTime.Now,
                        Type = "image"
                    })
                    .ToList();

                _dbContext.PointsCommodityImages.AddRange(newImages);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(ApiResult.Success("编辑成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("编辑积分商品失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail($"编辑失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 删除积分商品（软删除）
    /// </summary>
    [HttpPost("goods/delete")]
    public async Task<IActionResult> DeleteGoods(
        [FromBody] DeletePointsGoodsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Id <= 0)
                return Ok(ApiResult.Fail("参数不正确", 400));

            var goods = await _dbContext.PointsCommodities
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.IsDelete == 0, cancellationToken);

            if (goods is null)
                return Ok(ApiResult.Fail("商品不存在", 404));

            goods.IsDelete = 1;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("删除积分商品失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    // ==================== 兑换订单管理 ====================

    /// <summary>
    /// 积分兑换订单列表
    /// </summary>
    [HttpGet("order/list")]
    public async Task<IActionResult> GetOrderList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        [FromQuery] int? statusId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.PointsExchanges.AsNoTracking();

            if (statusId.HasValue)
                query = query.Where(o => o.StatusId == statusId.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(o => o.OrderNo.Contains(kw));
            }

            var total = await query.CountAsync(cancellationToken);

            // 先查订单，再查商品名
            var orders = await query
                .OrderByDescending(o => o.CreateTime)
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var commodityIds = orders.Select(o => o.CommodityId).Distinct().ToList();
            var commodities = await _dbContext.PointsCommodities.AsNoTracking()
                .Where(c => commodityIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.ImageUrl })
                .ToListAsync(cancellationToken);
            var commodityMap = commodities.ToDictionary(c => c.Id);

            var list = orders.Select(o =>
            {
                var goods = commodityMap.GetValueOrDefault(o.CommodityId);
                return new
                {
                    id = o.Id,
                    orderNo = o.OrderNo,
                    commodityId = o.CommodityId,
                    commodityName = goods?.Name ?? string.Empty,
                    commodityImage = MediaHelper.NormalizeImageUrl(goods?.ImageUrl),
                    quantity = o.Quantity,
                    pointsSpent = o.PointsSpent,
                    statusId = o.StatusId,
                    userId = o.UserId,
                    createTime = o.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                    verifyTime = o.VerifyTime?.ToString("yyyy-MM-dd HH:mm")
                };
            }).ToList();

            return Ok(ApiResult.Success(new
            {
                list,
                total,
                pageNum,
                pageSize,
                pages = (total + pageSize - 1) / pageSize
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取兑换订单列表失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取兑换订单列表失败", 500));
        }
    }

    /// <summary>
    /// 积分兑换订单详情
    /// </summary>
    [HttpGet("order/detail")]
    public async Task<IActionResult> GetOrderDetail(
        [FromQuery] long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
                return Ok(ApiResult.Fail("参数不正确", 400));

            var order = await _dbContext.PointsExchanges
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

            if (order is null)
                return Ok(ApiResult.Fail("订单不存在", 404));

            var goods = await _dbContext.PointsCommodities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == order.CommodityId, cancellationToken);

            return Ok(ApiResult.Success(new
            {
                id = order.Id,
                orderNo = order.OrderNo,
                commodityId = order.CommodityId,
                commodityName = goods?.Name ?? string.Empty,
                commodityImage = MediaHelper.NormalizeImageUrl(goods?.ImageUrl),
                quantity = order.Quantity,
                pointsSpent = order.PointsSpent,
                statusId = order.StatusId,
                userId = order.UserId,
                verifyCode = order.VerifyCode,
                createTime = order.CreateTime.ToString("yyyy-MM-dd HH:mm"),
                verifyTime = order.VerifyTime?.ToString("yyyy-MM-dd HH:mm")
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取兑换订单详情失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取兑换订单详情失败", 500));
        }
    }

    /// <summary>
    /// 核销积分兑换订单
    /// </summary>
    [HttpPost("order/verify")]
    public async Task<IActionResult> VerifyOrder(
        [FromBody] VerifyPointsOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null || request.Id <= 0)
                return Ok(ApiResult.Fail("参数不正确", 400));

            var order = await _dbContext.PointsExchanges
                .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

            if (order is null)
                return Ok(ApiResult.Fail("订单不存在", 404));

            if (order.StatusId != 1)
                return Ok(ApiResult.Fail("仅待核销订单可核销", 400));

            order.StatusId = 2;
            order.VerifyTime = DateTime.Now;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success("核销成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("核销兑换订单失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail($"核销失败：{ex.Message}", 500));
        }
    }

    // ==================== 积分规则管理 ====================

    /// <summary>
    /// 取消兑换订单（仅待核销可取消，恢复库存）
    /// </summary>
    [HttpPost("order/cancel")]
    public async Task<IActionResult> CancelOrder(
        [FromBody] CancelPointsOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null || request.Id <= 0)
                return Ok(ApiResult.Fail("参数不正确", 400));

            var order = await _dbContext.PointsExchanges
                .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

            if (order is null)
                return Ok(ApiResult.Fail("订单不存在", 404));

            if (order.StatusId != 1)
                return Ok(ApiResult.Fail("仅待核销订单可取消", 400));

            // 恢复库存
            var commodity = await _dbContext.PointsCommodities
                .FirstOrDefaultAsync(c => c.Id == order.CommodityId, cancellationToken);
            if (commodity is not null)
                commodity.Stock += order.Quantity;

            // 退回积分给用户
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.UserId == order.UserId, cancellationToken);
            if (user is not null)
            {
                user.Points += order.PointsSpent;

                // 记录积分流水
                _dbContext.Database.ExecuteSqlRaw(
                    "INSERT INTO points_record (user_id, type, points, description, order_no, create_time) VALUES ({0}, {1}, {2}, {3}, {4}, NOW())",
                    order.UserId, "earn", order.PointsSpent, $"取消兑换", order.OrderNo);
            }

            // 状态改为已取消
            order.StatusId = 3;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success("取消成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("取消兑换订单失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail($"取消失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取积分规则
    /// </summary>
    [HttpGet("rule")]
    public async Task<IActionResult> GetRule(CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await _dbContext.PointsRules
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (rule is null)
                return Ok(ApiResult.Fail("暂未配置积分规则", 404));

            return Ok(ApiResult.Success(new
            {
                id = rule.Id,
                ruleName = rule.RuleName,
                unitAmount = rule.UnitAmount,
                unitPoints = rule.UnitPoints,
                description = rule.Description ?? $"每消费{rule.UnitAmount}元获得{rule.UnitPoints}积分"
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取积分规则失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取积分规则失败", 500));
        }
    }

    /// <summary>
    /// 编辑积分规则
    /// </summary>
    [HttpPut("rule/edit")]
    [HttpPost("rule/edit")]
    public async Task<IActionResult> UpdateRule(
        [FromBody] UpdatePointsRuleDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dto is null || dto.UnitAmount < 0 || dto.UnitPoints <= 0)
                return Ok(ApiResult.Fail("参数不正确", 400));

            var rule = await _dbContext.PointsRules
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (rule is null)
            {
                rule = new PointsRule
                {
                    RuleName = dto.RuleName ?? "默认规则",
                    UnitAmount = dto.UnitAmount,
                    UnitPoints = dto.UnitPoints,
                    Description = dto.Description,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _dbContext.PointsRules.Add(rule);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dto.RuleName))
                    rule.RuleName = dto.RuleName;
                rule.UnitAmount = dto.UnitAmount;
                rule.UnitPoints = dto.UnitPoints;
                if (dto.Description is not null)
                    rule.Description = dto.Description;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success("保存成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError("编辑积分规则失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail($"保存失败：{ex.Message}", 500));
        }
    }

    // ==================== 状态列表 ====================

    /// <summary>
    /// 积分商品状态列表
    /// </summary>
    [HttpGet("goods/statuses")]
    public async Task<IActionResult> GetGoodsStatuses(CancellationToken cancellationToken = default)
    {
        try
        {
            var list = await _dbContext.PointsCommodityStatuses
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => new { statusId = s.Id, statusName = s.StatusName })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(list));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取积分商品状态列表失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取积分商品状态列表失败", 500));
        }
    }

    /// <summary>
    /// 积分订单状态列表
    /// </summary>
    [HttpGet("order/statuses")]
    public async Task<IActionResult> GetOrderStatuses(CancellationToken cancellationToken = default)
    {
        try
        {
            var list = await _dbContext.PointsCommodityOrderStatuses
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => new { statusId = s.Id, statusName = s.StatusName })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(list));
        }
        catch (Exception ex)
        {
            _logger.LogError("获取积分订单状态列表失败: {Message}", ex.Message);
            return Ok(ApiResult.Fail("获取积分订单状态列表失败", 500));
        }
    }
}

// ==================== DTO ====================

public class CreatePointsGoodsDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PointsPrice { get; set; }
    public int Stock { get; set; }
    public int StatusId { get; set; } = 1;
    public string? Image { get; set; }
    public List<string>? Images { get; set; }
}

public class UpdatePointsGoodsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PointsPrice { get; set; }
    public int Stock { get; set; }
    public int StatusId { get; set; } = 1;
    public string? Image { get; set; }
    public List<string>? Images { get; set; }
}

public class DeletePointsGoodsRequest
{
    public int Id { get; set; }
}

public class VerifyPointsOrderRequest
{
    public long Id { get; set; }
}

public class CancelPointsOrderRequest
{
    public long Id { get; set; }
}

public class UpdatePointsRuleDto
{
    public string? RuleName { get; set; }
    public decimal UnitAmount { get; set; }
    public int UnitPoints { get; set; }
    public string? Description { get; set; }
}
