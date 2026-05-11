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
    private string FarmPath => Path.Combine(WebRootPath, "farm");
    private string VideoPath => Path.Combine(WebRootPath, "videos");
    private string UploadPath => Path.Combine(WebRootPath, "uploads");

    public FileController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("images")]
    public IActionResult ListImages()
    {
        var files = EnumerateImageFolders()
            .SelectMany(folder => Directory.Exists(folder)
                ? Directory.GetFiles(folder).Where(IsImageFile).Select(Path.GetFileName)
                : [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(ApiResult.Success(new { files }));
    }

    [HttpGet("image/{*fileName}")]
    public IActionResult GetImage(string fileName)
    {
        var normalizedInput = NormalizeImageLookupInput(fileName);
        var filePath = ResolveSafeFile(normalizedInput, EnumerateImageFolders(), allowSpaceFallback: true);
        if (filePath is null)
        {
            return NotFound("image not found");
        }

        return PhysicalFile(filePath, GetContentType(filePath));
    }

    [HttpGet("videos")]
    public IActionResult ListVideos()
    {
        var files = Directory.Exists(VideoPath)
            ? Directory.GetFiles(VideoPath).Where(IsVideoFile).Select(Path.GetFileName).ToList()
            : [];

        return Ok(ApiResult.Success(new { path = "wwwroot/videos", files }));
    }
    [HttpGet("video/{*fileName}")]
    public IActionResult GetVideo(string fileName)
    {
        var filePath = ResolveSafeFile(fileName, [VideoPath]);
        if (filePath is null)
        {
            return NotFound("video not found");
        }

        return PhysicalFile(filePath, GetContentType(filePath, "video/mp4"), enableRangeProcessing: true);
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
    [HttpGet("uploads/{*fileName}")]
    public IActionResult GetUploadedFile(string fileName)
    {
        var filePath = ResolveSafeFile(fileName, [UploadPath]);
        if (filePath is null)
        {
            return NotFound("file not found");
        }

        return PhysicalFile(filePath, GetContentType(filePath));
    }

    private async Task<IActionResult> UploadFileCore(IFormFile file, string? path)
    {
        if (file == null || file.Length == 0)
        {
            return Ok(ApiResult.Fail("file is required", 400));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return Ok(ApiResult.Fail("file size cannot exceed 5MB", 400));
        }

        if (!IsImageFile(file))
        {
            return Ok(ApiResult.Fail("only image files are allowed", 400));
        }

        var relativeFolder = NormalizeUploadFolder(path);
        var targetFolder = string.IsNullOrWhiteSpace(relativeFolder)
            ? UploadPath
            : Path.Combine(UploadPath, relativeFolder);

        if (!IsUnderFolder(targetFolder, UploadPath))
        {
            return Ok(ApiResult.Fail("upload path is invalid", 400));
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
            ? $"/api/file/uploads/{fileName}"
            : $"/api/file/uploads/{relativeFolder.Replace("\\", "/", StringComparison.Ordinal)}/{fileName}";

        return Ok(ApiResult.Success(new
        {
            url = relativeUrl,
            filename = fileName,
            path = relativeUrl
        }));
    }

    private string[] EnumerateImageFolders()
    {
        return [IconPath, FarmPath, UploadPath];
    }

    private static string NormalizeImageLookupInput(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var normalized = fileName.Trim().Replace('\\', '/');
        while (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        // Backward compatibility:
        // some old payloads store image paths like "images/farm/Farm_29.jpg",
        // but /api/file/image expects just the file name.
        if (normalized.StartsWith("images/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("farm/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        {
            var fileOnly = Path.GetFileName(normalized);
            if (!string.IsNullOrWhiteSpace(fileOnly))
            {
                return fileOnly;
            }
        }

        return normalized;
    }

    private static string? ResolveSafeFile(string fileName, IEnumerable<string> folders, bool allowSpaceFallback = false)
    {
        var normalizedName = fileName.Trim().TrimStart('/', '\\');
        if (string.IsNullOrWhiteSpace(normalizedName)
            || normalizedName.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalizedName))
        {
            return null;
        }

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var filePath = Path.Combine(folder, normalizedName);
            if (IsUnderFolder(filePath, folder) && System.IO.File.Exists(filePath))
            {
                return filePath;
            }

            if (allowSpaceFallback && normalizedName.Contains(' ', StringComparison.Ordinal))
            {
                var fallbackPath = Path.Combine(folder, normalizedName.Replace(' ', '_'));
                if (IsUnderFolder(fallbackPath, folder) && System.IO.File.Exists(fallbackPath))
                {
                    return fallbackPath;
                }
            }
        }

        return null;
    }

    private static bool IsUnderFolder(string path, string folder)
    {
        var fullPath = Path.GetFullPath(path);
        var fullFolder = Path.GetFullPath(folder);
        return fullPath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUploadFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim().Trim('/');
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
        return file.Length > 0
               && (IsImageFile(file.FileName)
                   || (!string.IsNullOrWhiteSpace(file.ContentType)
                       && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsImageFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    private static bool IsVideoFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv";
    }

    private static string GetContentType(string filePath, string fallback = "application/octet-stream")
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(filePath, out var contentType) ? contentType : fallback;
    }
}
