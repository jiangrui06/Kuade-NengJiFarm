using System.Text.Json;

using ManageAPI.Common;

namespace ManageAPI.Middleware;

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
        catch (OperationCanceledException)
        {
            if (!context.Response.HasStarted)
            {           
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred while processing {Path}", context.Request.Path);

           

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(ApiResult.Fail($"服务器异常：{ex.Message}", 500));
            await context.Response.WriteAsync(payload);
        }
    }
}
