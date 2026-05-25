using Microsoft.AspNetCore.Mvc;

using WebAPI.Common;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/common")]
public class CommonController : ControllerBase
{
    private readonly ICommonService _commonService;

    public CommonController(ICommonService commonService)
    {
        _commonService = commonService;
    }

    /// <summary>
    /// 文件上传
    /// 返回 data 为文件路径字符串（非对象），直接用于 image/videoUrl/carouselMedia.url
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var url = await _commonService.UploadAsync(file, cancellationToken);
        return Ok(ApiResult.Success(url));  // data 直接返回字符串，非 {url: ...}
    }
}
