namespace ManageAPI.Controllers;

using Microsoft.AspNetCore.Mvc;

using ManageAPI.Common;
using ManageAPI.Dtos;
using ManageAPI.Services;

[ApiController]
[Route("api/dish")]
public class DishController : ControllerBase
{
    private readonly IDishService _dishService;
    private readonly IWebHostEnvironment _env;

    public DishController(IDishService dishService, IWebHostEnvironment env)
    {
        _dishService = dishService;
        _env = env;
    }

    /// <summary>
    /// 获取菜品列表
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (records, total) = await _dishService.GetDishListAsync(pageNum, pageSize, keyword, status, cancellationToken);

            return Ok(ApiResult.Success(new
            {
                records,
                total,
                pageNum,
                pageSize,
                pages = (total + pageSize - 1) / pageSize
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取菜品详情
    /// </summary>
    [HttpGet("detail")]
    public async Task<IActionResult> GetDetail(
        [FromQuery] int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
            {
                return Ok(ApiResult.Fail("菜品ID不正确", 400));
            }

            var dish = await _dishService.GetDishDetailAsync(id, cancellationToken);
            if (dish is null)
            {
                return Ok(ApiResult.Fail("菜品不存在或已被删除", 404));
            }

            return Ok(ApiResult.Success(dish));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 创建菜品 — 支持 multipart/form-data（传文件）和 application/json（兼容旧版）
    /// </summary>
    [HttpPost("add")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        try
        {
            CreateDishDto dto;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                dto = await BuildCreateDtoFromFormAsync(form);
            }
            else
            {
                dto = (await Request.ReadFromJsonAsync<CreateDishDto>(cancellationToken: cancellationToken))!;
            }

            if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                return Ok(ApiResult.Fail("菜品名称不能为空", 400));

            var id = await _dishService.CreateDishAsync(dto, cancellationToken);
            return Ok(ApiResult.Success(new { id }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"创建失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 编辑菜品 — 支持 multipart/form-data 和 application/json
    /// </summary>
    [HttpPut("edit")]
    [HttpPost("edit")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Update(CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateDishDto dto;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                dto = await BuildUpdateDtoFromFormAsync(form);
            }
            else
            {
                dto = (await Request.ReadFromJsonAsync<UpdateDishDto>(cancellationToken: cancellationToken))!;
            }

            if (dto is null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.Name))
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _dishService.UpdateDishAsync(dto, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("菜品不存在或已被删除", 404));

            return Ok(ApiResult.Success("编辑成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"编辑失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 删除菜品
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteDishRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Id <= 0)
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _dishService.DeleteDishAsync(request.Id, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("菜品不存在或已被删除", 404));

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 批量删除菜品
    /// </summary>
    [HttpPost("deleteBatch")]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] DeleteBatchDishRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Ids == null || request.Ids.Length == 0)
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _dishService.DeleteDishBatchAsync(request.Ids, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("删除失败", 404));

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    private async Task<CreateDishDto> BuildCreateDtoFromFormAsync(IFormCollection form)
    {
        var imageFile = form.Files.GetFile("image");
        var image = await MediaHelper.SaveFileAsync(imageFile, _env.WebRootPath);

        var specImages = new List<string>();
        foreach (var file in form.Files.GetFiles("specImages"))
        {
            var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
            if (!string.IsNullOrEmpty(url))
                specImages.Add(url);
        }

        return new CreateDishDto
        {
            Name = form["name"].FirstOrDefault() ?? string.Empty,
            Price = decimal.TryParse(form["price"].FirstOrDefault(), out var p) ? p : 0,
            Stock = int.TryParse(form["stock"].FirstOrDefault(), out var s) ? s : 0,
            Status = form["status"].FirstOrDefault() ?? "已上架",
            Image = image,
            SpecImages = specImages,
            Description = form["description"].FirstOrDefault() ?? string.Empty,
        };
    }

    private async Task<UpdateDishDto> BuildUpdateDtoFromFormAsync(IFormCollection form)
    {
        var baseDto = await BuildCreateDtoFromFormAsync(form);
        return new UpdateDishDto
        {
            Id = int.TryParse(form["id"].FirstOrDefault(), out var id) ? id : 0,
            Name = baseDto.Name,
            Price = baseDto.Price,
            Stock = baseDto.Stock,
            Status = baseDto.Status,
            Image = baseDto.Image,
            SpecImages = baseDto.SpecImages,
            Description = baseDto.Description,
        };
    }
}
