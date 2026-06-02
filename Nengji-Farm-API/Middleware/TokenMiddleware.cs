using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using WebAPI.Services;

namespace WebAPI.Middleware
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

            // 仅保护 /api/* 路径
            if (path == null || !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // 图片（含二维码）、视频、上传文件访问跳过 token 校验
            // 浏览器 img/video 标签不带 Authorization header
            if (path.StartsWith("/api/file/image", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/file/video", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/file/uploads", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // 跳过标记了 [AllowAnonymous] 的终结点（登录、支付回调等）
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await _next(context);
                return;
            }

            // 如果已被内置 JWT Bearer 认证（小程序 Authorization: Bearer），跳过
            if (context.User.Identity?.IsAuthenticated == true)
            {
                await _next(context);
                return;
            }

            _logger.LogInformation($"进入需要验证Token的受保护路径: {path}");

            var token = context.Request.Headers["token"].FirstOrDefault()
                ?? context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("未找到 Token，请求被拒绝");
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var response = new { code = 401, message = "token已过期，请重新登录" };
                await context.Response.WriteAsJsonAsync(response);
                return;
            }

            _logger.LogInformation($"接收到的 Token (前30字符): {token.Substring(0, Math.Min(30, token.Length))}...");

            if (!tokenService.ValidateToken(token))
            {
                _logger.LogWarning("Token 验证失败：签名或过期，请求被拒绝");
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var response = new { code = 401, message = "token已过期，请重新登录" };
                await context.Response.WriteAsJsonAsync(response);
                return;
            }

            _logger.LogInformation("Token 签名验证成功");

            // 设置 HttpContext.User，使 [RequireTokenType] 等授权过滤器可以正常工作
            var principal = tokenService.GetPrincipalFromToken(token);
            if (principal != null)
            {
                context.User = principal;
            }

            var userId = tokenService.GetUserIdFromToken(token);
            context.Items["UserId"] = userId;

            await _next(context);
        }
    }
}
