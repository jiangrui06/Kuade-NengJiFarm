using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebAPI.Configuration;

namespace WebAPI.Services
{
    /// <summary>
    /// Token 服务实现（JWT 方案）
    /// 无状态验证，无需查询数据库
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<TokenService> _logger;

        /// <summary>
        /// Token 黑名单（仅用于退出登录场景）
        /// </summary>
        private static readonly HashSet<string> _tokenBlacklist = new();
        private static readonly object _lockObject = new();

        public TokenService(IOptions<JwtSettings> jwtSettings, ILogger<TokenService> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _logger = logger;

            //if (string.IsNullOrEmpty(_jwtSettings.SecretKey) || _jwtSettings.SecretKey.Length < 32)
            //{
            //    throw new InvalidOperationException("JWT SecretKey 必须至少 32 个字符");
            //}

            _logger.LogInformation($"TokenService 已初始化，密钥长度: {_jwtSettings.SecretKey.Length}，颁发者: {_jwtSettings.Issuer}");
        }

        /// <summary>
        /// 创建 JWT Token
        /// </summary>
        public string CreateToken(string userId)
        {
            try
            {
                // ? 验证输入参数
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("userId 不能为空");

                if (_jwtSettings.SecretKey.Length < 32)
                {
                    // 补齐到 32 位，例如用原 Key 重复填充或加固定后缀
                    string paddedKey = _jwtSettings.SecretKey.PadRight(32, '0');
                    _jwtSettings.SecretKey = paddedKey;
                    _logger.LogWarning($"? 检测到 SecretKey 长度不足 32 位，已在内存中自动补齐。当前长度: {_jwtSettings.SecretKey.Length}");
                }

                //if (string.IsNullOrEmpty(userRole))
                //    throw new ArgumentException("userRole 不能为空");

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    //new Claim(ClaimTypes.Role, userRole),
                    new Claim("UserId", userId),
                    //new Claim("Role", userRole)
                };

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
                    Issuer = _jwtSettings.Issuer,
                    Audience = _jwtSettings.Audience,
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var jwtToken = tokenHandler.WriteToken(token);

                _logger.LogInformation($"? JWT Token 已生成 | UserId: {userId} | 过期时间: {tokenDescriptor.Expires}");
                return jwtToken;
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Token 生成失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 验证 Token
        /// </summary>
        public bool ValidateToken(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("? Token 为空");
                    return false;
                }

                // 1. 检查黑名单
                lock (_lockObject)
                {
                    if (_tokenBlacklist.Contains(token))
                    {
                        _logger.LogWarning("? Token 已被撤销");
                        return false;
                    }
                }

                // 2. 验证 JWT 签名和过期时间
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                _logger.LogInformation("? Token 验证成功");
                return true;
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning($"? Token 已过期: {ex.Message}");
                return false;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogWarning($"? Token 签名无效（密钥可能不匹配）: {ex.Message}");
                return false;
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning($"? Token 验证失败: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Token 验证异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从 Token 获取用户 ID
        /// </summary>
        public string? GetUserIdFromToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                // ? 先尝试 ClaimTypes.NameIdentifier
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => 
                    c.Type == ClaimTypes.NameIdentifier);
                
                // 如果没找到，尝试自定义 "UserId"
                if (userIdClaim == null)
                {
                    userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId");
                }

                var userId = userIdClaim?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning($"??  Token 中未找到 UserId 信息");
                    return null;
                }

                _logger.LogInformation($"? 从 Token 提取用户 ID 成功: {userId}");
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"? 从 Token 获取用户 ID 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 撤销 Token
        /// </summary>
        public void RevokeToken(string token)
        {
            try
            {
                lock (_lockObject)
                {
                    _tokenBlacklist.Add(token);
                }
                _logger.LogInformation("? Token 已被撤销");
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Token 撤销失败: {ex.Message}");
                throw;
            }
        }
    }
}