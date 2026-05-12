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

    public ProductController(IProductService productService)
    {
        _productService = productService;
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
    /// 创建产品
    /// </summary>
    [HttpPost("add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Ok(ApiResult.Fail("参数不能为空", 400));
            }

            var id = await _productService.CreateProductAsync(dto, cancellationToken);
            return Ok(ApiResult.Success(new { id }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"创建失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 编辑产品
    /// </summary>
    [HttpPut("edit")]
    public async Task<IActionResult> Update(
        [FromBody] UpdateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.Name))
            {
                return Ok(ApiResult.Fail("参数不能为空", 400));
            }

            var success = await _productService.UpdateProductAsync(dto, cancellationToken);
            if (!success)
            {
                return Ok(ApiResult.Fail("产品不存在或已被删除", 404));
            }

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
            {
                return Ok(ApiResult.Fail("参数不能为空", 400));
            }

            var success = await _productService.DeleteProductAsync(request.Id, cancellationToken);
            if (!success)
            {
                return Ok(ApiResult.Fail("产品不存在或已被删除", 404));
            }

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
            {
                return Ok(ApiResult.Fail("参数不能为空", 400));
            }

            var success = await _productService.DeleteProductBatchAsync(request.Ids, cancellationToken);
            if (!success)
            {
                return Ok(ApiResult.Fail("删除失败", 404));
            }

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
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
