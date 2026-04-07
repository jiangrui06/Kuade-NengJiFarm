using WebAdminApi.DBs;
using WebAdminApi.DTOs;
using WebAdminApi.Entities;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext dbContext, ITokenService tokenService, ILogger<UserService> logger)
        {
            _dbContext = dbContext;
            _tokenService = tokenService;
            _logger = logger;
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
            var adminQuery = from adminuser in _dbContext.AdminStaffs
                        join r in _dbContext.Role_Staffs
                        on adminuser.Role equals r.RoleStaffId
                        select new UserListItemDto
                        {
                            id = adminuser.AdminId,
                            phone = adminuser.Phone,
                            nickname = adminuser.NickName,
                            WxOpenid = null,
                            gender = adminuser.Gender ?? "未设置",
                            address = adminuser.Address ?? "未设置",
                            role = r.RoleStaffName ?? "普通用户",
                            status = adminuser.Status,
                            loginTime = adminuser.LoginTime != null 
                                ? adminuser.LoginTime.Value.ToString("yyyy/M/d H:mm") 
                                : "未登录",
                            selected = false,

                            userType = "admin"
                        };

            var userQuery = from u in _dbContext.WeChatUsers
                            join r in _dbContext.Roles
                            on u.RoleId equals r.RoleId
                            select new UserListItemDto
                            {
                                id = u.UserId.ToString(),
                                phone = u.PhoneNumber,
                                nickname = u.WxName,

                                // ❗没有的字段全部用 null
                                gender = null,
                                address = null,

                                role = r.RoleName,

                                status = null,
                               
                                loginTime = u.RegisterTime.ToString("yyyy/M/d H:mm"),

                                selected = false,

                                userType = "user",

                                WxOpenid = u.WxOpenId
                            };

            // 如果提供了搜索关键词，则进行模糊查询
            var query = adminQuery.Concat(userQuery);

            // ✅ 搜索（统一作用）
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

            // 获取角色ID（从 role_staff 表）
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
                LoginTime = null,
                RegisterTime = DateTime.Now
            };

            _dbContext.AdminStaffs.Add(newUser);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation($"✅ 新增用户成功 | 手机号: {dto.Phone} | 昵称: {dto.Nickname} | 角色: {dto.Role}");
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
            
            _logger.LogInformation($"✅ 编辑用户成功 | 用户ID: {dto.id}");
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
            
            _logger.LogInformation($"✅ 用户状态已更改 | 用户ID: {userId} | 新状态: {status}");
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
            
            _logger.LogInformation($"✅ 用户已删除 | 用户ID: {userId}");
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

            _logger.LogInformation($"🔍 开始验证用户 | 手机号: {phone}");

            var user = _dbContext.AdminStaffs.FirstOrDefault(u => u.Phone == phone);
            
            if (user == null)
            {
                _logger.LogWarning($"❌ 用户未找到 | 手机号: {phone}");
                throw new Exception("该手机号未注册");
            }
            
            _logger.LogInformation($"✅ 用户找到 | UserId: {user.AdminId} | RoleId: {user.Role}");

            if (user.Status == "禁用")
            {
                _logger.LogWarning($"❌ 用户已禁用 | 用户ID: {user.AdminId}");
                throw new Exception("账号已禁用，请联系管理员");
            }
            
            if (user.Password != password)
            {
                _logger.LogWarning($"❌ 密码错误 | 用户ID: {user.AdminId}");
                throw new Exception("密码错误，请重新输入");
            }
            
            // 获取用户角色信息（从 role_staff 表）
            var role = _dbContext.Role_Staffs.FirstOrDefault(r => r.RoleStaffId == user.Role);
            string roleName = role?.RoleStaffName ?? "普通用户";

            _logger.LogInformation($"📋 角色信息 | RoleStaffId: {user.Role} | RoleStaffName: {roleName}");

            // 只有管理员才能登录
            if (roleName != "管理员")
            {
                _logger.LogWarning($"❌ 权限不足 | 用户ID: {user.AdminId} | 角色: {roleName}");
                throw new Exception($"权限不足，仅管理员可登录，你的角色是: {roleName}");
            }
            
            // 仅更新最后登录时间，不存储 Token
            user.LoginTime = DateTime.Now;
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation($"✅ 登录时间已更新 | 用户ID: {user.AdminId}");
            
            // 生成 JWT Token（无需存数据库）
            string token = _tokenService.CreateToken(user.AdminId, roleName);
            
            _logger.LogInformation($"✅ JWT Token 已生成 | 用户ID: {user.AdminId} | 角色: {roleName}");

            return new LoginResponseDto
            {
                id = user.AdminId,
                phone = user.Phone,
                nickname = user.NickName,
                gender = user.Gender,
                role = roleName,
                status = user.Status,
                token = token  // 直接返回生成的 Token
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
        /// 注意：应该从 role_staff 表查询，而不是 role 表
        /// </summary>
        private int GetRoleIdByName(string roleName)
        {
            // ✅ 改为从 Role_Staffs 表查询
            var role = _dbContext.Role_Staffs.FirstOrDefault(r => r.RoleStaffName == roleName);
            
            if (role == null)
            {
                _logger.LogWarning($"⚠️  角色未找到 | 角色名: {roleName}，默认使用角色ID: 1");
                return 1;
            }

            _logger.LogInformation($"✅ 角色ID已获取 | 角色名: {roleName} | 角色ID: {role.RoleStaffId}");
            return role.RoleStaffId;
        }

        #endregion
    }
}
