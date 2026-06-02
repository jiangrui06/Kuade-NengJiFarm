using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;

namespace WebAPI.Middleware;

public class DisabledUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DisabledUserMiddleware> _logger;

    public DisabledUserMiddleware(RequestDelegate next, ILogger<DisabledUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? context.User.FindFirstValue("userId");
            if (int.TryParse(userIdClaim, out var userId))
            {
                var isDisabled = await dbContext.SysConfigs
                    .AnyAsync(c => c.ConfigKey == "disabled_user_" + userId);
                if (isDisabled)
                {
                    _logger.LogWarning($"被禁用用户拒绝访问 | 用户ID: {userId}");
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { code = 403, message = "账号已禁用，请联系管理员" });
                    return;
                }
            }
        }

        await _next(context);
    }
}
