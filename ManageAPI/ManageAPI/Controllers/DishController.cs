using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ManageAPI.Dtos;
using ManageAPI.Services;

namespace ManageAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/dish")]
public class DishController : ControllerBase
{
    private readonly IDishService _dishService;
    private readonly ILogger<DishController> _logger;

    public DishController(IDishService dishService, ILogger<DishController> logger)
    {
        _dishService = dishService;
        _logger = logger;
    }

    /// <summary>获取菜品列表（支持分页、搜索、状态筛选）</summary>
    [HttpGet("list")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponses<object>>> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            pageNum = Math.Max(1, pageNum);
            pageSize = Math.Max(1, Math.Min(100, pageSize));

            var (records, total) = await _dishService.GetDishListAsync(
                pageNum, pageSize, keyword, status, cancellationToken);

            return Ok(ApiResponses<object>.Success(new
            {
                records,
                total,
                pages = (int)Math.Ceiling((double)total / pageSize),
                pageNum
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取菜品列表失败");
            return Ok(ApiResponses<object>.Error(500, "获取失败"));
        }
    }

    /// <summary>获取菜品详情</summary>
    [HttpGet("detail/{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponses<DishDetailDto>>> GetDetail(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!int.TryParse(id, out var dishId) || dishId <= 0)
                return Ok(ApiResponses<object>.Error(400, "菜品ID不正确"));

            var detail = await _dishService.GetDishDetailAsync(dishId, cancellationToken);
            if (detail is null)
                return Ok(ApiResponses<object>.Error(404, "菜品不存在"));

            return Ok(ApiResponses<DishDetailDto>.Success(detail));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取菜品详情失败");
            return Ok(ApiResponses<object>.Error(500, "获取失败"));
        }
    }

    /// <summary>新增菜品</summary>
    [HttpPost("add")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponses<object>>> Add(
        [FromBody] CreateDishDto? dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                return Ok(ApiResponses<object>.Error(400, "参数错误"));

            var id = await _dishService.CreateDishAsync(dto, cancellationToken);

            return Ok(ApiResponses<object>.Success(new
            {
                id = id.ToString(),
                name = dto.Name,
                price = dto.Price,
                stock = dto.Stock,
                status = dto.Status
            }, "新增成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增菜品失败");
            return Ok(ApiResponses<object>.Error(500, "新增失败"));
        }
    }

    /// <summary>更新菜品</summary>
    [HttpPost("edit")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponses<object>>> Edit(
        [FromBody] UpdateDishDto? dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(ApiResponses<object>.Error(400, "参数错误"));

            var success = await _dishService.UpdateDishAsync(dto, cancellationToken);
            if (!success)
                return Ok(ApiResponses<object>.Error(404, "菜品不存在"));

            return Ok(ApiResponses<object>.Success(null, "修改成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新菜品失败");
            return Ok(ApiResponses<object>.Error(500, "修改失败"));
        }
    }

    /// <summary>删除菜品（支持单个或批量）</summary>
    [HttpPost("delete")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponses<object>>> Delete(
        [FromBody] DeleteDishRequest? request,
        [FromQuery] string? id = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetId = request?.Id ?? id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetId) || !int.TryParse(targetId, out var dishId) || dishId <= 0)
                return Ok(ApiResponses<object>.Error(400, "菜品ID不正确"));

            var success = await _dishService.DeleteDishAsync(dishId, cancellationToken);
            if (!success)
                return Ok(ApiResponses<object>.Error(404, "菜品不存在"));

            return Ok(ApiResponses<object>.Success(null, "删除成功"));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "删除菜品失败（外键约束）");
            return Ok(ApiResponses<object>.Error(500, "该菜品存在关联订单，无法删除"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除菜品失败");
            return Ok(ApiResponses<object>.Error(500, "删除失败"));
        }
    }
}
