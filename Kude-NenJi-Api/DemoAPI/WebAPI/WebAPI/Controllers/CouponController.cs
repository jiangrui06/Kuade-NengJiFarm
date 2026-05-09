using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/coupon")]
public class CouponController : ControllerBase
{
    private readonly ICouponService _couponService;

    public CouponController(ICouponService couponService)
    {
        _couponService = couponService;
    }

    /// <summary>
    /// 获取券品列表
    /// </summary>
    //[HttpGet("list")]
    //public async Task<IActionResult> GetList(
    //    [FromQuery] int pageNum = 1,
    //    [FromQuery] int pageSize = 15,
    //    [FromQuery] string? keyword = null,
    //    CancellationToken cancellationToken = default)
    //{
    //    try
    //    {
    //        var (records, total) = await _couponService.GetCouponListAsync(pageNum, pageSize, keyword, cancellationToken);

    //        return Ok(ApiResult.Success(new
    //        {
    //            records,
    //            total,
    //            pageNum,
    //            pageSize,
    //            pages = (total + pageSize - 1) / pageSize
    //        }));
    //    }
    //    catch (Exception ex)
    //    {
    //        return Ok(ApiResult.Fail($"获取失败：{ex.Message}", 500));
    //    }
    //}

    /// <summary>
    /// 获取券品详情
    /// </summary>
    [HttpGet("detail")]
    //public async Task<IActionResult> GetDetail(
    //    [FromQuery] string id,
    //    CancellationToken cancellationToken = default)
    //{
    //    try
    //    {
    //        if (string.IsNullOrWhiteSpace(id))
    //        {
    //            return Ok(ApiResult.Fail("券品ID不能为空", 400));
    //        }

    //        var coupon = await _couponService.GetCouponDetailAsync(id, cancellationToken);
    //        if (coupon is null)
    //        {
    //            return Ok(ApiResult.Fail("券品不存在或已被删除", 404));
    //        }

    //        return Ok(ApiResult.Success(coupon));
    //    }
    //    catch (Exception ex)
    //    {
    //        return Ok(ApiResult.Fail($"获取失败：{ex.Message}", 500));
    //    }
    //}

    /// <summary>
    /// 新增券品
    /// </summary>
    [HttpPost("add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCouponDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Ok(ApiResult.Fail("请求参数不完整或格式错误", 400));
            }

            var couponCode = await _couponService.CreateCouponAsync(dto, cancellationToken);
            return Ok(ApiResult.Success(new { id = couponCode }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"新增失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 编辑券品
    /// </summary>
    [HttpPut("edit")]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCouponDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Id))
            {
                return Ok(ApiResult.Fail("请求参数不完整或格式错误", 400));
            }

            var success = await _couponService.UpdateCouponAsync(dto, cancellationToken);
            if (!success)
            {
                return Ok(ApiResult.Fail("券品不存在或已被删除", 404));
            }

            return Ok(ApiResult.Success("编辑成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"编辑失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 删除券品
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteCouponRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Id))
            {
                return Ok(ApiResult.Fail("券品ID不能为空", 400));
            }

            var success = await _couponService.DeleteCouponAsync(request.Id, cancellationToken);
            if (!success)
            {
                return Ok(ApiResult.Fail("券品不存在或已被删除", 404));
            }

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 批量删除券品
    /// </summary>
    [HttpPost("deleteBatch")]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] DeleteBatchCouponRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Ids == null || request.Ids.Length == 0)
            {
                return Ok(ApiResult.Fail("券品ID不能为空", 400));
            }

            var success = await _couponService.DeleteCouponBatchAsync(request.Ids, cancellationToken);
            if (!success)
            {
                return Ok(ApiResult.Fail("删除失败", 404));
            }

            return Ok(ApiResult.Success("批量删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }
}