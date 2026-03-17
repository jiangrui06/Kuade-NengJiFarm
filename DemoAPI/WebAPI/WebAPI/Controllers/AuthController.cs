using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly JwtHelper _jwtHelper;

    public AuthController(
        IAuthService authService,
        AppDbContext dbContext,
        IConfiguration configuration,
        JwtHelper jwtHelper)
    {
        _authService = authService;
        _dbContext = dbContext;
        _configuration = configuration;
        _jwtHelper = jwtHelper;
    }

    [HttpPost("wxlogin")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult>> WxLogin([FromBody] WechatLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Ok(ApiResult.Fail("code is required"));
        }

        var appId = _configuration["WeChat:AppId"];
        var appSecret = _configuration["WeChat:AppSecret"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
        {
            return Ok(ApiResult.Fail("wechat config missing"));
        }

        var wxSession = await GetWechatSessionAsync(appId, appSecret, request.Code, cancellationToken);
        if (wxSession is null || string.IsNullOrWhiteSpace(wxSession.OpenId))
        {
            return Ok(ApiResult.Fail("wechat login failed"));
        }

        if (wxSession.ErrCode.HasValue && wxSession.ErrCode.Value != 0)
        {
            return Ok(ApiResult.Fail(wxSession.ErrMsg ?? "wechat login failed", wxSession.ErrCode.Value));
        }

        var openId = wxSession.OpenId.Trim();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.WxOpenId == openId, cancellationToken);

        if (user is null)
        {
            user = await CreateWechatUserAsync(openId, request, cancellationToken);
            _dbContext.Users.Add(user);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.Nickname))
            {
                user.WxName = TrimToLength(request.Nickname, 45);
            }

            if (!string.IsNullOrWhiteSpace(request.Avatar))
            {
                user.WxImage = TrimToLength(request.Avatar, 255);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var userInfo = new AuthUserDto
        {
            Id = user.UserId,
            UserNo = user.UserNo,
            Nickname = user.WxName,
            Avatar = user.WxImage,
            Phone = user.PhoneNumber
        };

        return Ok(ApiResult.Success(new
        {
            token = _jwtHelper.GenerateToken(user),
            userGuid = user.UserNo,
            openid = openId,
            user = userInfo,
            userInfo
        }));
    }

    [HttpGet("check")]
    [Authorize]
    public async Task<ActionResult<ApiResult>> Check(CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();
        if (userId is null)
        {
            return Ok(ApiResult.Fail("login state invalid", 401));
        }

        var user = await _authService.GetCurrentUserAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Ok(ApiResult.Fail("user not found", 404));
        }

        var openId = await _dbContext.Users
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.WxOpenId)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return Ok(ApiResult.Success(new
        {
            isLogin = true,
            isLoggedIn = true,
            openid = openId,
            user,
            userInfo = user,
            userGuid = user.UserNo
        }));
    }

    private int? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private async Task<WechatSessionResponse?> GetWechatSessionAsync(
        string appId,
        string appSecret,
        string code,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.weixin.qq.com/sns/jscode2session?appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(appSecret)}&js_code={Uri.EscapeDataString(code)}&grant_type=authorization_code";

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"wechat api error: {response.StatusCode}");
        }

        return JsonSerializer.Deserialize<WechatSessionResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private async Task<User> CreateWechatUserAsync(
        string openId,
        WechatLoginRequest request,
        CancellationToken cancellationToken)
    {
        var roleId = await _dbContext.Roles
            .OrderBy(x => x.RoleId)
            .Select(x => x.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId <= 0)
        {
            var role = new Role
            {
                RoleName = "user"
            };

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync(cancellationToken);
            roleId = role.RoleId;
        }

        return new User
        {
            UserNo = Guid.NewGuid().ToString("N"),
            PhoneNumber = string.Empty,
            RegisterTime = DateTime.UtcNow,
            WxOpenId = openId,
            WxImage = TrimToLength(request.Avatar, 255),
            WxName = TrimToLength(request.Nickname, 45),
            RoleId = roleId
        };
    }

    private static string TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed class WechatSessionResponse
    {
        public string? OpenId { get; set; }

        public string? SessionKey { get; set; }

        public string? UnionId { get; set; }

        public int? ErrCode { get; set; }

        public string? ErrMsg { get; set; }
    }
}
