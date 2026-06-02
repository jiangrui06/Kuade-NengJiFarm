using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebAPI.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireTokenTypeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _requiredTokenType;

    public RequireTokenTypeAttribute(string tokenType)
    {
        _requiredTokenType = tokenType;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            return;

        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                code = 401,
                message = "token已过期，请重新登录"
            });
            return;
        }

        var tokenTypeClaim = user.FindFirst("token_type")?.Value;
        // 没 token_type 声明的旧 token 向后兼容
        if (tokenTypeClaim != null && tokenTypeClaim != _requiredTokenType)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                code = 401,
                message = $"Token类型不匹配，无法访问此系统"
            });
        }
    }
}
