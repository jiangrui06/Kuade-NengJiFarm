using Microsoft.AspNetCore.Http;
using WebAdminApi.Services;

namespace WebAdminApi.Middleware
{
    public class TokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenMiddleware> _logger;

        public TokenMiddleware(RequestDelegate next, ILogger<TokenMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
        {
            var path = context.Request.Path.Value;

            // 需要token验证的管理员接口路径
            var protectedPaths = new[]
            {
                "/api/user/list",
                "/api/user/add",
                "/api/user/edit",
                "/api/user/delete",
                "/api/user/changeStatus"
            };

            if (protectedPaths.Any(p => path?.StartsWith(p) == true))
            {
                var token = context.Request.Headers["token"].FirstOrDefault();

                if (string.IsNullOrEmpty(token))
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 401, message = "登录已过期，请重新登录", data = (object?)null };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                // 验证token有效性
                if (!tokenService.ValidateToken(token))
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 401, message = "登录已过期，请重新登录", data = (object?)null };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                // 验证管理员权限
                var userRole = tokenService.GetUserRoleFromToken(token);
                if (userRole != "管理员")
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 403, message = "权限不足，仅管理员可操作", data = (object?)null };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                // 将用户信息存储到HttpContext中供控制器使用
                context.Items["UserRole"] = userRole;
                context.Items["UserId"] = tokenService.GetUserIdFromToken(token);

                _logger.LogInformation($"Token验证通过，用户角色: {userRole}");
            }

            await _next(context);
        }
    }
}