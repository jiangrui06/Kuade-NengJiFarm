using System.Text.Json;

namespace WebAPI.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Services;

[ApiController]
[Route("api/dish")]
public class DishController : ControllerBase
{
    private readonly IDishService _dishService;
    private readonly ManageAppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public DishController(IDishService dishService, ManageAppDbContext dbContext, IWebHostEnvironment env)
    {
        _dishService = dishService;
        _dbContext = dbContext;
        _env = env;
    }

    /// <summary>
    /// 获取菜品状态列表
    /// </summary>
    [HttpGet("statuses")]
    public async Task<IActionResult> GetStatuses(CancellationToken cancellationToken)
    {
        try
        {
            var statuses = await _dbContext.DishStatuses
                .OrderBy(s => s.DishStatusId)
                .Select(s => new
                {
                    statusId = s.DishStatusId,
                    statusName = s.StatusName
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(statuses));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取状态列表失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取菜品类型列表
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _dbContext.DishCategories
                .OrderBy(c => c.DishSortOrder)
                .Select(c => new
                {
                    categoryId = c.DishCategoryId,
                    categoryName = c.DishCategoryName
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(categories));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取类型列表失败：{ex.Message}", 500));
        }
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

            // 保存后异步压缩视频
            MediaHelper.QueueVideoCompression(dto.Image, _env.WebRootPath);
            foreach (var s in dto.SpecImages)
                MediaHelper.QueueVideoCompression(s.Url, _env.WebRootPath);

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

            // 保存后异步压缩视频
            MediaHelper.QueueVideoCompression(dto.Image, _env.WebRootPath);
            foreach (var s in dto.SpecImages)
                MediaHelper.QueueVideoCompression(s.Url, _env.WebRootPath);

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
        // 封面图：优先取上传文件 → 否则取表单字段已有值
        var imageFile = form.Files.GetFile("image");
        string image;
        if (imageFile != null && imageFile.Length > 0)
        {
            image = await MediaHelper.SaveFileAsync(imageFile, _env.WebRootPath);
        }
        else
        {
            image = form["image"].FirstOrDefault() ?? string.Empty;
        }

        // 轮播媒体：从 JSON 解析 + 索引文件匹配
        var carouselMedia = await ParseCarouselMediaFromFormAsync(form);

        // 规格图片：同上
        var specImages = await ParseSpecImagesFromFormAsync(form);

        return new CreateDishDto
        {
            Name = form["name"].FirstOrDefault() ?? string.Empty,
            Price = decimal.TryParse(form["price"].FirstOrDefault(), out var p) ? p : 0,
            Stock = int.TryParse(form["stock"].FirstOrDefault(), out var s) ? s : 0,
            Status = form["status"].FirstOrDefault() ?? "已上架",
            Image = image,
            CarouselMedia = carouselMedia,
            SpecImages = specImages,
            Description = form["description"].FirstOrDefault() ?? string.Empty,
            DishType = form["dishType"].FirstOrDefault(),
        };
    }

    private async Task<List<CarouselMediaDto>> ParseCarouselMediaFromFormAsync(IFormCollection form)
    {
        var list = new List<CarouselMediaDto>();

        // 1. 从 JSON 反序列化完整列表
        var json = form["carouselMedia"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                list = JsonSerializer.Deserialize<List<CarouselMediaDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            catch
            {
                list = [];
            }
        }

        // 2. 索引文件 carouselMedia_file_N 匹配
        foreach (var file in form.Files)
        {
            if (!file.Name.StartsWith("carouselMedia_file_", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Length == 0) continue;

            var indexStr = file.Name["carouselMedia_file_".Length..];
            if (!int.TryParse(indexStr, out var index) || index < 0 || index >= list.Count)
                continue;
            if (!string.IsNullOrEmpty(list[index].Url))
                continue;

            var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
            if (!string.IsNullOrEmpty(url))
            {
                list[index].Url = url;
                list[index].Type = MediaHelper.IsVideoUrl(file.FileName) ? "video" : "image";
            }
        }

        // 3. 兼容旧版：直接传 carouselMedia 文件（追加）
        if (list.Count == 0)
        {
            foreach (var file in form.Files.GetFiles("carouselMedia"))
            {
                if (file.Length == 0) continue;
                var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
                if (!string.IsNullOrEmpty(url))
                {
                    list.Add(new CarouselMediaDto
                    {
                        Type = MediaHelper.IsVideoUrl(file.FileName) ? "video" : "image",
                        Url = url,
                        SortOrder = list.Count,
                    });
                }
            }
        }

        for (var i = 0; i < list.Count; i++)
            list[i].SortOrder = i;

        return list;
    }

    private async Task<List<SpecImageItemDto>> ParseSpecImagesFromFormAsync(IFormCollection form)
    {
        var list = new List<SpecImageItemDto>();

        var json = form["specImages"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                list = JsonSerializer.Deserialize<List<SpecImageItemDto>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            catch
            {
                list = [];
            }
        }

        // 索引文件 specImages_file_N
        foreach (var file in form.Files)
        {
            if (!file.Name.StartsWith("specImages_file_", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Length == 0) continue;

            var indexStr = file.Name["specImages_file_".Length..];
            if (!int.TryParse(indexStr, out var index) || index < 0 || index >= list.Count)
                continue;
            if (!string.IsNullOrEmpty(list[index].Url))
                continue;

            var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
            if (!string.IsNullOrEmpty(url))
                list[index].Url = url;
        }

        if (list.Count == 0)
        {
            foreach (var file in form.Files.GetFiles("specImages"))
            {
                if (file.Length == 0) continue;
                var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
                if (!string.IsNullOrEmpty(url))
                    list.Add(new SpecImageItemDto { Url = url, SortOrder = list.Count });
            }
        }

        for (var i = 0; i < list.Count; i++)
            list[i].SortOrder = i;

        return list;
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
            CarouselMedia = baseDto.CarouselMedia,
            SpecImages = baseDto.SpecImages,
            Description = baseDto.Description,
            DishType = baseDto.DishType,
        };
    }
}
