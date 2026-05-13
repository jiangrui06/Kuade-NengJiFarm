using Microsoft.AspNetCore.Mvc;

using ManageAPI.Common;
using ManageAPI.Dtos;
using ManageAPI.Services;

namespace ManageAPI.Controllers;

[ApiController]
[Route("api/dining-table")]
public class DiningTableController : ControllerBase
{
    private readonly IDiningTableService _diningTableService;

    public DiningTableController(IDiningTableService diningTableService)
    {
        _diningTableService = diningTableService;
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
        if (string.IsNullOrWhiteSpace(dto.TableNo))
            return Ok(ApiResult.Fail("桌号不能为空", 400));

        if (dto.SeatCount <= 0)
            return Ok(ApiResult.Fail("座位数必须大于0", 400));

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

        return Ok(ApiResult.Success("停用成功"));
    }
}

public class DeleteDiningTableRequest
{
    public string Id { get; set; } = string.Empty;
}
