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

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var url = await _commonService.UploadAsync(file, cancellationToken);
        return Ok(ApiResult.Success(new { url }));
    }
}
