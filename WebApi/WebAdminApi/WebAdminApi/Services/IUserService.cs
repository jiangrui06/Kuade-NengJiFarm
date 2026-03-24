using WebAdminApi.DTOs;

namespace WebAdminApi.Services
{
    public interface IUserService
    {
        List<UserListItemDto> GetUserList(string? keyword);
        bool AddUser(AddUserDto dto);
        bool EditUser(EditUserDto dto);
        bool ChangeUserStatus(string userId, string status);
        bool DeleteUser(string userId);
    }
}