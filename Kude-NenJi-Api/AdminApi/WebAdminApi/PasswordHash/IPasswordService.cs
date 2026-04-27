namespace WebAdminApi.PasswordHash
{
    public interface IPasswordService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }

    public class PasswordService : IPasswordService
    {
        private const int WorkFactor = 11;

        public string HashPassword(string password)
        {
            // 注册时调用
            return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        }

        public bool VerifyPassword(string password, string hash)
        {
            // 登录验证时调用
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}
