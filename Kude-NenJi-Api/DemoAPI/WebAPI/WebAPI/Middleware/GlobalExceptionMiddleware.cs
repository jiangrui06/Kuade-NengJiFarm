using System.Text.Json;

using WebAPI.Common;

namespace WebAPI.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

            var payload = JsonSerializer.Serialize(ApiResult.Fail("服务器异常，请稍后重试"));
            await context.Response.WriteAsync(payload);
        }
    }
}
