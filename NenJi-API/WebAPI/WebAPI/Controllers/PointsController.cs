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
    /// 可积分兑换的商品列表
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

            var query = _dbContext.Commodities
                .AsNoTracking()
                .Where(x => (x.ProductStatus ?? 0) == 1 && x.PointsPrice != null && x.PointsPrice > 0);

            var total = await query.CountAsync(cancellationToken);
            var list = await query
                .OrderByDescending(x => x.CommodityId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                list = list.Select(x => new
                {
                    id = x.CommodityId.ToString(),
                    name = x.ProductName,
                    image = MediaUrlHelper.Normalize(x.ImageUrl),
                    pointsPrice = x.PointsPrice,
                    stock = x.InStock ?? 0,
                    description = x.SpecDescription ?? string.Empty,
                    unit = x.UnitName ?? string.Empty
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
            var commodity = await _dbContext.Commodities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CommodityId == id && (x.ProductStatus ?? 0) == 1 && x.PointsPrice != null && x.PointsPrice > 0, cancellationToken);

            if (commodity is null)
                return Ok(ApiResult.Fail("商品不存在", 404));

            return Ok(ApiResult.Success(new
            {
                id = commodity.CommodityId.ToString(),
                name = commodity.ProductName,
                image = MediaUrlHelper.Normalize(commodity.ImageUrl),
                pointsPrice = commodity.PointsPrice,
                stock = commodity.InStock ?? 0,
                price = commodity.UnitPrice ?? 0m,
                description = commodity.SpecDescription ?? string.Empty,
                storageCondition = commodity.StorageCondition ?? string.Empty,
                weightText = commodity.WeightText ?? string.Empty,
                unit = commodity.UnitName ?? string.Empty
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
                pointsSpent = result.PointsSpent,
                pointsRemaining = result.PointsRemaining,
                status = result.Status
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
                return Ok(ApiResult.Success(new
                {
                    ruleName = "默认规则",
                    unitAmount = 0.01m,
                    unitPoints = 10,
                    unitAmountText = "0.01元",
                    description = "每消费0.01元获得10积分"
                }));
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
