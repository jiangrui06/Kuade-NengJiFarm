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

            // 需要 Token 验证的受保护路径
            var protectedPaths = new[]
            {
                "/api/back-user/list",
                "/api/back-user/add",
                "/api/back-user/edit",
                "/api/back-user/delete",
                "/api/back-user/changeStatus"
            };

            if (protectedPaths.Any(p => path?.StartsWith(p) == true))
            {
                _logger.LogInformation($"?? 正在验证受保护路径: {path}");

                // 从 Header 获取 Token
                var token = context.Request.Headers["token"].FirstOrDefault()
                    ?? context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("? 未找到 Token，请求被拒绝");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 401, message = "登录已过期，请重新登录" };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                _logger.LogInformation($"?? 接收到 Token (前30字符): {token.Substring(0, Math.Min(30, token.Length))}...");

                // 验证 JWT 签名
                if (!tokenService.ValidateToken(token))
                {
                    _logger.LogWarning("? Token 验证失败（签名或过期），请求被拒绝");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 401, message = "登录已过期或 Token 无效" };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                _logger.LogInformation("? Token 签名验证成功");

                // 从 Token 中提取用户信息
                var userRole = tokenService.GetUserRoleFromToken(token);
                var userId = tokenService.GetUserIdFromToken(token);

                _logger.LogInformation($"?? 提取用户信息 | UserId: {userId} | Role: {userRole}");

                // ? 添加详细的权限检查日志
                if (string.IsNullOrEmpty(userRole))
                {
                    _logger.LogError("? 用户角色为空，拒绝访问");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 401, message = "用户角色信息无效，请重新登录" };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                if (userRole != "管理员")
                {
                    _logger.LogWarning($"? 权限不足 | 用户角色: {userRole} | 需要: 管理员");
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 403, message = $"权限不足，用户角色: {userRole}，仅管理员可操作" };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                _logger.LogInformation($"? 权限验证通过 | UserId: {userId} | Role: {userRole}");

                // 将用户信息存储到 HttpContext
                context.Items["UserRole"] = userRole;
                context.Items["UserId"] = userId;
            }

            await _next(context);
        }
    }
}