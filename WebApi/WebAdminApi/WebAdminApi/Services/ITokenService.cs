namespace WebAdminApi.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// 创建新Token
        /// </summary>
        string CreateToken(string userId, string userRole);

        /// <summary>
        /// 验证Token是否有效
        /// </summary>
        bool ValidateToken(string token);

        /// <summary>
        /// 从Token获取用户角色
        /// </summary>
        string? GetUserRoleFromToken(string token);

        /// <summary>
        /// 从Token获取用户ID
        /// </summary>
        string? GetUserIdFromToken(string token);

        /// <summary>
        /// 撤销Token（退出登录）
        /// </summary>
        void RevokeToken(string token);
    }
}