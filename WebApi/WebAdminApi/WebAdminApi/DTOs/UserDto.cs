namespace WebAdminApi.DTOs
{
    /// <summary>
    /// 用户列表响应DTO（符合API文档）
    /// </summary>
    public class UserListItemDto
    {
        /// <summary>
        /// 用户唯一ID（格式：U + yyyyMMdd + 序号，例：U20260101120019）
        /// </summary>
        public string id { get; set; } = null!;

        public string phone { get; set; } = null!;
        public string nickname { get; set; } = null!;
        public string gender { get; set; } = null!;
        public string address { get; set; } = null!;

        /// <summary>
        /// 角色：管理员/普通用户
        /// </summary>
        public string role { get; set; } = null!;

        /// <summary>
        /// 状态：启用/禁用
        /// </summary>
        public string status { get; set; } = null!;

        /// <summary>
        /// 最后登录时间（格式：yyyy/M/d H:mm）
        /// </summary>
        public string loginTime { get; set; } = "未登录";

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool selected { get; set; } = false;
    }

    /// <summary>
    /// 用户列表分页响应DTO
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
        public string Nickname { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public string? Address { get; set; }
        public string Role { get; set; } = null!;
        public string Status { get; set; } = "禁用";
    }

    /// <summary>
    /// 编辑用户请求DTO
    /// 用户在前端选中用户后，修改字段并发送此对象
    /// </summary>
    public class EditUserDto
    {
        /// <summary>
        /// 用户ID（必须提供，用来识别要编辑哪个用户）
        /// </summary>
        public string id { get; set; } = null!;
        
        /// <summary>
        /// 昵称（可选，不修改可以不发）
        /// </summary>
        public string? nickname { get; set; }
        
        /// <summary>
        /// 性别（可选，不修改可以不发）
        /// </summary>
        public string? gender { get; set; }
        
        /// <summary>
        /// 地址（可选，不修改可以不发）
        /// </summary>
        public string? address { get; set; }
        
        /// <summary>
        /// 角色：管理员/普通用户（可选，不修改可以不发）
        /// </summary>
        public string? role { get; set; }
        
        /// <summary>
        /// 状态：启用/禁用（可选，不修改可以不发）
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
        /// 目标状态：启用/禁用
        /// </summary>
        public string status { get; set; } = null!;
    }

    /// <summary>
    /// 删除用户请求DTO
    /// </summary>
    public class DeleteUserDto
    {
        public string id { get; set; } = null!;
    }

    /// <summary>
    /// 用户登录请求DTO
    /// </summary>
    public class LoginDto
    {
        public string phone { get; set; } = null!;
        public string password { get; set; } = null!;
    }

    /// <summary>
    /// 用户登录响应DTO
    /// </summary>
    public class LoginResponseDto
    {
        public string id { get; set; } = null!;
        public string phone { get; set; } = null!;
        public string nickname { get; set; } = null!;
        public string gender { get; set; } = null!;
        public string role { get; set; } = null!;
        public string status { get; set; } = null!;
        public string token { get; set; } = null!;
    }
}