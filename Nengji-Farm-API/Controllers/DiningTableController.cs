using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/dining-table")]
public class DiningTableController : ControllerBase
{
    private readonly IDiningTableService _diningTableService;

    public DiningTableController(IDiningTableService diningTableService)
    {
        _diningTableService = diningTableService;
    }

    [HttpGet("statuses")]
    public async Task<IActionResult> GetStatuses(
        [FromQuery] string? scope = "form",
        CancellationToken cancellationToken = default)
    {
        var statuses = await _diningTableService.GetStatusesAsync(cancellationToken, scope);
        return Ok(ApiResult.Success(statuses));
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var (records, total) = await _diningTableService.GetListAsync(pageNum, pageSize, keyword, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            records,
            total,
            pageNum,
            pageSize,
            pages = (total + pageSize - 1) / pageSize
        }));
    }

    [HttpPost("add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateDiningTableDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto is null)
            return Ok(ApiResult.Fail("请求参数不能为空", 400));

        if (string.IsNullOrWhiteSpace(dto.TableNo))
            return Ok(ApiResult.Fail("桌号不能为空", 400));

        if (dto.SeatCount <= 0)
            return Ok(ApiResult.Fail("座位数必须大于0", 400));

        var (normalized, error) = TableNoHelper.Normalize(dto.TableNo);
        if (error != null)
            return Ok(ApiResult.Fail(error, 400));

        dto.TableNo = normalized;

        var id = await _diningTableService.CreateAsync(dto, cancellationToken);
        return Ok(ApiResult.Success(new { id }));
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteDiningTableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Id))
            return Ok(ApiResult.Fail("参数不能为空", 400));

        var success = await _diningTableService.DeleteAsync(request.Id, cancellationToken);

        if (!success)
            return Ok(ApiResult.Fail("桌台不存在", 404));

        return Ok(ApiResult.Success("删除成功"));
    }
}

public class DeleteDiningTableRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// 餐桌编号校验与格式化：只允许 "X号桌" 格式（如 1号桌、12号桌），
/// 纯数字会自动补上 "号桌" 后缀。
/// </summary>
internal static class TableNoHelper
{
    public static (string? Normalized, string? Error) Normalize(string? input)
    {
        var raw = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return (null, "桌号不能为空");

        // 已经是 "X号桌" 格式
        if (raw.EndsWith("号桌", StringComparison.Ordinal))
        {
            var prefix = raw[..^2];
            if (int.TryParse(prefix, out _) && prefix.Length > 0)
                return (raw, null);
            return (null, $"桌号格式不正确，请输入数字 + \"号桌\"，例如: 1号桌");
        }

        // 纯数字 → 自动补 "号桌"
        if (int.TryParse(raw, out var num))
            return ($"{num}号桌", null);

        return (null, $"桌号格式不正确，请输入数字 + \"号桌\"，例如: 1号桌");
    }
}
