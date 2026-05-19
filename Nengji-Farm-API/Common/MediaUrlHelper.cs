namespace WebAPI.Common;

/// <summary>
/// 统一处理图片/视频 URL 归一化。
/// 
/// 数据库中可以存:
///   - 完整 URL:    https://example.com/xxx.jpg
///   - 相对路径:    /api/file/image/xxx.jpg
///   - 旧格式:      /images/farm/Farm_8.jpg
///   - 纯文件名:    Farm_8.jpg
/// 
/// Normalize() 输出统一的相对路径 (如 /api/file/image/xxx.jpg)
/// NormalizeFull() 输出完整的可访问 URL (拼接当前 Request 的域名)
/// </summary>
public static class MediaUrlHelper
{
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];
    private static readonly HashSet<string> VideoExtensions = [".mp4", ".mov", ".avi", ".mkv", ".wmv"];

    /// <summary>
    /// 返回统一相对路径。
    /// - 完整 URL → 原样返回
    /// - /api/file/... → 原样返回
    /// - /images/farm/... → 转为 /api/file/image/...
    /// - 纯文件名 → 转为 /api/file/image/... 或 /api/file/video/...
    /// </summary>
    public static string Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var trimmed = url.Trim().Trim('`', '"', '\'');

        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        // 已经是完整 URL，原样返回
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        // 已是 /api/file/ 开头的相对路径，原样返回
        if (trimmed.StartsWith("/api/file/", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        if (trimmed.StartsWith("api/file/", StringComparison.OrdinalIgnoreCase))
            return "/" + trimmed;

        // 统一的文件名提取：去掉路径前缀，只保留文件名
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(name))
            name = trimmed.TrimStart('/', '\\');

        var ext = Path.GetExtension(name).ToLowerInvariant();

        if (VideoExtensions.Contains(ext))
            return $"/api/file/video/{name}";

        return $"/api/file/image/{name}";
    }

    /// <summary>
    /// 返回完整可访问 URL（基于当前请求的域名）。
    /// 如果已经是完整 URL 则原样返回。
    /// </summary>
    public static string NormalizeFull(string? url, HttpRequest request)
    {
        var normalized = Normalize(url);
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return normalized;

        var baseUrl = $"{request.Scheme}://{request.Host}";
        return $"{baseUrl}{normalized}";
    }
}
