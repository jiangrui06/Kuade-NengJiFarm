namespace WebAPI.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// ������Token
        /// </summary>
        string CreateToken(string userId);

        /// <summary>
        /// ��֤Token�Ƿ���Ч
        /// </summary>
        bool ValidateToken(string token);


        /// <summary>
        /// ��Token��ȡ�û�ID
        /// </summary>
        string? GetUserIdFromToken(string token);

        /// <summary>
        /// ����Token���˳���¼��
        /// </summary>
        void RevokeToken(string token);

        /// <summary>
        /// ��֤Token������ ClaimsPrincipal
        /// </summary>
        System.Security.Claims.ClaimsPrincipal? GetPrincipalFromToken(string token);
    }
}