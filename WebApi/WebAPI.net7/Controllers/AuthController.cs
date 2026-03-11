using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Models.Entities;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// 认证控制器，提供一键登录、微信登录、手机号登录等接口
        /// </summary>
        public AuthController(
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// 一键登录：/api/auth/login
        /// 根据 deviceId 作为设备标识创建或查找游客用户，并返回 token 和基础用户信息。
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<object>>> Login([FromBody] LoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.DeviceId))
            {
                return ApiResponse<object>.Fail("deviceId 必填", 400);
            }

            try
            {
                // 使用 deviceId 作为 user_no，便于同一设备多次登录找到同一条用户记录
                var userNo = req.DeviceId.Trim();
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserNo == userNo);

                if (user == null)
                {
                    user = await CreateGuestUser(userNo);
                    if (user == null)
                    {
                        return ApiResponse<object>.Fail("创建用户失败", 500);
                    }
                }

                var token = GenerateJwtToken(user);

                var data = new
                {
                    token,
                    userInfo = new
                    {
                        id = user.UserId,
                        nickname = user.WxNickname ?? user.UserNo,
                        avatar = user.WxImage ?? string.Empty
                    }
                };

                return ApiResponse<object>.Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "一键登录失败");
                return ApiResponse<object>.Fail("登录失败，请稍后重试", 500);
            }
        }

        /// <summary>
        /// 微信登录：/api/auth/wechat
        /// 使用 wx.login 返回的 code 调用微信接口换取 openid，并获取用户信息
        /// </summary>
        [HttpPost("wechat")]
        public async Task<ActionResult<ApiResponse<object>>> Wechat([FromBody] WechatLoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Code))
            {
                return ApiResponse<object>.Fail("code 必填", 400);
            }

            var appId = _configuration["WeChat:AppId"];
            var secret = _configuration["WeChat:Secret"];

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("微信配置缺失：AppId 或 Secret 未配置");
                return ApiResponse<object>.Fail("微信登录功能未配置", 500);
            }

            try
            {
                // 1. 获取 session_key 和 openid
                var session = await GetWeChatSessionAsync(appId, secret, req.Code);

                if (session == null)
                {
                    return ApiResponse<object>.Fail("微信服务器响应无效", 502);
                }

                if (session.Errcode != 0)
                {
                    _logger.LogWarning($"微信登录失败：{session.Errmsg}");
                    return ApiResponse<object>.Fail($"微信登录失败：{session.Errmsg}", 1000 + (session.Errcode ?? 0));
                }

                if (string.IsNullOrWhiteSpace(session.Openid))
                {
                    return ApiResponse<object>.Fail("微信返回的openid为空", 502);
                }

                // 2. 解密用户信息（如果前端传了加密数据）
                string wxNickname = "微信用户";
                string wxAvatar = string.Empty;

                if (!string.IsNullOrWhiteSpace(req.EncryptedData) &&
                    !string.IsNullOrWhiteSpace(req.Iv) &&
                    !string.IsNullOrWhiteSpace(session.Session_key))
                {
                    try
                    {
                        var userInfo = DecryptWeChatUserInfo(
                            session.Session_key,
                            req.EncryptedData,
                            req.Iv);

                        if (userInfo != null)
                        {
                            wxNickname = userInfo.NickName ?? wxNickname;
                            wxAvatar = userInfo.AvatarUrl ?? string.Empty;

                            _logger.LogInformation($"成功解密用户信息：{wxNickname}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // 解密失败，但登录流程不应中断
                        _logger.LogWarning(ex, "微信用户信息解密失败");
                    }
                }

                // 3. 查找或创建用户
                var user = await _db.Users.FirstOrDefaultAsync(u => u.WxOpenId == session.Openid);

                if (user == null)
                {
                    user = await CreateWeChatUser(session.Openid, wxNickname, wxAvatar);
                    if (user == null)
                    {
                        return ApiResponse<object>.Fail("创建微信用户失败", 500);
                    }

                    _logger.LogInformation($"创建新微信用户：{session.Openid}");
                }
                else
                {
                    // 4. 更新已有用户的微信信息（昵称、头像可能变了）
                    bool needUpdate = false;

                    if (!string.IsNullOrWhiteSpace(wxNickname) && user.WxNickname != wxNickname)
                    {
                        user.WxNickname = wxNickname;
                        needUpdate = true;
                    }

                    if (!string.IsNullOrWhiteSpace(wxAvatar) && user.WxImage != wxAvatar)
                    {
                        user.WxImage = wxAvatar;
                        needUpdate = true;
                    }

                    if (user.RegisterTime == null)
                    {
                        user.RegisterTime = DateTime.UtcNow;
                        needUpdate = true;
                    }

                    if (needUpdate)
                    {
                        await _db.SaveChangesAsync();
                        _logger.LogInformation($"更新微信用户信息：{session.Openid}");
                    }
                }

                // 5. 生成 JWT Token
                var token = GenerateJwtToken(user);

                var data = new
                {
                    token,
                    userInfo = new
                    {
                        id = user.UserId,
                        nickname = user.WxNickname ?? "微信用户",
                        avatar = user.WxImage ?? string.Empty,
                        openId = user.WxOpenId
                    }
                };

                return ApiResponse<object>.Ok(data);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "调用微信接口失败");
                return ApiResponse<object>.Fail("微信服务暂时不可用", 502);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "微信登录异常");
                return ApiResponse<object>.Fail("登录失败，请稍后重试", 500);
            }
        }

        /// <summary>
        /// 手机号登录：/api/auth/phone
        /// 根据手机号查找或创建用户
        /// </summary>
        [HttpPost("phone")]
        public async Task<ActionResult<ApiResponse<object>>> Phone([FromBody] PhoneLoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Phone))
            {
                return ApiResponse<object>.Fail("phone 必填", 400);
            }

            if (string.IsNullOrWhiteSpace(req.Code))
            {
                return ApiResponse<object>.Fail("验证码必填", 1001);
            }

            try
            {
                // TODO: 在这里添加真实的短信验证码校验逻辑
                // 示例环境暂时不做真实验证码校验

                var phone = req.Phone.Trim();
                var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);

                if (user == null)
                {
                    user = await CreatePhoneUser(phone);
                    if (user == null)
                    {
                        return ApiResponse<object>.Fail("创建用户失败", 500);
                    }
                }

                var token = GenerateJwtToken(user);

                var data = new
                {
                    token,
                    userInfo = new
                    {
                        id = user.UserId,
                        nickname = user.WxNickname ?? "手机用户",
                        avatar = user.WxImage ?? string.Empty,
                        phone = user.PhoneNumber
                    }
                };

                return ApiResponse<object>.Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "手机号登录失败");
                return ApiResponse<object>.Fail("登录失败，请稍后重试", 500);
            }
        }

        /// <summary>
        /// 发送验证码：/api/auth/send-code
        /// </summary>
        [HttpPost("send-code")]
        public async Task<ActionResult<ApiResponse<object>>> SendCode([FromBody] SendCodeRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Phone))
            {
                return ApiResponse<object>.Fail("phone 必填", 400);
            }

            try
            {
                // TODO: 集成真实的短信服务发送验证码
                // 这里只是示例，返回固定验证码 123456

                _logger.LogInformation($"向手机 {req.Phone} 发送验证码：123456");

                // 在实际项目中，这里应该：
                // 1. 生成6位随机验证码
                // 2. 将验证码存储到Redis或数据库，设置5分钟过期
                // 3. 调用短信服务商API发送短信

                return ApiResponse<object>.Ok(new { message = "验证码发送成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送验证码失败");
                return ApiResponse<object>.Fail("发送失败，请稍后重试", 500);
            }
        }

        /// <summary>
        /// 登出接口
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public ActionResult<ApiResponse<object>> Logout()
        {
            // JWT是无状态的，服务器不需要存储session
            // 客户端删除本地token即可
            return ApiResponse<object>.Ok(null);
        }

        /// <summary>
        /// 检查登录状态
        /// </summary>
        [HttpGet("check")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> Check()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return ApiResponse<object>.Fail("未登录", 401);
                }

                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                {
                    return ApiResponse<object>.Fail("用户不存在", 401);
                }

                var data = new
                {
                    isLoggedIn = true,
                    userInfo = new
                    {
                        id = user.UserId,
                        nickname = user.WxNickname ?? user.UserNo,
                        avatar = user.WxImage ?? string.Empty,
                        phone = user.PhoneNumber
                    }
                };

                return ApiResponse<object>.Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查登录状态失败");
                return ApiResponse<object>.Fail("检查失败", 500);
            }
        }

        /// <summary>
        /// 刷新token
        /// </summary>
        [HttpPost("refresh")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> RefreshToken()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return ApiResponse<object>.Fail("无效的token", 401);
                }

                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                {
                    return ApiResponse<object>.Fail("用户不存在", 401);
                }

                var newToken = GenerateJwtToken(user);

                return ApiResponse<object>.Ok(new { token = newToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新token失败");
                return ApiResponse<object>.Fail("刷新失败", 500);
            }
        }

        #region 私有辅助方法

        /// <summary>
        /// 调用微信 jscode2session 接口
        /// </summary>
        private async Task<WeChatSessionResponse> GetWeChatSessionAsync(string appId, string secret, string code)
        {
            var url = $"https://api.weixin.qq.com/sns/jscode2session?" +
                      $"appid={Uri.EscapeDataString(appId)}" +
                      $"&secret={Uri.EscapeDataString(secret)}" +
                      $"&js_code={Uri.EscapeDataString(code)}" +
                      $"&grant_type=authorization_code";

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.Add("User-Agent", "ASP.NET Core WeChat Client");

            var response = await http.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogDebug($"微信接口返回：{content}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<WeChatSessionResponse>(content, options);
        }

        /// <summary>
        /// 解密微信用户信息
        /// </summary>
        private WeChatUserInfo DecryptWeChatUserInfo(string sessionKey, string encryptedData, string iv)
        {
            try
            {
                var aesKey = Convert.FromBase64String(sessionKey);
                var aesIV = Convert.FromBase64String(iv);
                var encryptedBytes = Convert.FromBase64String(encryptedData);

                using var aes = Aes.Create();
                aes.Key = aesKey;
                aes.IV = aesIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(encryptedBytes);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs, Encoding.UTF8);

                var json = sr.ReadToEnd();
                _logger.LogDebug($"解密后的用户信息：{json}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<WeChatUserInfo>(json, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解密微信用户信息失败");
                throw;
            }
        }

        /// <summary>
        /// 创建游客用户
        /// </summary>
        private async Task<User> CreateGuestUser(string deviceId)
        {
            var defaultRoleId = await GetDefaultRoleId();

            var user = new User
            {
                UserNo = deviceId,
                PhoneNumber = null,
                RegisterTime = DateTime.UtcNow,
                WxOpenId = null,
                WxImage = null,
                WxNickname = "游客",
                RoleId = defaultRoleId
            };

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();

            return user;
        }

        /// <summary>
        /// 创建微信用户
        /// </summary>
        private async Task<User> CreateWeChatUser(string openId, string nickname, string avatar)
        {
            var defaultRoleId = await GetDefaultRoleId();

            // 生成唯一的user_no
            var userNo = $"wx_{openId}";
            if (userNo.Length > 50) // 假设user_no字段长度为50
            {
                userNo = userNo.Substring(0, 50);
            }

            var user = new User
            {
                UserNo = userNo,
                PhoneNumber = null,
                RegisterTime = DateTime.UtcNow,
                WxOpenId = openId,
                WxImage = avatar,
                WxNickname = nickname,
                RoleId = defaultRoleId
            };

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();

            return user;
        }

        /// <summary>
        /// 创建手机号用户
        /// </summary>
        private async Task<User> CreatePhoneUser(string phone)
        {
            var defaultRoleId = await GetDefaultRoleId();

            var user = new User
            {
                UserNo = $"phone_{phone}",
                PhoneNumber = phone,
                RegisterTime = DateTime.UtcNow,
                WxOpenId = null,
                WxImage = null,
                WxNickname = "手机用户",
                RoleId = defaultRoleId
            };

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();

            return user;
        }

        /// <summary>
        /// 获取默认角色ID
        /// </summary>
        private async Task<int> GetDefaultRoleId()
        {
            var roleId = await _db.Roles
                .Where(r => r.RoleName == "普通用户") // 假设有普通用户角色
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();

            if (roleId == 0)
            {
                roleId = await _db.Roles
                    .OrderBy(r => r.RoleId)
                    .Select(r => r.RoleId)
                    .FirstOrDefaultAsync();
            }

            return roleId == 0 ? 1 : roleId;
        }

        /// <summary>
        /// 生成 JWT Token
        /// </summary>
        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"] ??
                "your-default-secret-key-at-least-16-chars-long");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.WxNickname ?? user.UserNo),
                    new Claim("user_no", user.UserNo),
                    new Claim("openid", user.WxOpenId ?? string.Empty),
                    new Claim("phone", user.PhoneNumber ?? string.Empty)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        #endregion

        #region 请求/响应模型类

        private class WeChatSessionResponse
        {
            [JsonPropertyName("openid")]
            public string Openid { get; set; } = string.Empty;

            [JsonPropertyName("session_key")]
            public string Session_key { get; set; } = string.Empty;

            [JsonPropertyName("unionid")]
            public string? Unionid { get; set; }

            [JsonPropertyName("errcode")]
            public int? Errcode { get; set; }

            [JsonPropertyName("errmsg")]
            public string? Errmsg { get; set; }
        }

        private class WeChatUserInfo
        {
            [JsonPropertyName("openId")]
            public string OpenId { get; set; } = string.Empty;

            [JsonPropertyName("nickName")]
            public string NickName { get; set; } = string.Empty;

            [JsonPropertyName("avatarUrl")]
            public string AvatarUrl { get; set; } = string.Empty;

            [JsonPropertyName("gender")]
            public int Gender { get; set; }

            [JsonPropertyName("country")]
            public string Country { get; set; } = string.Empty;

            [JsonPropertyName("province")]
            public string Province { get; set; } = string.Empty;

            [JsonPropertyName("city")]
            public string City { get; set; } = string.Empty;

            [JsonPropertyName("language")]
            public string Language { get; set; } = string.Empty;

            [JsonPropertyName("watermark")]
            public Watermark Watermark { get; set; } = new Watermark();
        }

        private class Watermark
        {
            [JsonPropertyName("appid")]
            public string AppId { get; set; } = string.Empty;

            [JsonPropertyName("timestamp")]
            public long Timestamp { get; set; }
        }

        #endregion
    }

    #region 公开的请求模型类

    /// <summary>
    /// 一键登录请求
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 设备唯一标识
        /// </summary>
        [Required]
        public string DeviceId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 微信登录请求
    /// </summary>
    public class WechatLoginRequest
    {
        /// <summary>
        /// wx.login 获取的临时 code
        /// </summary>
        [Required]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 微信用户信息的加密数据
        /// </summary>
        public string? EncryptedData { get; set; }

        /// <summary>
        /// 加密算法的初始向量
        /// </summary>
        public string? Iv { get; set; }

        /// <summary>
        /// 用户非敏感信息
        /// </summary>
        public string? RawData { get; set; }

        /// <summary>
        /// 签名
        /// </summary>
        public string? Signature { get; set; }
    }

    /// <summary>
    /// 手机号登录请求
    /// </summary>
    public class PhoneLoginRequest
    {
        /// <summary>
        /// 手机号
        /// </summary>
        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// 短信验证码
        /// </summary>
        [Required]
        [StringLength(6, MinimumLength = 4)]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// 发送验证码请求
    /// </summary>
    public class SendCodeRequest
    {
        /// <summary>
        /// 手机号
        /// </summary>
        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;
    }

    #endregion
}