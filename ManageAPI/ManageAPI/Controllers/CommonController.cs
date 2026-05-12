using Microsoft.AspNetCore.Mvc;

using ManageAPI.Common;

namespace ManageAPI.Controllers;

[ApiController]
[Route("api/common")]
public class CommonController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public CommonController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return Ok(ApiResult.Fail("请选择要上传的文件", 400));

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".mov", ".avi" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return Ok(ApiResult.Fail("不支持的文件格式", 400));

        if (file.Length > 50 * 1024 * 1024)
            return Ok(ApiResult.Fail("文件大小不能超过50MB", 400));

        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        if (!Directory.Exists(uploadsDir))
            Directory.CreateDirectory(uploadsDir);

        var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N[..8]}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var url = $"/uploads/{fileName}";

        return Ok(ApiResult.Success(new { url }));
    }
}
