namespace WebAdminApi.Services
{
    public class TokenService : ITokenService
    {
        // 示例token格式: admin_token_valid 代表管理员，user_token_valid 代表普通用户
        private static readonly Dictionary<string, (string Role, string UserId)> ValidTokens = new()
        {
            { "admin_token_valid", ("管理员", "U20260101000001") },
            { "user_token_valid", ("普通用户", "U20260101000003") }
        };

        public bool ValidateToken(string token)
        {
            return ValidTokens.ContainsKey(token);
        }

        public string? GetUserRoleFromToken(string token)
        {
            return ValidTokens.TryGetValue(token, out var value) ? value.Role : null;
        }

        public string? GetUserIdFromToken(string token)
        {
            return ValidTokens.TryGetValue(token, out var value) ? value.UserId : null;
        }
    }
}