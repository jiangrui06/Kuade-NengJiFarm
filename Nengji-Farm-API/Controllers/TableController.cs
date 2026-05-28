using Microsoft.AspNetCore.Mvc;

using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

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

            return Ok(new ApiResponses<object>
            {
                Code = 200,
                Message = "success",
                Data = new { records, total, pages, pageNum }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取餐桌列表失败");
            return Ok(new ApiResponse { Code = 500, Message = "服务器内部错误" });
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
                return Ok(new ApiResponse { Code = 400, Message = "餐桌ID不能为空" });

            const string baseUrl = "https://api.nengjifarm.com";
            var detail = await _tableService.GetTableDetailAsync(id.Trim(), baseUrl, cancellationToken);

            if (detail is null)
                return Ok(new ApiResponse { Code = 404, Message = "餐桌不存在" });

            return Ok(new ApiResponses<object> { Code = 200, Message = "success", Data = detail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取餐桌详情失败 - ID: {Id}", id);
            return Ok(new ApiResponse { Code = 500, Message = "服务器内部错误" });
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
            if (dto is null)
                return Ok(new ApiResponse { Code = 400, Message = "请求参数不能为空" });

            if (string.IsNullOrWhiteSpace(dto.Tableno))
                return Ok(new ApiResponse { Code = 400, Message = "餐桌号不能为空" });

            var (normalized, error) = TableNoHelper.Normalize(dto.Tableno);
            if (error != null)
                return Ok(new ApiResponse { Code = 400, Message = error });
            dto.Tableno = normalized!;

            if (dto.Capacity < 1 || dto.Capacity > 30)
                return Ok(new ApiResponse { Code = 400, Message = "容纳人数必须在 1-30 之间" });

            const string baseUrl = "https://api.nengjifarm.com";
            var result = await _tableService.CreateTableAsync(dto, baseUrl, cancellationToken);

            return Ok(new ApiResponses<object> { Code = 200, Message = "新增成功", Data = result });
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResponse { Code = 409, Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Ok(new ApiResponse { Code = 400, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增餐桌失败");
            return Ok(new ApiResponse { Code = 500, Message = "服务器内部错误" });
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
                return Ok(new ApiResponse { Code = 400, Message = "餐桌ID不能为空" });

            if (!string.IsNullOrWhiteSpace(dto.Tableno))
            {
                var (normalized, error) = TableNoHelper.Normalize(dto.Tableno);
                if (error != null)
                    return Ok(new ApiResponse { Code = 400, Message = error });
                dto.Tableno = normalized;
            }

            const string baseUrl = "https://api.nengjifarm.com";
            var result = await _tableService.UpdateTableAsync(dto, baseUrl, cancellationToken);

            if (result is null)
                return Ok(new ApiResponse { Code = 404, Message = "餐桌不存在" });

            return Ok(new ApiResponses<object> { Code = 200, Message = "修改成功", Data = result });
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new ApiResponse { Code = 409, Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Ok(new ApiResponse { Code = 400, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新餐桌失败 - ID: {Id}", dto.Id);
            return Ok(new ApiResponse { Code = 500, Message = "服务器内部错误" });
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
                return Ok(new ApiResponse { Code = 400, Message = "餐桌ID不能为空" });

            var success = await _tableService.DeleteTableAsync(dto.Id.Trim(), cancellationToken);

            if (!success)
                return Ok(new ApiResponse { Code = 404, Message = "餐桌不存在" });

            return Ok(new ApiResponses<object> { Code = 200, Message = "删除成功", Data = null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除餐桌失败 - ID: {Id}", dto.Id);
            return Ok(new ApiResponse { Code = 500, Message = "服务器内部错误" });
        }
    }

    /// <summary>
    /// 重新生成所有餐桌二维码（修复旧数据路径错误）
    /// </summary>
    [HttpPost("regenerate-qrcodes")]
    public async Task<IActionResult> RegenerateQrCodes(CancellationToken cancellationToken)
    {
        try
        {
            const string baseUrl = "https://api.nengjifarm.com";
            var count = await _tableService.RegenerateAllQrCodesAsync(baseUrl, cancellationToken);
            return Ok(new ApiResponses<object> { Code = 200, Message = $"已重新生成 {count} 张二维码", Data = new { count } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新生成二维码失败");
            return Ok(new ApiResponse { Code = 500, Message = "服务器内部错误" });
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
                return Ok(new ApiResponse { Code = 400, Message = "餐桌号不能为空" });

            var validStatuses = await _tableService.GetStatusesAsync(cancellationToken);
            var validIds = validStatuses.Select(s => s.StatusId).ToHashSet();
            if (!validIds.Contains(dto.Status))
            {
                var validDesc = string.Join(", ", validStatuses.Select(s => $"{s.StatusId}={s.StatusName}"));
                return Ok(new ApiResponse { Code = 400, Message = $"状态值不正确，仅支持 {validDesc}" });
            }

            var result = await _tableService.UpdateTableStatusAsync(dto, cancellationToken);

            if (result is null)
                return Ok(new ApiResponse { Code = 404, Message = "餐桌不存在" });

            return Ok(new ApiResponses<object> { Code = 200, Message = "状态更新成功", Data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新餐桌状态失败 - Tableno: {Tableno}", dto.Tableno);
            return Ok(new ApiResponse { Code = 500, Message = "服务器内部错误" });
        }
    }
}
