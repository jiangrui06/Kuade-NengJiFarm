using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using WebAPI.Configuration;

namespace WebAPI.Services
{
    /// <summary>
    /// Token ����ʵ�֣�JWT ������
    /// ��״̬��֤�������ѯ���ݿ�
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<TokenService> _logger;

        /// <summary>
        /// Token ���������������˳���¼������
        /// </summary>
        private static readonly HashSet<string> _tokenBlacklist = new();
        private static readonly object _lockObject = new();

        public TokenService(IOptions<JwtSettings> jwtSettings, ILogger<TokenService> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_jwtSettings.SecretKey) || _jwtSettings.SecretKey.Length < 32)
            {
                _logger.LogWarning($"JWT SecretKey 长度不足 32 位（当前: {_jwtSettings.SecretKey?.Length ?? 0}），自动补全");
                _jwtSettings.SecretKey = (_jwtSettings.SecretKey ?? string.Empty).PadRight(32, '0');
            }

            _logger.LogInformation($"TokenService 已初始化，密钥长度: {_jwtSettings.SecretKey.Length}，签发者: {_jwtSettings.Issuer}");
        }

        /// <summary>
        /// ���� JWT Token
        /// </summary>
        public string CreateToken(string userId)
        {
            try
            {
                // ? ��֤�������
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("userId ����Ϊ��");

                if (_jwtSettings.SecretKey.Length < 32)
                {
                    // ���뵽 32 λ��������ԭ Key �ظ�����ӹ̶���׺
                    string paddedKey = _jwtSettings.SecretKey.PadRight(32, '0');
                    _jwtSettings.SecretKey = paddedKey;
                    _logger.LogWarning($"? ��⵽ SecretKey ���Ȳ��� 32 λ�������ڴ����Զ����롣��ǰ����: {_jwtSettings.SecretKey.Length}");
                }

                //if (string.IsNullOrEmpty(userRole))
                //    throw new ArgumentException("userRole ����Ϊ��");

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    //new Claim(ClaimTypes.Role, userRole),
                    new Claim("UserId", userId),
                    new Claim("token_type", "admin"),
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

                _logger.LogInformation($"? JWT Token ������ | UserId: {userId} | ����ʱ��: {tokenDescriptor.Expires}");
                return jwtToken;
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Token ����ʧ��: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ��֤ Token
        /// </summary>
        public bool ValidateToken(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("? Token Ϊ��");
                    return false;
                }

                // 1. ��������
                lock (_lockObject)
                {
                    if (_tokenBlacklist.Contains(token))
                    {
                        _logger.LogWarning("? Token �ѱ�����");
                        return false;
                    }
                }

                // 2. ��֤ JWT ǩ���͹���ʱ��
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

                _logger.LogInformation("? Token ��֤�ɹ�");
                return true;
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning($"? Token �ѹ���: {ex.Message}");
                return false;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogWarning($"? Token ǩ����Ч����Կ���ܲ�ƥ�䣩: {ex.Message}");
                return false;
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning($"? Token ��֤ʧ��: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Token ��֤�쳣: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// �� Token ��ȡ�û� ID
        /// </summary>
        public string? GetUserIdFromToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                // ? �ȳ��� ClaimTypes.NameIdentifier
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier);

                // ���û�ҵ��������Զ��� "UserId"
                if (userIdClaim == null)
                {
                    userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId");
                }

                var userId = userIdClaim?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning($"??  Token ��δ�ҵ� UserId ��Ϣ");
                    return null;
                }

                _logger.LogInformation($"? �� Token ��ȡ�û� ID �ɹ�: {userId}");
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"? �� Token ��ȡ�û� ID ʧ��: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ���� Token
        /// </summary>
        public void RevokeToken(string token)
        {
            try
            {
                lock (_lockObject)
                {
                    _tokenBlacklist.Add(token);
                }
                _logger.LogInformation("? Token �ѱ�����");
            }
            catch (Exception ex)
            {
                _logger.LogError($"? Token ����ʧ��: {ex.Message}");
                throw;
            }
        }
    }
}