namespace ManageAPI.Controllers;

using Microsoft.AspNetCore.Mvc;

using ManageAPI.Common;
using ManageAPI.Dtos;
using ManageAPI.Services;

[ApiController]
[Route("api/product")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IWebHostEnvironment _env;

    public ProductController(IProductService productService, IWebHostEnvironment env)
    {
        _productService = productService;
        _env = env;
    }

    /// <summary>
    /// 获取产品列表
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (records, total) = await _productService.GetProductListAsync(pageNum, pageSize, keyword, cancellationToken);

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
    /// 获取产品详情
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
                return Ok(ApiResult.Fail("ID参数不正确", 400));
            }

            var product = await _productService.GetProductDetailAsync(id, cancellationToken);
            if (product is null)
            {
                return Ok(ApiResult.Fail("产品不存在或已被删除", 404));
            }

            return Ok(ApiResult.Success(product));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 创建产品 — 支持 multipart/form-data（传文件）和 application/json（兼容旧版）
    /// multipart 模式：服务端自己收图片文件、落盘、生成文件名，保证 DB 路径和本地文件名永远一致。
    /// </summary>
    [HttpPost("add")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        try
        {
            CreateProductDto dto;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                dto = await BuildCreateDtoFromFormAsync(form);
            }
            else
            {
                dto = (await Request.ReadFromJsonAsync<CreateProductDto>(cancellationToken: cancellationToken))!;
            }

            if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                return Ok(ApiResult.Fail("产品名称不能为空", 400));

            var id = await _productService.CreateProductAsync(dto, cancellationToken);
            return Ok(ApiResult.Success(new { id }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"创建失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 编辑产品 — 支持 multipart/form-data 和 application/json
    /// </summary>
    [HttpPut("edit")]
    [HttpPost("edit")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Update(CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateProductDto dto;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                dto = await BuildUpdateDtoFromFormAsync(form);
            }
            else
            {
                dto = (await Request.ReadFromJsonAsync<UpdateProductDto>(cancellationToken: cancellationToken))!;
            }

            if (dto is null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.Name))
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _productService.UpdateProductAsync(dto, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("产品不存在或已被删除", 404));

            return Ok(ApiResult.Success("编辑成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"编辑失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 删除产品
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteProductRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Id <= 0)
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _productService.DeleteProductAsync(request.Id, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("产品不存在或已被删除", 404));

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 批量删除产品
    /// </summary>
    [HttpPost("deleteBatch")]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] DeleteBatchProductRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Ids == null || request.Ids.Length == 0)
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _productService.DeleteProductBatchAsync(request.Ids, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("删除失败", 404));

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取商品分类列表
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await _productService.GetCategoriesAsync(cancellationToken);
            return Ok(ApiResult.Success(categories));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取分类失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取已启用的单位列表
    /// </summary>
    [HttpGet("units")]
    public async Task<IActionResult> GetUnits(CancellationToken cancellationToken = default)
    {
        try
        {
            var units = await _productService.GetUnitsAsync(cancellationToken);
            return Ok(ApiResult.Success(units));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取单位失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取产品管理统计数据
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _productService.GetProductStatsAsync(cancellationToken);
            return Ok(ApiResult.Success(stats));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取统计失败：{ex.Message}", 500));
        }
    }

    private async Task<CreateProductDto> BuildCreateDtoFromFormAsync(IFormCollection form)
    {
        var coverImageFile = form.Files.GetFile("coverImage");
        var coverImage = await MediaHelper.SaveFileAsync(coverImageFile, _env.WebRootPath);

        var carouselMedia = new List<CarouselMediaDto>();
        foreach (var file in form.Files.GetFiles("carouselMedia"))
        {
            var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
            if (!string.IsNullOrEmpty(url))
            {
                carouselMedia.Add(new CarouselMediaDto
                {
                    Type = MediaHelper.IsVideoUrl(file.FileName) ? "video" : "image",
                    Url = url
                });
            }
        }

        var specImages = new List<string>();
        foreach (var file in form.Files.GetFiles("specImages"))
        {
            var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
            if (!string.IsNullOrEmpty(url))
                specImages.Add(url);
        }

        return new CreateProductDto
        {
            Name = form["name"].FirstOrDefault() ?? string.Empty,
            Price = decimal.TryParse(form["price"].FirstOrDefault(), out var p) ? p : 0,
            Stock = int.TryParse(form["stock"].FirstOrDefault(), out var s) ? s : 0,
            Status = form["status"].FirstOrDefault() ?? "已下架",
            CoverImage = coverImage,
            CarouselMedia = carouselMedia,
            NetWeight = decimal.TryParse(form["netWeight"].FirstOrDefault(), out var nw) ? nw : null,
            WeightUnit = form["weightUnit"].FirstOrDefault() ?? string.Empty,
            StorageCondition = form["storageCondition"].FirstOrDefault() ?? string.Empty,
            SpecImages = specImages,
            Description = form["description"].FirstOrDefault() ?? string.Empty
        };
    }

    private async Task<UpdateProductDto> BuildUpdateDtoFromFormAsync(IFormCollection form)
    {
        var baseDto = await BuildCreateDtoFromFormAsync(form);
        return new UpdateProductDto
        {
            Id = int.TryParse(form["id"].FirstOrDefault(), out var id) ? id : 0,
            Name = baseDto.Name,
            Price = baseDto.Price,
            Stock = baseDto.Stock,
            Status = baseDto.Status,
            CoverImage = baseDto.CoverImage,
            CarouselMedia = baseDto.CarouselMedia,
            NetWeight = baseDto.NetWeight,
            WeightUnit = baseDto.WeightUnit,
            StorageCondition = baseDto.StorageCondition,
            SpecImages = baseDto.SpecImages,
            Description = baseDto.Description
        };
    }
}

public class DeleteProductRequest
{
    public int Id { get; set; }
}

public class DeleteBatchProductRequest
{
    public int[]? Ids { get; set; }
}
