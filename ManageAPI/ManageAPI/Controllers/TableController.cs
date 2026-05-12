using Microsoft.AspNetCore.Mvc;

using ManageAPI.Common;
using ManageAPI.Dtos;
using ManageAPI.Services;

namespace ManageAPI.Controllers;

[ApiController]
[Route("api/table")]
public class TableController : ControllerBase
{
    private readonly IDiningTableService _tableService;
    private readonly ILogger<TableController> _logger;

    public TableController(IDiningTableService tableService, ILogger<TableController> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    /// <summary>
    /// 获取餐桌列表（支持分页、搜索、状态筛选）
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageNum < 1) pageNum = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (records, total) = await _tableService.GetTableListAsync(
                pageNum, pageSize, keyword, status, cancellationToken);

            var pages = (total + pageSize - 1) / pageSize;

            return Ok(new
            {
                code = 200,
                message = "success",
                data = new
                {
                    records,
                    total,
                    pages,
                    pageNum
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取餐桌列表失败");
            return StatusCode(500, new { code = 500, message = "服务器内部错误", data = (object?)null });
        }
    }

    /// <summary>
    /// 获取单个餐桌详情
    /// </summary>
    [HttpGet("detail/{id}")]
    public async Task<IActionResult> GetDetail(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { code = 400, message = "餐桌ID不能为空", data = (object?)null });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var detail = await _tableService.GetTableDetailAsync(id.Trim(), baseUrl, cancellationToken);

            if (detail is null)
                return NotFound(new { code = 404, message = "餐桌不存在", data = (object?)null });

            return Ok(new
            {
                code = 200,
                message = "success",
                data = detail
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取餐桌详情失败 - ID: {Id}", id);
            return StatusCode(500, new { code = 500, message = "服务器内部错误", data = (object?)null });
        }
    }

    /// <summary>
    /// 新增餐桌
    /// </summary>
    [HttpPost("add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTableRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Tableno))
                return BadRequest(new { code = 400, message = "餐桌号不能为空", data = (object?)null });

            if (dto.Capacity < 1 || dto.Capacity > 30)
                return BadRequest(new { code = 400, message = "容纳人数必须在 1-30 之间", data = (object?)null });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _tableService.CreateTableAsync(dto, baseUrl, cancellationToken);

            return Ok(new
            {
                code = 200,
                message = "新增成功",
                data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = 409, message = ex.Message, data = (object?)null });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = 400, message = ex.Message, data = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增餐桌失败");
            return StatusCode(500, new { code = 500, message = "服务器内部错误", data = (object?)null });
        }
    }

    /// <summary>
    /// 更新餐桌信息
    /// </summary>
    [HttpPost("edit")]
    public async Task<IActionResult> Update(
        [FromBody] UpdateTableRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Id))
                return BadRequest(new { code = 400, message = "餐桌ID不能为空", data = (object?)null });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _tableService.UpdateTableAsync(dto, baseUrl, cancellationToken);

            if (result is null)
                return NotFound(new { code = 404, message = "餐桌不存在", data = (object?)null });

            return Ok(new
            {
                code = 200,
                message = "修改成功",
                data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = 409, message = ex.Message, data = (object?)null });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = 400, message = ex.Message, data = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新餐桌失败 - ID: {Id}", dto.Id);
            return StatusCode(500, new { code = 500, message = "服务器内部错误", data = (object?)null });
        }
    }

    /// <summary>
    /// 删除餐桌
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteTableRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Id))
                return BadRequest(new { code = 400, message = "餐桌ID不能为空", data = (object?)null });

            var success = await _tableService.DeleteTableAsync(dto.Id.Trim(), cancellationToken);

            if (!success)
                return NotFound(new { code = 404, message = "餐桌不存在", data = (object?)null });

            return Ok(new
            {
                code = 200,
                message = "删除成功",
                data = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除餐桌失败 - ID: {Id}", dto.Id);
            return StatusCode(500, new { code = 500, message = "服务器内部错误", data = (object?)null });
        }
    }

    /// <summary>
    /// 更新餐桌状态
    /// </summary>
    [HttpPost("status")]
    public async Task<IActionResult> UpdateStatus(
        [FromBody] UpdateTableStatusRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Tableno))
                return BadRequest(new { code = 400, message = "餐桌号不能为空", data = (object?)null });

            if (dto.Status < 1 || dto.Status > 3)
                return BadRequest(new { code = 400, message = "状态值不正确，仅支持 1=空闲, 2=停用, 3=使用中", data = (object?)null });

            var result = await _tableService.UpdateTableStatusAsync(dto, cancellationToken);

            if (result is null)
                return NotFound(new { code = 404, message = "餐桌不存在", data = (object?)null });

            return Ok(new
            {
                code = 200,
                message = "状态更新成功",
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新餐桌状态失败 - Tableno: {Tableno}", dto.Tableno);
            return StatusCode(500, new { code = 500, message = "服务器内部错误", data = (object?)null });
        }
    }
}
