using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    private const string AuthApiVersion = "nenji-auth-20260430-phone-login-diagnostics";
    private static readonly TimeSpan WeChatRequestTimeout = TimeSpan.FromSeconds(3);

    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly JwtHelper _jwtHelper;

    public AuthController(
        IAuthService authService,
        AppDbContext dbContext,
        IConfiguration configuration,
        JwtHelper jwtHelper,
        IHttpClientFactory httpClientFactory)
    {
        _authService = authService;
        _dbContext = dbContext;
        _configuration = configuration;
        _jwtHelper = jwtHelper;

        _config = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// 登录接口诊断，用于确认当前运行的是 NenJi-API 的最新代码。
    /// </summary>
    [HttpGet("diagnostics")]
    [AllowAnonymous]
    public ActionResult<ApiResult> Diagnostics()
    {
        Response.Headers["X-NenJi-Auth-Version"] = AuthApiVersion;

        return Ok(ApiResult.Success(new
        {
            service = "NenJi-API",
            controller = "AuthController",
            version = AuthApiVersion,
            now = DateTimeOffset.Now,
            wechatAppIdConfigured = !string.IsNullOrWhiteSpace(_configuration["WeChat:AppId"]),
            wechatAppSecretConfigured = !string.IsNullOrWhiteSpace(_configuration["WeChat:AppSecret"])
        }));
    }

    /// <summary>
    /// 微信登录
    /// 前端只需传 code
    /// </summary>
    [HttpPost("wxlogin")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult>> WxLogin(
        [FromBody] WechatLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return Ok(ApiResult.Fail("code is required"));
            }

            var appId = _configuration["WeChat:AppId"];
            var appSecret = _configuration["WeChat:AppSecret"];

            Console.WriteLine("========== WxLogin Start ==========");
            Console.WriteLine($"[WxLogin] Code: {request.Code}");
            Console.WriteLine($"[WxLogin] AppId Exists: {!string.IsNullOrWhiteSpace(appId)}");
            Console.WriteLine($"[WxLogin] AppSecret Exists: {!string.IsNullOrWhiteSpace(appSecret)}");

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            {
                Console.WriteLine("[WxLogin] WeChat config missing");
                return Ok(ApiResult.Fail("wechat config missing"));
            }

            var wxSession = await GetWechatSessionAsync(appId, appSecret, request.Code, cancellationToken);

            Console.WriteLine($"[WxLogin] wxSession null: {wxSession is null}");
            Console.WriteLine($"[WxLogin] OpenId: {wxSession?.OpenId}");
            Console.WriteLine($"[WxLogin] ErrCode: {wxSession?.ErrCode}");
            Console.WriteLine($"[WxLogin] ErrMsg: {wxSession?.ErrMsg}");

            if (wxSession is null)
            {
                return Ok(ApiResult.Fail("wechat login failed"));
            }

            if (wxSession.ErrCode.HasValue && wxSession.ErrCode.Value != 0)
            {
                return Ok(ApiResult.Fail(wxSession.ErrMsg ?? "wechat login failed", wxSession.ErrCode.Value));
            }

            if (string.IsNullOrWhiteSpace(wxSession.OpenId))
            {
                return Ok(ApiResult.Fail("openid is empty"));
            }

            var openId = wxSession.OpenId.Trim();

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.WxOpenId == openId, cancellationToken);

            var isNewUser = false;

            if (user is null)
            {
                isNewUser = true;
                user = await CreateWechatUserAsync(openId, cancellationToken);
                _dbContext.Users.Add(user);
            }
            else
            {
                var isDisabled = await _dbContext.SysConfigs
                    .AnyAsync(c => c.ConfigKey == "disabled_user_" + user.UserId, cancellationToken);
                if (isDisabled)
                {
                    return Ok(ApiResult.Fail("账号已禁用，请联系管理员", 403));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Avatar))
            {
                user.WxImage = request.Avatar.Trim();
            }

            // 如果昵称还是空的，设置默认昵称
            if (string.IsNullOrWhiteSpace(user.WxName))
            {
                user.WxName = "微信用户";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var role = await ResolveUserRoleAsync(user.RoleId, cancellationToken);
            user.Role = new Role { RoleId = user.RoleId, RoleName = role };
            var token = _jwtHelper.GenerateToken(user);

            Console.WriteLine($"[WxLogin] token generated, UserId: {user.UserId}");
            Console.WriteLine("========== WxLogin End ==========");

            return Ok(ApiResult.Success(new
            {
                token,
                isNewUser,
                user_id = user.UserId,
                user_guid = user.UserNo,
                register_time = user.RegisterTime,
                openid = user.WxOpenId,
                phone_number = user.PhoneNumber,
                role
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== WxLogin ERROR ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("===================================");

            return Ok(ApiResult.Fail("服务器内部异常，请稍后再试"));
        }
    }

    /// <summary>
    /// 微信手机号快捷登录
    /// 前端需传 wx.login 的 code + getPhoneNumber 的 phoneCode
    /// </summary>
    [HttpPost("wx-phone-login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult>> WxPhoneLogin(
        [FromBody] WxPhoneLoginRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers["X-NenJi-Auth-Version"] = AuthApiVersion;

        var phase = "validate-request";
        try
        {
            Console.WriteLine("========== WxPhoneLogin Start ==========");
            Console.WriteLine($"Version: {AuthApiVersion}");
            Console.WriteLine($"CodeLength: {request?.Code?.Length ?? 0}");
            Console.WriteLine($"PhoneCodeLength: {request?.PhoneCode?.Length ?? 0}");

            if (request == null || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.PhoneCode))
            {
                return Ok(ApiResult.Fail("code and phoneCode are required"));
            }

            var appId = _configuration["WeChat:AppId"];
            var appSecret = _configuration["WeChat:AppSecret"];
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            {
                return Ok(ApiResult.Fail("wechat config missing"));
            }

            phase = "wechat-jscode2session";
            var wxSession = await GetWechatSessionAsync(appId, appSecret, request.Code, cancellationToken);
            if (wxSession is null)
            {
                return Ok(ApiResult.Fail("wechat login failed"));
            }

            if (wxSession.ErrCode.HasValue && wxSession.ErrCode.Value != 0)
            {
                return Ok(ApiResult.Fail(wxSession.ErrMsg ?? "wechat login failed", wxSession.ErrCode.Value));
            }

            if (string.IsNullOrWhiteSpace(wxSession.OpenId))
            {
                return Ok(ApiResult.Fail("openid is empty"));
            }

            phase = "wechat-get-phone-number";
            var (purePhoneNumber, phoneError) = await InternalGetPhoneNumberWithErrorAsync(request.PhoneCode, cancellationToken);
            if (string.IsNullOrWhiteSpace(purePhoneNumber))
            {
                return Ok(ApiResult.Fail(string.IsNullOrWhiteSpace(phoneError) ? "获取手机号失败" : phoneError));
            }

            var openId = wxSession.OpenId.Trim();
            var isNewUser = false;

            phase = "query-user-by-openid";
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.WxOpenId == openId, cancellationToken);

            if (user is null)
            {
                phase = "query-user-by-phone";
                user = await _dbContext.Users
                    .FirstOrDefaultAsync(x => x.PhoneNumber == purePhoneNumber, cancellationToken);

                if (user is not null)
                {
                    if (!string.IsNullOrWhiteSpace(user.WxOpenId) &&
                        !string.Equals(user.WxOpenId.Trim(), openId, StringComparison.Ordinal))
                    {
                        return Ok(ApiResult.Fail("该手机号已绑定其他微信账号", 409));
                    }

                    user.WxOpenId = openId;
                }
                else
                {
                    isNewUser = true;
                    phase = "create-wechat-user";
                    user = await CreateWechatUserAsync(openId, cancellationToken);
                    _dbContext.Users.Add(user);
                }
            }

            if (!isNewUser)
            {
                var isDisabled = await _dbContext.SysConfigs
                    .AnyAsync(c => c.ConfigKey == "disabled_user_" + user.UserId, cancellationToken);
                if (isDisabled)
                {
                    return Ok(ApiResult.Fail("账号已禁用，请联系管理员", 403));
                }
            }

            user.PhoneNumber = purePhoneNumber;

            if (!string.IsNullOrWhiteSpace(request.Avatar))
            {
                user.WxImage = request.Avatar.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Nickname))
            {
                user.WxName = request.Nickname.Trim();
            }

            if (string.IsNullOrWhiteSpace(user.WxName))
            {
                user.WxName = "微信用户";
            }

            phase = "save-user";
            await _dbContext.SaveChangesAsync(cancellationToken);

            phase = "resolve-role";
            var role = await ResolveUserRoleAsync(user.RoleId, cancellationToken);
            user.Role = new Role { RoleId = user.RoleId, RoleName = role };

            phase = "generate-token";
            var token = _jwtHelper.GenerateToken(user);

            Console.WriteLine($"[WxPhoneLogin] success, UserId: {user.UserId}, Role: {role}");
            Console.WriteLine("========== WxPhoneLogin End ==========");

            return Ok(ApiResult.Success(new
            {
                token,
                isNewUser,
                user_id = user.UserId,
                user_guid = user.UserNo,
                register_time = user.RegisterTime,
                openid = user.WxOpenId,
                phone_number = user.PhoneNumber,
                purePhoneNumber = purePhoneNumber,
                role
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== WxPhoneLogin ERROR ==========");
            Console.WriteLine($"Phase: {phase}");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("========================================");
            return Ok(ApiResult.Fail($"手机登录失败[{phase}]: {GetInnermostExceptionMessage(ex)}"));
        }
    }

    /// <summary>
    /// 检查登录状态
    /// </summary>
    [HttpGet("check")]
    [Authorize]
    public async Task<ActionResult<ApiResult>> Check(CancellationToken cancellationToken)
    {
        try
        {
            var userId = TryGetCurrentUserId();
            if (userId is null)
            {
                return Ok(ApiResult.Fail("login state invalid", 401));
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

            if (user is null)
            {
                return Ok(ApiResult.Fail("user not found", 404));
            }

            return Ok(ApiResult.Success(new
            {
                isLogin = true,
                isLoggedIn = true,
                user_id = user.UserId,
                user_guid = user.UserNo,
                register_time = user.RegisterTime,
                openid = user.WxOpenId,
                phone_number = user.PhoneNumber
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== Check ERROR ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("=================================");

            return Ok(ApiResult.Fail("服务器内部异常，请稍后再试"));
        }
    }

    /// <summary>
    /// 获取当前微信用户手机号
    /// 前端调用 getPhoneNumber 返回的 code
    /// 需要登录状态
    /// </summary>
    [HttpPost("phone")]
    [Authorize]
    public async Task<ActionResult<ApiResult>> GetPhoneNumber(
        [FromBody] AuthPhoneCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return Ok(ApiResult.Fail("code不能为空"));
            }

            var userId = TryGetCurrentUserId();
            if (userId is null)
            {
                return Ok(ApiResult.Fail("登录状态无效", 401));
            }
      
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            var (purePhoneNumber, phoneError) = await InternalGetPhoneNumberWithErrorAsync(request.Code, cancellationToken);
            if (string.IsNullOrWhiteSpace(purePhoneNumber))
            {
                return Ok(ApiResult.Fail(string.IsNullOrWhiteSpace(phoneError) ? "获取手机号失败" : phoneError));
            }

            user.PhoneNumber = purePhoneNumber;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                user_id = user.UserId,
                user_guid = user.UserNo,
                purePhoneNumber
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== GetPhoneNumber ERROR ==========");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("=========================================");

            return Ok(ApiResult.Fail("服务器内部异常，请稍后再试"));
        }
    }

    private int? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("userId");

        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }

    /// <summary>
    /// 调用微信 jscode2session
    /// </summary>
    private async Task<WechatSessionResponse?> GetWechatSessionAsync(
        string appId,
        string appSecret,
        string code,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.weixin.qq.com/sns/jscode2session?appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(appSecret)}&js_code={Uri.EscapeDataString(code)}&grant_type=authorization_code";

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(WeChatRequestTimeout);

        using var response = await _httpClient.GetAsync(url, timeoutCts.Token);
        var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        Console.WriteLine("========== WeChat API Response ==========");
        Console.WriteLine(content);
        Console.WriteLine("=========================================");

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"wechat api error: {response.StatusCode}, content: {content}");
        }

        return JsonSerializer.Deserialize<WechatSessionResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// 自动创建微信用户
    /// </summary>
    private async Task<User> CreateWechatUserAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        var roleId = await _dbContext.Roles
            .Where(x => x.RoleName == "user" || x.RoleName == "普通用户")
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
            RegisterTime = DateTime.Now,
            WxOpenId = openId,
            WxImage = string.Empty,
            WxName = string.Empty,
            RoleId = roleId
        };
    }

    private async Task<string> ResolveUserRoleAsync(int roleId, CancellationToken cancellationToken)
    {
        if (roleId <= 0)
        {
            return "user";
        }

        var roleName = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.RoleName)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return "user";
        }

        return roleName.Trim().Equals("staff", StringComparison.OrdinalIgnoreCase) ||
               roleName.Contains("员工", StringComparison.OrdinalIgnoreCase)
            ? "staff"
            : "user";
    }

    private static string GetInnermostExceptionMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return string.IsNullOrWhiteSpace(current.Message)
            ? ex.GetType().Name
            : current.Message;
    }

    /// <summary>
    /// 微信登录响应结构
    /// </summary>
    private sealed class WechatSessionResponse
    {
        [JsonPropertyName("openid")]
        public string? OpenId { get; set; }

        [JsonPropertyName("session_key")]
        public string? SessionKey { get; set; }

        [JsonPropertyName("unionid")]
        public string? UnionId { get; set; }

        [JsonPropertyName("errcode")]
        public int? ErrCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string? ErrMsg { get; set; }
    }

    /// <summary>
    /// 获取手机号请求参数
    /// </summary>
    public sealed class AuthPhoneCodeRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    private async Task<string?> InternalGetPhoneNumberAsync(string phoneCode, CancellationToken cancellationToken)
    {
        var (phone, _) = await InternalGetPhoneNumberWithErrorAsync(phoneCode, cancellationToken);
        return phone;
    }

    private async Task<(string? PhoneNumber, string? ErrorMessage)> InternalGetPhoneNumberWithErrorAsync(string phoneCode, CancellationToken cancellationToken)
    {
        try
        {
            string appId = _config["WeChat:AppId"] ?? string.Empty;
            string appSecret = _config["WeChat:AppSecret"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            {
                return (null, "wechat config missing");
            }

            string tokenUrl =
                $"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(appSecret)}";

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(WeChatRequestTimeout);

            using var tokenResp = await _httpClient.GetAsync(tokenUrl, timeoutCts.Token);
            var tokenJson = await tokenResp.Content.ReadAsStringAsync(timeoutCts.Token);
            tokenResp.EnsureSuccessStatusCode();

            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
            if (tokenData.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() != 0)
            {
                var errMsg = tokenData.TryGetProperty("errmsg", out var tokenErrMsg) ? tokenErrMsg.GetString() : "get access_token failed";
                return (null, $"微信access_token获取失败: {errMsg}");
            }

            string accessToken = tokenData.GetProperty("access_token").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return (null, "微信access_token为空");
            }

            string phoneUrl =
                $"https://api.weixin.qq.com/wxa/business/getuserphonenumber?access_token={Uri.EscapeDataString(accessToken)}";

            var postData = new { code = phoneCode };
            using var content = new StringContent(
                JsonSerializer.Serialize(postData),
                Encoding.UTF8,
                "application/json");

            using var phoneResp = await _httpClient.PostAsync(phoneUrl, content, timeoutCts.Token);
            var phoneJson = await phoneResp.Content.ReadAsStringAsync(timeoutCts.Token);
            phoneResp.EnsureSuccessStatusCode();

            var phoneData = JsonSerializer.Deserialize<JsonElement>(phoneJson);
            int phoneErrCode = phoneData.TryGetProperty("errcode", out var phoneErrCodeValue)
                ? phoneErrCodeValue.GetInt32()
                : -1;

            if (phoneErrCode != 0)
            {
                var phoneErrMsg = phoneData.TryGetProperty("errmsg", out var phoneErrMsgValue)
                    ? phoneErrMsgValue.GetString()
                    : "getuserphonenumber failed";
                return (null, $"微信手机号获取失败({phoneErrCode}): {phoneErrMsg}");
            }

            if (!phoneData.TryGetProperty("phone_info", out var phoneInfo))
            {
                return (null, "微信返回缺少phone_info");
            }

            var phoneNumber = phoneInfo.TryGetProperty("purePhoneNumber", out var purePhoneNumberValue)
                ? purePhoneNumberValue.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return (null, "微信返回手机号为空");
            }

            return (phoneNumber, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InternalGetPhoneNumberAsync] Error: {ex}");
            return (null, $"服务器异常: {ex.Message}");
        }
    }
}
