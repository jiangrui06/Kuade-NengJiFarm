using WebAPI.Dtos;

namespace WebAPI.Services
{
    public interface IUserService
    {
        UserListPageDto GetUserListPage(string? keyword, int pageNum = 1, int pageSize = 10);
        List<UserListItemDto> GetUserList(string? keyword);
        Task<(bool Success, bool IsDefaultPassword)> AddUser(AddUserDto dto);
        Task<bool> EditUser(EditUserDto dto);
        //Task<bool> ChangeUserStatus(string userId, string status);
        Task<bool> DeleteUser(string userId);
        Task<LoginResponseDto?> Login(string phone, string password);
        Task<UserDetailDto?> GetUserDetailAsync(string userId);
        /// <summary>
        /// ��ȡ�û����飨�����û�ID��
        /// </summary>
        Task<UserDetailDto?> GetUserDetailByIdAsync(int userId);

        /// <summary>
        /// ��ȡ�û����飨����UserGuid��
        /// </summary>
        Task<UserDetailDto?> GetUserDetailByGuidAsync(string userGuid);
    }
}