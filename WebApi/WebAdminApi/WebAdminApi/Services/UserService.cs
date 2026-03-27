using WebAdminApi.DBs;
using WebAdminApi.DTOs;
using WebAdminApi.Entities;

namespace WebAdminApi.Services
{
    /// <summary>
    /// 用户服务实现类
    /// 负责用户相关的业务逻辑处理
    /// </summary>
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbContext;
        private readonly ITokenService _tokenService;

        public UserService(AppDbContext dbContext, ITokenService tokenService)
        {
            _dbContext = dbContext;
            _tokenService = tokenService;
        }

        /// <summary>
        /// 获取用户列表（分页），支持按昵称或手机号搜索
        /// </summary>
        public UserListPageDto GetUserListPage(string? keyword, int pageNum = 1, int pageSize = 10)
        {
            var query = GetUserQuery(keyword);

            // 计算总数
            int total = query.Count();

            // 分页
            var data = query
                .Skip((pageNum - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new UserListPageDto
            {
                pageNum = pageNum,
                pageSize = pageSize,
                total = total,
                records = data
            };
        }

        /// <summary>
        /// 获取用户列表，支持按昵称或手机号搜索
        /// </summary>
        public List<UserListItemDto> GetUserList(string? keyword)
        {
            return GetUserQuery(keyword).ToList();
        }

        /// <summary>
        /// 构建用户查询（内部方法）
        /// </summary>
        private IQueryable<UserListItemDto> GetUserQuery(string? keyword)
        {
            var query = from adminuser in _dbContext.AdminStaffs
                        join r in _dbContext.Roles
                        on adminuser.Role equals r.RoleId
                        select new UserListItemDto
                        {
                            id = adminuser.AdminId,
                            phone = adminuser.Phone,
                            nickname = adminuser.NickName,
                            gender = adminuser.Gender ?? "未设置",
                            address = adminuser.Address ?? "未设置",
                            role = r.RoleName ?? "普通用户",
                            status = adminuser.Status,
                            loginTime = adminuser.LoginTime != null 
                                ? adminuser.LoginTime.Value.ToString("yyyy/M/d H:mm") 
                                : "未登录",
                            selected = false
                        };

            // 如果提供了搜索关键词，则进行模糊查询
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u =>
                    u.nickname.Contains(keyword) ||
                    u.phone.Contains(keyword)
                );
            }

            return query;
        }

        /// <summary>
        /// 添加新用户
        /// </summary>
        public async Task<bool> AddUser(AddUserDto dto)
        {
            // 检查手机号是否已存在
            if (_dbContext.AdminStaffs.Any(u => u.Phone == dto.Phone))
            {
                throw new Exception("手机号已存在");
            }

            // 获取角色ID
            int roleId = GetRoleIdByName(dto.Role);

            // 创建新用户实体
            var newUser = new AdminStaffs
            {
                AdminId = GenerateAdminId(),
                Phone = dto.Phone,
                NickName = dto.Nickname,
                Gender = dto.Gender,
                Address = dto.Address ?? "未设置",
                Role = roleId,
                Status = dto.Status,
                Password = "123456",
                Token = "",
                LoginTime = null,
                RegisterTime = DateTime.Now
            };

            _dbContext.AdminStaffs.Add(newUser);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 编辑现有用户信息
        /// 只更新前端发送的字段，其他字段保持不变
        /// </summary>
        public async Task<bool> EditUser(EditUserDto dto)
        {
            var user = _dbContext.AdminStaffs.FirstOrDefault(u => u.AdminId == dto.id);
            
            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            if (!string.IsNullOrWhiteSpace(dto.nickname))
                user.NickName = dto.nickname;
            
            if (!string.IsNullOrWhiteSpace(dto.gender))
                user.Gender = dto.gender;
            
            if (!string.IsNullOrWhiteSpace(dto.address))
                user.Address = dto.address;
            
            if (!string.IsNullOrWhiteSpace(dto.role))
                user.Role = GetRoleIdByName(dto.role);
            
            if (!string.IsNullOrWhiteSpace(dto.status))
                user.Status = dto.status;
            
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 更改用户状态（启用/禁用）
        /// </summary>
        public async Task<bool> ChangeUserStatus(string userId, string status)
        {
            var user = _dbContext.AdminStaffs.FirstOrDefault(u => u.AdminId == userId);
            
            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            user.Status = status;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 删除指定用户
        /// </summary>
        public async Task<bool> DeleteUser(string userId)
        {
            var user = _dbContext.AdminStaffs.FirstOrDefault(u => u.AdminId == userId);
            
            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            _dbContext.AdminStaffs.Remove(user);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 用户登录（仅管理员可登录）
        /// </summary>
        public async Task<LoginResponseDto?> Login(string phone, string password)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
                throw new Exception("手机号和密码不能为空");

            var user = _dbContext.AdminStaffs.FirstOrDefault(u => u.Phone == phone);
            
            if (user == null)
                throw new Exception("该手机号未注册");
            
            if (user.Status == "禁用")
                throw new Exception("账号已禁用，请联系管理员");
            
            if (user.Password != password)
                throw new Exception("密码错误，请重新输入");
            
            // 获取用户角色信息
            var role = _dbContext.Roles.FirstOrDefault(r => r.RoleId == user.Role);
            string roleName = role?.RoleName ?? "普通用户";

            // 核心：只有管理员才能登录
            if (roleName != "管理员")
                throw new Exception("权限不足，仅管理员可登录");
            
            // 更新最后登录时间
            user.LoginTime = DateTime.Now;
            await _dbContext.SaveChangesAsync();
            
            // 使用 TokenService 生成 Token
            string token = _tokenService.CreateToken(user.AdminId, roleName);
            
            return new LoginResponseDto
            {
                id = user.AdminId,
                phone = user.Phone,
                nickname = user.NickName,
                gender = user.Gender,
                role = roleName,
                status = user.Status,
                token = token
            };
        }

        #region 辅助方法

        /// <summary>
        /// 生成新用户的AdminId
        /// 格式：U + yyyyMMdd + 6位序号
        /// 例如：U202603270000001
        /// </summary>
        private string GenerateAdminId()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = _dbContext.AdminStaffs
                .Where(u => u.AdminId.StartsWith($"U{date}"))
                .Count() + 1;
            return $"U{date}{sequence:D6}";
        }

        /// <summary>
        /// 根据角色名称获取角色ID
        /// </summary>
        private int GetRoleIdByName(string roleName)
        {
            var role = _dbContext.Roles.FirstOrDefault(r => r.RoleName == roleName);
            return role?.RoleId ?? 1;
        }

        #endregion
    }
}
