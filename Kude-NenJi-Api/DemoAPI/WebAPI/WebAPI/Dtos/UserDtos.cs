namespace WebAPI.DTOs
{
    /// <summary>
    /// 用户列表项对应DTO，用于API的数据返回
    /// </summary>
    public class UserListItemDto
    {
        /// <summary>
        /// 用户唯一ID，格式为：U + yyyyMMdd + 序号，例如：U20260101120019
        /// </summary>
        public string id { get; set; } = null!;

        public string Guid { get; set; } = null!;

        public string phone { get; set; } = null!;
        public string nickname { get; set; } = null!;
        public string? gender { get; set; }
        public string? address { get; set; }

        public string? WxOpenid { get; set; }

        /// <summary>
        /// 角色，可以是/普通用户
        /// </summary>
        public string role { get; set; } = null!;

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool selected { get; set; } = false;

        /// <summary>
        /// 用户类型 - 区分平台的工作人员(staff)或者平台外的微信用户(user)
        /// </summary>
        public string userType { get; set; } = null!;
    }

    /// <summary>
    /// 用户列表分页对应DTO
    /// </summary>
    public class UserListPageDto
    {
        /// <summary>
        /// 当前页码（从1开始）
        /// </summary>
        public int pageNum { get; set; }

        /// <summary>
        /// 每页记录数
        /// </summary>
        public int pageSize { get; set; }

        /// <summary>
        /// 总记录数
        /// </summary>
        public int total { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int pages { get; set; }

        /// <summary>
        /// 用户列表数据
        /// </summary>
        public List<UserListItemDto> records { get; set; } = new();
    }

    /// <summary>
    /// 新增用户请求DTO
    /// </summary>
    public class AddUserDto
    {
        public string Phone { get; set; } = null!;
        public string RealName { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public int? RoleId { get; set; } = null!;

        public string Password { get; set; } = null!;
    }

    public class UserDetailDto
    {
        public string Guid { get; set; } = "";
        public string phone { get; set; } = "";
        public string nickname { get; set; } = "";
        public string avatar { get; set; } = "";
        public string gender { get; set; } = "";
        public string loginTime { get; set; } = "";
    }

    /// <summary>
    /// 编辑用户请求DTO
    /// 用户在前端可选择某个字段修改，仅发送变更的字段，其他字段保持不变
    /// </summary>
    public class EditUserDto
    {
        /// <summary>
        /// 用户ID，必须提供，以此识别要编辑的个用户
        /// </summary>
        public string Guid { get; set; } = null!;

        /// <summary>
        /// 昵称，可选，仅修改时才发送
        /// </summary>
        public string? nickname { get; set; }

        /// <summary>
        /// 性别，可选，仅修改时才发送
        /// </summary>
        public string? gender { get; set; }

        /// <summary>
        /// 地址，可选，仅修改时才发送
        /// </summary>
        public string? address { get; set; }

        /// <summary>
        /// 角色，可以是：管理员/普通用户，可选，仅修改时才发送
        /// </summary>
        public string? role { get; set; }

        /// <summary>
        /// 状态，可以是：启用/禁用，可选，仅修改时才发送
        /// </summary>
        public string? status { get; set; }
    }

    /// <summary>
    /// 修改用户状态请求DTO
    /// </summary>
    public class ChangeStatusDto
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string id { get; set; } = null!;

        /// <summary>
        /// 目标状态，可以是：启用/禁用
        /// </summary>
        public string status { get; set; } = null!;
    }

    /// <summary>
    /// 删除用户请求DTO
    /// </summary>
    public class DeleteUserDto
    {
        public string Guid { get; set; } = null!;
    }

    /// <summary>
    /// 用户登录请求DTO
    /// </summary>
    public class LoginDto
    {
        public string user_no { get; set; } = null!;
        public string password { get; set; } = null!;
    }

    /// <summary>
    /// 用户登录响应DTO
    /// </summary>
    public class LoginResponseDto
    {
        public string user_no { get; set; } = null!;

        public string role { get; } = "user";

        public string LoginTime { get; set; } = DateTime.Now.ToString("yyyy年MM月dd日 HH:mm");

        public string token { get; set; } = null!;

        public string user_password { get; set; } = null!;
    }
}