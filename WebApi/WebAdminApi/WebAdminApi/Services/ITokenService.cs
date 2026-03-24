namespace WebAdminApi.Services
{
    public interface ITokenService
    {
        bool ValidateToken(string token);
        string? GetUserRoleFromToken(string token);
        string? GetUserIdFromToken(string token);
    }
}