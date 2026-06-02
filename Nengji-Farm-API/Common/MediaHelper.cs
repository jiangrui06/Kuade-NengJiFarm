namespace WebAPI.Common;

public static class MediaHelper
{
    public static string NormalizeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var trimmed = url.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            return uri.PathAndQuery;
        }

        // Bare filename without path prefix — files live in wwwroot/images/farm/
        if (!trimmed.StartsWith('/') && !trimmed.Contains('/'))
        {
            return $"/images/farm/{trimmed}";
        }

        return trimmed;
    }

    public static bool IsVideoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
        var extension = Path.GetExtension(url).ToLowerInvariant();
        return videoExtensions.Contains(extension);
    }

    /// <summary>
    /// Process image data: if it's base64, save to disk and return the path.
    /// If it's a URL/path, normalize and return. This ensures the stored path
    /// always matches the file on disk.
    /// </summary>
    public static string ProcessImageData(string? imageData, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(imageData))
            return string.Empty;

        var trimmed = imageData.Trim();

        // Base64 data URL: data:image/png;base64,xxxx
        if (trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = trimmed.IndexOf(',');
            if (commaIndex < 0) return string.Empty;

            var mimePart = trimmed[..commaIndex];
            var extension = ".png";
            if (mimePart.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || mimePart.Contains("jpg", StringComparison.OrdinalIgnoreCase))
                extension = ".jpg";
            else if (mimePart.Contains("gif", StringComparison.OrdinalIgnoreCase))
                extension = ".gif";
            else if (mimePart.Contains("webp", StringComparison.OrdinalIgnoreCase))
                extension = ".webp";

            var base64Content = trimmed[(commaIndex + 1)..];
            return SaveBase64ToDisk(base64Content, extension, webRootPath);
        }

        // Try base64 decode + magic byte detection — must be before path checks
        // because JPEG base64 starts with /9j/ which would be mistaken for a path
        var (isImage, ext) = TryDecodeBase64Image(trimmed);
        if (isImage)
            return SaveBase64ToDisk(trimmed, ext, webRootPath);

        // Already a relative path — normalize and return as-is
        if (trimmed.StartsWith('/'))
            return NormalizeImageUrl(trimmed);

        // Full HTTP URL — normalize to relative path
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeImageUrl(trimmed);
        }

        // Default: treat as a path and normalize
        return NormalizeImageUrl(trimmed);
    }

    public static async Task<string> SaveFileAsync(IFormFile? file, string webRootPath)
    {
        if (file is null || file.Length == 0)
            return string.Empty;

        const int maxSize = 50 * 1024 * 1024;
        if (file.Length > maxSize)
            return string.Empty;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
        if (!allowedExts.Contains(ext))
            return string.Empty;

        var fileName = $"{Guid.NewGuid():N}{ext}";

        // 视频文件保存到 wwwroot/videos/，返回 /api/file/video/ 路径（走 FileController）
        var videoExts = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
        if (videoExts.Contains(ext))
        {
            var videoDir = Path.Combine(webRootPath, "videos");
            if (!Directory.Exists(videoDir))
                Directory.CreateDirectory(videoDir);

            var filePath = Path.Combine(videoDir, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // 用 FFmpeg 截取视频第一帧作为缩略图
            var thumbDir = Path.Combine(webRootPath, "thumbs");
            if (!Directory.Exists(thumbDir))
                Directory.CreateDirectory(thumbDir);
            var thumbFileName = Path.ChangeExtension(fileName, ".jpg");
            await GenerateVideoThumbnailAsync(filePath, Path.Combine(thumbDir, thumbFileName));

            return $"/api/file/video/{fileName}";
        }

        // 图片文件保存到 wwwroot/farm/，返回 /images/farm/ 路径（走静态文件）
        var farmDir = Path.Combine(webRootPath, "farm");
        if (!Directory.Exists(farmDir))
            Directory.CreateDirectory(farmDir);

        var farmFilePath = Path.Combine(farmDir, fileName);
        await using var farmStream = new FileStream(farmFilePath, FileMode.Create);
        await file.CopyToAsync(farmStream);

        return $"/images/farm/{fileName}";
    }

    /// <summary>
    /// 用 FFmpeg 截取视频第一帧作为缩略图
    /// </summary>
    private static async Task GenerateVideoThumbnailAsync(string videoPath, string thumbPath)
    {
        try
        {
            if (File.Exists(thumbPath))
                return;

            var thumbDir = Path.GetDirectoryName(thumbPath);
            if (!string.IsNullOrEmpty(thumbDir) && !Directory.Exists(thumbDir))
                Directory.CreateDirectory(thumbDir);

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 -q:v 2 \"{thumbPath}\" -y";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            await process.WaitForExitAsync();
        }
        catch
        {
            // 缩略图生成失败不影响视频本身
        }
    }

    /// <summary>
    /// 根据视频 URL 推导缩略图 URL（命名约定：{guid}.mp4 → {guid}.jpg）
    /// </summary>
    public static string GetVideoThumbUrl(string? videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return string.Empty;

        var fileName = Path.GetFileNameWithoutExtension(videoUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        return $"/api/file/video-thumb/{fileName}.jpg";
    }

    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] GifMagic = { 0x47, 0x49, 0x46, 0x38 };
    private static readonly byte[] WebPMagicRiff = { 0x52, 0x49, 0x46, 0x46 };
    private static readonly byte[] WebPMagicWebp = { 0x57, 0x45, 0x42, 0x50 };

    private static (bool IsImage, string Extension) TryDecodeBase64Image(string data)
    {
        try
        {
            var bytes = Convert.FromBase64String(data);
            if (bytes.Length < 12) return (false, ".png");

            if (bytes.Take(PngMagic.Length).SequenceEqual(PngMagic))
                return (true, ".png");
            if (bytes.Take(JpegMagic.Length).SequenceEqual(JpegMagic))
                return (true, ".jpg");
            if (bytes.Take(GifMagic.Length).SequenceEqual(GifMagic))
                return (true, ".gif");
            if (bytes.Take(WebPMagicRiff.Length).SequenceEqual(WebPMagicRiff)
                && bytes.Skip(8).Take(WebPMagicWebp.Length).SequenceEqual(WebPMagicWebp))
                return (true, ".webp");
        }
        catch
        {
            // Not valid base64
        }

        return (false, ".png");
    }

    private static string SaveBase64ToDisk(string base64Content, string extension, string webRootPath)
    {
        var bytes = Convert.FromBase64String(base64Content);

        var farmDir = Path.Combine(webRootPath, "farm");
        if (!Directory.Exists(farmDir))
            Directory.CreateDirectory(farmDir);

        var guid = Guid.NewGuid().ToString("N")[..8];
        var fileName = $"{guid}{extension}";
        var filePath = Path.Combine(farmDir, fileName);

        File.WriteAllBytes(filePath, bytes);

        return $"/images/farm/{fileName}";
    }
}
