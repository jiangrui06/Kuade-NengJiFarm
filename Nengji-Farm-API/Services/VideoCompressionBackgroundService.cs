using System.Diagnostics;
using WebAPI.Common;

namespace WebAPI.Services;

public class VideoCompressionBackgroundService : BackgroundService
{
    private readonly VideoCompressionQueue _queue;
    private readonly ILogger<VideoCompressionBackgroundService> _logger;

    public VideoCompressionBackgroundService(VideoCompressionQueue queue, ILogger<VideoCompressionBackgroundService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[VideoBg] 后台压缩服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var filePath = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation("[VideoBg] 开始压缩: {Path}", filePath);

                var sw = Stopwatch.StartNew();
                await MediaHelper.CompressVideoAsync(filePath);
                sw.Stop();

                _logger.LogInformation("[VideoBg] 压缩完成: {Path} 耗时={Elapsed}ms", filePath, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VideoBg] 压缩异常");
            }
        }
    }
}
