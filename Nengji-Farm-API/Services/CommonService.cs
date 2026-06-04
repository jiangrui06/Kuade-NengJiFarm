using WebAPI.Common;

namespace WebAPI.Services;

public class CommonService : ICommonService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".mov", ".avi"];
    private readonly IWebHostEnvironment _env;

    public CommonService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new BusinessException("请选择要上传的文件", 400);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new BusinessException("不支持的文件格式", 400);

        var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        return await MediaHelper.SaveFileAsync(file, webRootPath);
    }
}
