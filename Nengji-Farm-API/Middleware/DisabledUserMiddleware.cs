using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Entities;

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
                var disabledRoleId = await dbContext.Roles
                    .Where(r => r.RoleName == "已禁用")
                    .Select(r => (int?)r.RoleId)
                    .FirstOrDefaultAsync() ?? 3;

                var userRoleId = await dbContext.Users
                    .Where(u => u.UserId == userId)
                    .Select(u => (int?)u.RoleId)
                    .FirstOrDefaultAsync();

                if (userRoleId.HasValue && userRoleId.Value == disabledRoleId)
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
