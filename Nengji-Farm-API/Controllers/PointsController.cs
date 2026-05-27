using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/points")]
public class PointsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IPointsService _pointsService;

    public PointsController(AppDbContext dbContext, IPointsService pointsService)
    {
        _dbContext = dbContext;
        _pointsService = pointsService;
    }

    /// <summary>
    /// 获取积分总览
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var summary = await _pointsService.GetSummaryAsync(userId, cancellationToken);
            return Ok(ApiResult.Success(summary));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取积分失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 可积分兑换的商品列表（从 points_commodity 表查询）
    /// </summary>
    [HttpGet("goods")]
    public async Task<IActionResult> GetGoods(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // 从 points_commodity_status 表获取"上架"状态的 ID
            var activeStatusIds = await GetActiveStatusIdsAsync(cancellationToken);

            var query = _dbContext.PointsCommodities
                .AsNoTracking()
                .Where(x => x.IsDelete == 0 && x.StatusId != null && activeStatusIds.Contains(x.StatusId!.Value));

            var total = await query.CountAsync(cancellationToken);
            var list = await query
                .OrderByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                list = list.Select(x => new
                {
                    id = x.Id.ToString(),
                    name = x.Name,
                    image = MediaUrlHelper.Normalize(x.ImageUrl),
                    pointsPrice = x.PointsPrice,
                    stock = x.Stock,
                    description = x.Description ?? string.Empty
                }).ToList(),
                total,
                page,
                pageSize
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取积分商品失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 积分商品详情
    /// </summary>
    [HttpGet("goods/{id}")]
    public async Task<IActionResult> GetGoodsDetail(int id, CancellationToken cancellationToken)
    {
        try
        {
            var activeStatusIds = await GetActiveStatusIdsAsync(cancellationToken);

            var commodity = await _dbContext.PointsCommodities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsDelete == 0
                    && x.StatusId != null && activeStatusIds.Contains(x.StatusId!.Value), cancellationToken);

            if (commodity is null)
                return Ok(ApiResult.Fail("商品不存在", 404));

            // 查询详情图（material_type=1）
            var images = await _dbContext.PointsCommodityImages
                .AsNoTracking()
                .Where(x => x.PointsCommodityId == id && x.MaterialType == 1)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.ImageUrl)
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                id = commodity.Id.ToString(),
                name = commodity.Name,
                image = MediaUrlHelper.Normalize(commodity.ImageUrl),
                images = images.Select(MediaUrlHelper.Normalize).ToList(),
                pointsPrice = commodity.PointsPrice,
                stock = commodity.Stock,
                description = commodity.Description ?? string.Empty
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取商品详情失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 积分兑换商品
    /// </summary>
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] PointsExchangeRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.CommodityId <= 0 || request.Quantity <= 0)
                return Ok(ApiResult.Fail("请求参数不正确", 400));

            var userId = GetUserId();
            var result = await _pointsService.ExchangeAsync(userId, request.CommodityId, request.Quantity, cancellationToken);

            return Ok(ApiResult.Success(new
            {
                exchangeId = result.ExchangeId,
                orderNo = result.OrderNo,
                name = result.Name,
                image = result.Image,
                pointsSpent = result.PointsSpent,
                pointsRemaining = result.PointsRemaining,
                qrcodeUrl = result.QrcodeUrl,
                status = result.Status,
                statusText = result.StatusText,
                time = result.Time
            }, "兑换成功"));
        }
        catch (BusinessException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, ex.Code));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"兑换失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 兑换详情（含二维码）
    /// </summary>
    [HttpGet("exchange-detail/{orderNo}")]
    public async Task<IActionResult> GetExchangeDetail(string orderNo, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return Ok(ApiResult.Fail("订单号不能为空", 400));

            var userId = GetUserId();
            var result = await _pointsService.GetExchangeDetailAsync(orderNo.Trim(), userId, cancellationToken);

            if (result is null)
                return Ok(ApiResult.Fail("兑换记录不存在", 404));

            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 积分流水
    /// </summary>
    [HttpGet("records")]
    public async Task<IActionResult> GetRecords(
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserId();
            var result = await _pointsService.GetRecordsAsync(userId, type, page, pageSize, cancellationToken);
            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取积分流水失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 兑换记录
    /// </summary>
    [HttpGet("exchange-records")]
    public async Task<IActionResult> GetExchangeRecords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserId();
            var result = await _pointsService.GetExchangeRecordsAsync(userId, page, pageSize, cancellationToken);
            return Ok(ApiResult.Success(result));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取兑换记录失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 手动积分入账（管理员用）
    /// </summary>
    [HttpPost("earn")]
    public async Task<IActionResult> Earn([FromBody] PointsEarnRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.Amount <= 0)
                return Ok(ApiResult.Fail("请求参数不正确", 400));

            var userId = GetUserId();
            var orderNo = request.OrderNo;
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                orderNo = $"MANUAL{DateTime.Now:yyyyMMddHHmmssfff}";
            }

            await _pointsService.EarnPointsAsync(userId, orderNo, request.Amount, cancellationToken);
            return Ok(ApiResult.Success(null, "积分入账成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"积分入账失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取当前积分规则
    /// </summary>
    [HttpGet("rule")]
    public async Task<IActionResult> GetRule(CancellationToken cancellationToken)
    {
        try
        {
            var rule = await _dbContext.PointsRules
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (rule is null)
            {
                return Ok(ApiResult.Fail("暂未配置积分规则", 404));
            }

            return Ok(ApiResult.Success(new
            {
                id = rule.Id,
                ruleName = rule.RuleName,
                unitAmount = rule.UnitAmount,
                unitPoints = rule.UnitPoints,
                unitAmountText = $"{rule.UnitAmount}元",
                description = rule.Description ?? $"每消费{rule.UnitAmount}元获得{rule.UnitPoints}积分"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取积分规则失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 从 points_commodity_status 表获取上架状态 ID 列表（数据库驱动）
    /// </summary>
    private async Task<HashSet<int>> GetActiveStatusIdsAsync(CancellationToken ct = default)
    {
        try
        {
            var active = await _dbContext.PointsCommodityStatuses
                .AsNoTracking()
                .Where(s => s.StatusName == "上架")
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (active.Count > 0)
                return active.ToHashSet();
        }
        catch { }

        return [1];
    }

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
    }
}

public sealed class PointsExchangeRequest
{
    public int CommodityId { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class PointsEarnRequest
{
    public decimal Amount { get; set; }
    public string? OrderNo { get; set; }
}
