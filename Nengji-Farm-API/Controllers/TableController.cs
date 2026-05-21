using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
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

            return Ok(ApiResult.Success(new
            {
                records,
                total,
                pages,
                pageNum
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取餐桌列表失败");
            return Ok(ApiResult.Fail("服务器内部错误", 500));
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
                return Ok(ApiResult.Fail("餐桌ID不能为空", 400));

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var detail = await _tableService.GetTableDetailAsync(id.Trim(), baseUrl, cancellationToken);

            if (detail is null)
                return Ok(ApiResult.Fail("餐桌不存在", 404));

            return Ok(ApiResult.Success(detail));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取餐桌详情失败 - ID: {Id}", id);
            return Ok(ApiResult.Fail("服务器内部错误", 500));
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
                return Ok(ApiResult.Fail("餐桌号不能为空", 400));

            var (normalized, error) = TableNoHelper.Normalize(dto.Tableno);
            if (error != null)
                return Ok(ApiResult.Fail(error, 400));
            dto.Tableno = normalized!;

            if (dto.Capacity < 1 || dto.Capacity > 30)
                return Ok(ApiResult.Fail("容纳人数必须在 1-30 之间", 400));

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _tableService.CreateTableAsync(dto, baseUrl, cancellationToken);

            return Ok(ApiResult.Success(result, "新增成功"));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, 409));
        }
        catch (ArgumentException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增餐桌失败");
            return Ok(ApiResult.Fail("服务器内部错误", 500));
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
                return Ok(ApiResult.Fail("餐桌ID不能为空", 400));

            if (!string.IsNullOrWhiteSpace(dto.Tableno))
            {
                var (normalized, error) = TableNoHelper.Normalize(dto.Tableno);
                if (error != null)
                    return Ok(ApiResult.Fail(error, 400));
                dto.Tableno = normalized;
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _tableService.UpdateTableAsync(dto, baseUrl, cancellationToken);

            if (result is null)
                return Ok(ApiResult.Fail("餐桌不存在", 404));

            return Ok(ApiResult.Success(result, "修改成功"));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, 409));
        }
        catch (ArgumentException ex)
        {
            return Ok(ApiResult.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新餐桌失败 - ID: {Id}", dto.Id);
            return Ok(ApiResult.Fail("服务器内部错误", 500));
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
                return Ok(ApiResult.Fail("餐桌ID不能为空", 400));

            var success = await _tableService.DeleteTableAsync(dto.Id.Trim(), cancellationToken);

            if (!success)
                return Ok(ApiResult.Fail("餐桌不存在", 404));

            return Ok(ApiResult.Success(null, "停用成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除餐桌失败 - ID: {Id}", dto.Id);
            return Ok(ApiResult.Fail("服务器内部错误", 500));
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
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var count = await _tableService.RegenerateAllQrCodesAsync(baseUrl, cancellationToken);
            return Ok(ApiResult.Success(new { count }, $"已重新生成 {count} 张二维码"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新生成二维码失败");
            return Ok(ApiResult.Fail("服务器内部错误", 500));
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
                return Ok(ApiResult.Fail("餐桌号不能为空", 400));

            if (dto.Status < 1 || dto.Status > 3)
                return Ok(ApiResult.Fail("状态值不正确，仅支持 1=空闲, 2=使用中, 3=停用", 400));

            var result = await _tableService.UpdateTableStatusAsync(dto, cancellationToken);

            if (result is null)
                return Ok(ApiResult.Fail("餐桌不存在", 404));

            return Ok(ApiResult.Success(result, "状态更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新餐桌状态失败 - Tableno: {Tableno}", dto.Tableno);
            return Ok(ApiResult.Fail("服务器内部错误", 500));
        }
    }
}
