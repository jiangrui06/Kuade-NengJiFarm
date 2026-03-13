using Microsoft.AspNetCore.Mvc;
using WebAPI.Common;
using WebAPI.Services;

namespace WebAPI.Controllers;

//[ApiController]
//[Route("api/home")]
//public class HomeController : ControllerBase
//{
//    private readonly IAppService _appService;

//    public HomeController(IAppService appService)
//    {
//        _appService = appService;
//    }

//    [HttpGet("index")]
//    public async Task<ActionResult<ApiResult>> Index(CancellationToken cancellationToken)
//    {
//        var data = await _appService.GetHomeIndexAsync(cancellationToken);
//        return Ok(ApiResult.Success(data));
//    }
//}
