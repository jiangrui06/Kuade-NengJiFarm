using System.Text.Json;
using WebAPI.Common;

namespace WebAPI.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 对于Swagger相关的请求，直接传递给下一个中间件，不捕获异常
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business exception occurred while processing {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(ApiResult.Fail(ex.Message, ex.Code));
            await context.Response.WriteAsync(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred while processing {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";

            var message = _env.IsDevelopment()
                ? $"服务器异常: {ex.GetType().Name}: {ex.Message}"
                : "服务器异常，请稍后重试";
            var payload = JsonSerializer.Serialize(ApiResult.Fail(message));
            await context.Response.WriteAsync(payload);
        }
    }
}
