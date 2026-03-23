namespace WebAdminApi.DTOs
{
    /// <summary>
    /// 用户列表响应DTO
    /// </summary>
    public class UserListItemDto
    {
        public string Id { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Nickname { get; set; } = null!;
        public string LoginTime { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string Status { get; set; } = null!;
        public bool Selected { get; set; } = false;
    }

    /// <summary>
    /// 新增用户请求DTO
    /// </summary>
    public class AddUserDto
    {
        public string Phone { get; set; } = null!;
        public string Nickname { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public string? Address { get; set; }
        public string Role { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    /// <summary>
    /// 编辑用户请求DTO
    /// </summary>
    public class EditUserDto
    {
        public string Id { get; set; } = null!;
        public string? Nickname { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>
    /// 修改用户状态请求DTO
    /// </summary>
    public class ChangeStatusDto
    {
        public string Id { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    /// <summary>
    /// 删除用户请求DTO
    /// </summary>
    public class DeleteUserDto
    {
        public string Id { get; set; } = null!;
    }
}