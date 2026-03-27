using System;
using System.Collections.Concurrent;

namespace WebAdminApi.Services
{
    public class TokenService : ITokenService
    {
        /// <summary>
        /// 存储有效的Token及其关联信息
        /// Key: Token值, Value: (UserId, UserRole, ExpirationTime)
        /// </summary>
        private static readonly ConcurrentDictionary<string, (string UserId, string Role, DateTime ExpirationTime)> 
            _validTokens = new();

        /// <summary>
        /// Token 过期时间（24小时）
        /// </summary>
        private static readonly TimeSpan TOKEN_EXPIRATION = TimeSpan.FromHours(24);

        /// <summary>
        /// 创建新Token（登录时调用）
        /// </summary>
        public string CreateToken(string userId, string userRole)
        {
            // 生成唯一的Token
            string token = $"token-{userId}-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";

            // 计算过期时间
            var expirationTime = DateTime.Now.Add(TOKEN_EXPIRATION);

            // 存储Token信息
            _validTokens.TryAdd(token, (userId, userRole, expirationTime));

            return token;
        }

        /// <summary>
        /// 验证Token是否有效
        /// </summary>
        public bool ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            if (!_validTokens.TryGetValue(token, out var tokenInfo))
                return false;

            // 检查是否过期
            if (DateTime.Now > tokenInfo.ExpirationTime)
            {
                _validTokens.TryRemove(token, out _);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从Token获取用户角色
        /// </summary>
        public string? GetUserRoleFromToken(string token)
        {
            if (!_validTokens.TryGetValue(token, out var tokenInfo))
                return null;

            return tokenInfo.Role;
        }

        /// <summary>
        /// 从Token获取用户ID
        /// </summary>
        public string? GetUserIdFromToken(string token)
        {
            if (!_validTokens.TryGetValue(token, out var tokenInfo))
                return null;

            return tokenInfo.UserId;
        }

        /// <summary>
        /// 撤销Token（退出登录时调用）
        /// </summary>
        public void RevokeToken(string token)
        {
            _validTokens.TryRemove(token, out _);
        }
    }
}