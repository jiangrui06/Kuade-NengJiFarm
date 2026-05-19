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

            var protectedPaths = new[]
            {
                "/api/back-user/list",
                "/api/back-user/add",
                "/api/back-user/edit",
                "/api/back-user/delete"
            };

            if (protectedPaths.Any(p => path?.StartsWith(p) == true))
            {
                _logger.LogInformation($"进入需要验证Token的受保护路径: {path}");

                var token = context.Request.Headers["token"].FirstOrDefault()
                    ?? context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("未找到 Token，请求被拒绝");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 401, message = "登录已过期，请重新登录" };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                _logger.LogInformation($"接收到的 Token (前30字符): {token.Substring(0, Math.Min(30, token.Length))}...");

                if (!tokenService.ValidateToken(token))
                {
                    _logger.LogWarning("Token 验证失败：签名或过期，请求被拒绝");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var response = new { code = 401, message = "登录已过期或 Token 无效" };
                    await context.Response.WriteAsJsonAsync(response);
                    return;
                }

                _logger.LogInformation("Token 签名验证成功");
                var userId = tokenService.GetUserIdFromToken(token);

                context.Items["UserId"] = userId;
            }

            await _next(context);
        }
    }
}
