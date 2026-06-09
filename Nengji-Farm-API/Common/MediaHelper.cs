using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using WebAPI.Services;

namespace WebAPI.Common;

public static class MediaHelper
{
    private const int MaxImageDimension = 1920;
    private const int JpegQuality = 80;
    private const int VideoCrf = 28;
    private const int VideoMaxWidth = 1280;
    private const int VideoMaxHeight = 720;

    public static VideoCompressionQueue? CompressionQueue { get; set; }
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
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

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
        await using (var farmStream = new FileStream(farmFilePath, FileMode.Create))
        {
            await file.CopyToAsync(farmStream);
        }
        var imageFormats = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        if (imageFormats.Contains(ext))
        {
            var newExt = await CompressImageAsync(farmFilePath, ext);
            if (newExt != ext)
            {
                var newFileName = Path.ChangeExtension(fileName, newExt);
                var newPath = Path.Combine(farmDir, newFileName);
                if (File.Exists(newPath)) File.Delete(newPath);
                File.Move(farmFilePath, newPath);
                fileName = newFileName;
            }
        }

        return $"/images/farm/{fileName}";
    }

    /// <summary>
    /// 用 ImageSharp 压缩图片（最大 1920px，JPEG 质量 80，PNG 最佳压缩）
    /// 先写临时文件，比原文件小才替换，避免越压越大
    /// </summary>
    private static async Task<string> CompressImageAsync(string filePath, string extension)
    {
        var tempPath = filePath + ".tmp";
        try
        {
            if (extension == ".gif")
                return extension;

            // 用大括号确保 Image 及时释放，避免后续文件操作冲突
            {
                using var image = await Image.LoadAsync(filePath);

                if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxImageDimension, MaxImageDimension),
                        Mode = ResizeMode.Max
                    }));
                }

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        await image.SaveAsJpegAsync(tempPath, new JpegEncoder { Quality = JpegQuality });
                        break;
                    case ".png":
                        await image.SaveAsPngAsync(tempPath, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
                        break;
                    default:
                        // webp / bmp 等不重新编码，直接返回
                        return extension;
                }
            }

            // 比较大小，压缩后更小才替换
            if (File.Exists(tempPath))
            {
                var originalSize = new FileInfo(filePath).Length;
                var compressedSize = new FileInfo(tempPath).Length;

                if (compressedSize < originalSize)
                {
                    File.Delete(filePath);
                    File.Move(tempPath, filePath);
                }
                else
                {
                    File.Delete(tempPath);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageCompress] 压缩失败 ({filePath}): {ex.Message}");
            // 压缩失败，保留原始文件
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        return extension;
    }

    /// <summary>
    /// CompressImageAsync 的同步版本（给 SaveBase64ToDisk 使用）
    /// </summary>
    private static string CompressImageSync(string filePath, string extension)
    {
        var tempPath = filePath + ".tmp";
        try
        {
            if (extension == ".gif")
                return extension;

            // 用大括号确保 Image 及时释放
            {
                using var image = Image.Load(filePath);

                if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxImageDimension, MaxImageDimension),
                        Mode = ResizeMode.Max
                    }));
                }

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        image.SaveAsJpeg(tempPath, new JpegEncoder { Quality = JpegQuality });
                        break;
                    case ".png":
                        image.SaveAsPng(tempPath, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
                        break;
                    default:
                        return extension;
                }
            }

            // 比较大小，压缩后更小才替换
            if (File.Exists(tempPath))
            {
                var originalSize = new FileInfo(filePath).Length;
                var compressedSize = new FileInfo(tempPath).Length;

                if (compressedSize < originalSize)
                {
                    File.Delete(filePath);
                    File.Move(tempPath, filePath);
                }
                else
                {
                    File.Delete(tempPath);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageCompress] 压缩失败 ({filePath}): {ex.Message}");
            // 压缩失败，保留原始文件
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        return extension;
    }

    /// <summary>
    /// 据视频 URL 入队后台压缩（保存到 DB 后调用）
    /// </summary>
    public static void QueueVideoCompression(string? videoUrl, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(videoUrl) || !IsVideoUrl(videoUrl))
            return;

        var fileName = Path.GetFileName(videoUrl);
        var filePath = Path.Combine(webRootPath, "videos", fileName);

        if (!File.Exists(filePath))
        {
            // 也可能在 farm 目录下（历史数据路径）
            filePath = Path.Combine(webRootPath, "farm", fileName);
            if (!File.Exists(filePath))
                return;
        }

        if (CompressionQueue is not null)
            CompressionQueue.EnqueueAsync(filePath).AsTask().Wait();
        else
            CompressVideoAsync(filePath).Wait();
    }

    /// <summary>
    /// 用 FFmpeg 压缩视频（H.264 CRF 28，最大 1080p）
    /// </summary>
    internal static async Task CompressVideoAsync(string filePath)
    {
        try
        {
            var tempPath = filePath + ".tmp.mp4";
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = $"-i \"{filePath}\" -c:v libx264 -crf {VideoCrf} -preset medium -vf \"scale={VideoMaxWidth}:{VideoMaxHeight}:force_original_aspect_ratio=decrease\" -c:a aac -b:a 96k \"{tempPath}\" -y";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var ffmpegLog = await stderrTask;
            Console.WriteLine($"[VideoCompress] exit={process.ExitCode}, size={new FileInfo(filePath).Length}, log={ffmpegLog[..Math.Min(2000, ffmpegLog.Length)]}");

            if (process.ExitCode == 0 && File.Exists(tempPath))
            {
                var originalSize = new FileInfo(filePath).Length;
                var compressedSize = new FileInfo(tempPath).Length;
                // 仅当压缩后确实更小时替换
                if (compressedSize < originalSize)
                {
                    await SafeReplaceAsync(filePath, tempPath);
                }
                else
                {
                    File.Delete(tempPath);
                }
            }
            else if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VideoCompress] Error: {ex.Message}");
            // 压缩失败，保留原始文件
            var tempFile = filePath + ".tmp.mp4";
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 安全替换文件：用压缩文件内容覆盖原文件，带重试
    /// </summary>
    private static async Task SafeReplaceAsync(string originalPath, string tempPath)
    {
        var originalSize = new FileInfo(originalPath).Length;
        var compressedSize = new FileInfo(tempPath).Length;

        for (var i = 0; i < 10; i++)
        {
            try
            {
                File.Copy(tempPath, originalPath, overwrite: true);
                if (File.Exists(tempPath))
                    try { File.Delete(tempPath); } catch { }
                var savedPct = (1 - (double)new FileInfo(originalPath).Length / originalSize) * 100;
                Console.WriteLine($"[VideoCompress] Replaced OK, saved ~{savedPct:F1}%");
                return;
            }
            catch when (i < 9) { await Task.Delay(200); }
        }
        Console.WriteLine($"[VideoCompress] Replace failed after 10 retries, keeping original");
        if (File.Exists(tempPath))
            try { File.Delete(tempPath); } catch { }
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

        // 写盘后压缩
        var imageFormats = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        if (imageFormats.Contains(extension))
        {
            var newExt = CompressImageSync(filePath, extension);
            if (newExt != extension)
            {
                var newFileName = Path.ChangeExtension(fileName, newExt);
                var newPath = Path.Combine(farmDir, newFileName);
                if (File.Exists(newPath)) File.Delete(newPath);
                File.Move(filePath, newPath);
                fileName = newFileName;
            }
        }

        return $"/images/farm/{fileName}";
    }
}
