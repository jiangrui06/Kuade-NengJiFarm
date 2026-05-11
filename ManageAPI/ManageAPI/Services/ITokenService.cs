namespace WebAPI.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// 创建新Token
        /// </summary>
        string CreateToken(string userId);

        /// <summary>
        /// 验证Token是否有效
        /// </summary>
        bool ValidateToken(string token);


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