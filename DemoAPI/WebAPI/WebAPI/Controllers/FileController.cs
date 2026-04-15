using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

using WebAPI.Common;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/file")]
public class FileController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    private string WebRootPath => _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
    private string IconPath => Path.Combine(WebRootPath, "icons");
    private string VideoPath => Path.Combine(WebRootPath, "videos");
    private string UploadPath => Path.Combine(WebRootPath, "uploads");

    public FileController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("images")]
    public IActionResult ListImages()
    {
        try
        {
            if (!Directory.Exists(IconPath))
            {
                return Ok(ApiResult.Fail("图标目录不存在", 404));
            }

            var files = Directory.GetFiles(IconPath)
                .Where(IsImageFile)
                .Select(Path.GetFileName)
                .ToList();

            return Ok(ApiResult.Success(new
            {
                path = "wwwroot/icons",
                files
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取图片列表失败：{ex.Message}"));
        }
    }

    [HttpGet("image/{fileName}")]
    public IActionResult GetImage(string fileName)
    {
        try
        {
            var filePath = Path.Combine(IconPath, fileName);
            if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(IconPath), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("无效的文件名");
            }

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("图片不存在");
            }

            return PhysicalFile(filePath, GetContentType(filePath));
        }
        catch (Exception ex)
        {
            return BadRequest($"获取图片失败：{ex.Message}");
        }
    }

    [HttpGet("videos")]
    public IActionResult ListVideos()
    {
        try
        {
            if (!Directory.Exists(VideoPath))
            {
                return Ok(ApiResult.Fail("视频目录不存在", 404));
            }

            var files = Directory.GetFiles(VideoPath)
                .Where(IsVideoFile)
                .Select(Path.GetFileName)
                .ToList();

            return Ok(ApiResult.Success(new
            {
                path = "wwwroot/videos",
                files
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取视频列表失败：{ex.Message}"));
        }
    }

    [HttpGet("video/{fileName}")]
    public IActionResult GetVideo(string fileName)
    {
        try
        {
            var filePath = Path.Combine(VideoPath, fileName);
            if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(VideoPath), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("无效的文件名");
            }

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("视频不存在");
            }

            return PhysicalFile(filePath, GetContentType(filePath, "video/mp4"), enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return BadRequest($"获取视频失败：{ex.Message}");
        }
    }

    [HttpPost("upload")]
    public Task<IActionResult> UploadFile(IFormFile file, [FromForm] string? path = null)
    {
        return UploadFileCore(file, path);
    }

    [HttpPost("upload/avatar")]
    public Task<IActionResult> UploadAvatar(IFormFile file)
    {
        return UploadFileCore(file, "avatar");
    }

    [HttpGet("uploads/{fileName}")]
    public IActionResult GetUploadedFile(string fileName)
    {
        try
        {
            var filePath = Path.Combine(UploadPath, fileName);
            if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(UploadPath), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("无效的文件名");
            }

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("文件不存在");
            }

            return PhysicalFile(filePath, GetContentType(filePath));
        }
        catch (Exception ex)
        {
            return BadRequest($"获取文件失败：{ex.Message}");
        }
    }

    private async Task<IActionResult> UploadFileCore(IFormFile file, string? path)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return Ok(ApiResult.Fail("没有选择文件", 400));
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return Ok(ApiResult.Fail("文件大小不能超过5MB", 400));
            }

            if (!IsImageFile(file))
            {
                return Ok(ApiResult.Fail("只能上传图片文件", 400));
            }

            var relativeFolder = NormalizeUploadFolder(path);
            var targetFolder = string.IsNullOrWhiteSpace(relativeFolder)
                ? UploadPath
                : Path.Combine(UploadPath, relativeFolder);

            if (!Path.GetFullPath(targetFolder).StartsWith(Path.GetFullPath(UploadPath), StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResult.Fail("上传路径无效", 400));
            }

            Directory.CreateDirectory(targetFolder);

            var ext = GetImageExtension(file);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(targetFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = string.IsNullOrWhiteSpace(relativeFolder)
                ? $"/uploads/{fileName}"
                : $"/uploads/{relativeFolder.Replace("\\", "/", StringComparison.Ordinal)}/{fileName}";
            var fileUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

            return Ok(ApiResult.Success(new
            {
                url = fileUrl,
                filename = fileName,
                path = relativeUrl
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"上传文件失败：{ex.Message}");
            return Ok(ApiResult.Fail("上传文件失败，请稍后重试"));
        }
    }

    private static string NormalizeUploadFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        normalized = Regex.Replace(normalized, "/{2,}", "/");
        normalized = Regex.Replace(normalized, @"[^a-zA-Z0-9/_-]", string.Empty);
        return normalized;
    }

    private static string GetImageExtension(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (IsImageFile(file.FileName))
        {
            return ext;
        }

        return file.ContentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
    }

    private static bool IsImageFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            return false;
        }

        if (IsImageFile(file.FileName))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(file.ContentType)
               && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".png"
               || ext == ".jpg"
               || ext == ".jpeg"
               || ext == ".gif"
               || ext == ".bmp"
               || ext == ".webp";
    }

    private static bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".mp4"
               || ext == ".mov"
               || ext == ".avi"
               || ext == ".mkv"
               || ext == ".wmv";
    }

    private static string GetContentType(string filePath, string fallback = "application/octet-stream")
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(filePath, out var contentType)
            ? contentType
            : fallback;
    }
}
