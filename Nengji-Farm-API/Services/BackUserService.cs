using System.Data;

using Microsoft.EntityFrameworkCore;

using WebAPI.Data;

using WebAPI.Dtos;
using WebAPI.Entities.Manage;
using WebAPI.PasswordHash;

namespace WebAPI.Services
{
    /// <summary>
    /// 用户服务实现类
    /// 负责用户相关的业务逻辑处理
    /// </summary>
    public class BackUserService : IUserService
    {
        private readonly ManageAppDbContext _dbContext;
        private readonly ITokenService _tokenService;
    private readonly ILogger<BackUserService> _logger;
    private readonly IPasswordService _passwordService;
    private const string DefaultPassword = "33668899aA@";

    public BackUserService(ManageAppDbContext dbContext, ITokenService tokenService, ILogger<BackUserService> logger, IPasswordService passwordService)
        {
            _dbContext = dbContext;
            _tokenService = tokenService;
            _logger = logger;
            _passwordService = passwordService;
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
                records = data,
                pages = (int)Math.Ceiling((double)total / pageSize)
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
            var userQuery = from u in _dbContext.Users
                            join r in _dbContext.Roles
                            on u.RoleId equals r.RoleId into roleGroup
                            from rg in roleGroup.DefaultIfEmpty()
                            select new
                            {
                                id = u.UserId.ToString(),
                                Guid = u.UserGuid,
                                phone = u.PhoneNumber,
                                nickname = u.WxName,
                                realName = u.RealName,
                                WxOpenid = u.WxOpenId,
                                gender = u.Gender ?? "保密",
                                role = rg != null ? rg.RoleName : "普通用户",
                                loginTime = u.RegisterTime,
                                selected = false,
                                userType = "user"
                            };

            // 如果提供了搜索关键词，则进行模糊查询
            var query = userQuery;

            // ✅ 搜索（统一作用）
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u =>
                    u.nickname.Contains(keyword) ||
                    u.phone.Contains(keyword) ||
                    u.realName.Contains(keyword)
                );
            }

            var result = query.Select(u => new UserListItemDto
            {
                id = u.id,
                Guid = u.Guid,
                phone = u.phone,
                nickname = u.nickname ?? u.realName ?? string.Empty,
                WxOpenid = u.WxOpenid,
                gender = u.gender ?? "保密",
                role = u.role ?? "普通用户",
                selected = u.selected,
                userType = u.userType, loginTime = u.loginTime.HasValue ? u.loginTime.Value.ToString("yyyy-MM-dd HH:mm") : null
            });

            return result;
        }

        /// <summary>
        /// 添加新用户
        /// </summary>
        public async Task<(bool Success, bool IsDefaultPassword)> AddUser(AddUserDto dto)
        {
            // 检查手机号是否已存在
            if (_dbContext.Users.Any(u => u.PhoneNumber == dto.Phone))
            {
                throw new Exception("手机号已存在");
            }

            // 密码处理：未提供则使用默认密码
            string password = string.IsNullOrWhiteSpace(dto.Password) ? DefaultPassword : dto.Password;
            bool isDefaultPassword = string.IsNullOrWhiteSpace(dto.Password);

            // 密码强度校验
            if (!IsPasswordValid(password))
                throw new Exception("密码必须超过9位并且包含大小写字母和符号");

            string NewUserGuid = Guid.NewGuid().ToString();

            // 创建新用户实体
            var newUser = new User
            {
                UserGuid = NewUserGuid,
                PhoneNumber = dto.Phone,
                RegisterTime = DateTime.Now,
                WxOpenId = null,
                WxImage = null,
                WxName = !string.IsNullOrWhiteSpace(dto.Nickname) ? dto.Nickname : dto.RealName,
                RealName = dto.RealName,
                Password = _passwordService.HashPassword(password),
                Gender = dto.Gender,
                RoleId = dto.RoleId ?? 2,
                Points = 0
            };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"✅ 新增用户成功 | 手机号: {dto.Phone} | 昵称: {dto.RealName} | 角色: {dto.RoleId} | 用户ID: {newUser.UserId}");
            return (true, isDefaultPassword);
        }

        /// <summary>
        /// 密码强度校验：9位以上、包含大小写字母、包含特殊符号
        /// </summary>
        private static bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 9)
                return false;

            bool hasLowerCase = password.Any(char.IsLower);
            bool hasUpperCase = password.Any(char.IsUpper);
            bool hasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));

            return hasLowerCase && hasUpperCase && hasSpecialChar;
        }

        /// <summary>
        /// 编辑现有用户信息
        /// 只更新前端发送的字段，其他字段保持不变
        /// </summary>
        public async Task<bool> EditUser(EditUserDto dto)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.UserGuid == dto.Guid);

            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            if (!string.IsNullOrWhiteSpace(dto.nickname))
                user.WxName = dto.nickname;

            if (!string.IsNullOrWhiteSpace(dto.gender))
                user.Gender = dto.gender;

            if (!string.IsNullOrWhiteSpace(dto.role))
                user.RoleId = GetRoleIdByName(dto.role);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"✅ 编辑用户成功 | 用户ID: {dto.Guid}");
            return true;
        }

        /// <summary>
        /// 删除指定用户
        /// </summary>
        public async Task<bool> DeleteUser(string UserGuid)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.UserGuid == UserGuid);

            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"✅ 用户已删除 | 用户GUID: {UserGuid}");
            return true;
        }

        public async Task<UserDetailDto?> GetUserDetailAsync(string id)
        {
            if (!int.TryParse(id, out var userId)) return null;
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);


            if (user == null) return null;

            return MapUserToDetailDto(user);
        }

        /// <summary>
        /// 用户登录（仅管理员可登录）
        /// </summary>
        public async Task<LoginResponseDto?> Login(string user_no, string password)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(user_no) || string.IsNullOrWhiteSpace(password))
                throw new Exception("管理员账号和密码不能为空");

            _logger.LogInformation($"🔍 开始验证用户 | 管理员账号: {user_no}");

            var admin = _dbContext.Admins.FirstOrDefault(u => u.UserNo == user_no);

            if (admin == null)
            {
                _logger.LogWarning($"❌ 用户未找到 | 管理员账号: {user_no}");
                throw new Exception("该管理员账号未注册");
            }


            //_logger.LogInformation($"TEST_HASH: {BCrypt.Net.BCrypt.HashPassword("123")}");
            bool isPasswordValid = _passwordService.VerifyPassword(password, admin.UserPassword);

            if (!isPasswordValid)
            {
                _logger.LogWarning($"❌ 密码错误 | 用户ID: {admin.UserNo}");
                throw new Exception("密码错误，请重新输入");
            }

            _logger.LogInformation($"✅ 登录成功 | 用户ID: {admin.UserNo}");

            //仅更新最后登录时间，不存储 Token
            //user.LoginTime = DateTime.Now;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"✅ 登录时间已更新 | 用户ID: {admin.UserNo}");

            // 生成 JWT Token（无需存数据库）
            string token = _tokenService.CreateToken(admin.UserNo);

            _logger.LogInformation($"✅ JWT Token 已生成 | 用户ID: {admin.UserNo}");

            return new LoginResponseDto
            {
                user_no = admin.UserNo,

                user_password = admin.UserPassword,
                //status = user.Status,
                token = token


            };
        }

        #region 辅助方法

        ///// <summary>
        ///// 生成新员工的AdminId
        ///// 格式：staff_ + 5位序号
        ///// 例如：staff_10001、staff_10002
        ///// </summary>
        //private string GenerateAdminId()
        //{
        //    // 获取所有以 "staff_" 开头的管理员ID
        //    var maxStaffId = _dbContext.Users
        //        .Where(u => u.WxOpenId.StartsWith("staff_"))
        //        .Select(u => u.WxOpenId)
        //        .AsEnumerable()
        //        .Select(id => int.TryParse(id.Replace("staff_", ""), out int num) ? num : 0)
        //        .DefaultIfEmpty(10000)
        //        .Max();

        //    return $"staff_{maxStaffId + 1:D5}";
        //}

        /// <summary>
        /// 根据角色名称获取角色ID
        /// 注意：应该从 role_staff 表查询，而不是 role 表
        /// </summary>
        private int GetRoleIdByName(string roleName)
        {
            var role = _dbContext.Roles.FirstOrDefault(r => r.RoleName == roleName);

            if (role == null)
            {
                _logger.LogWarning($"⚠️  角色未找到 | 角色名: {roleName}，默认使用角色ID: 1");
                return 1;
            }

            _logger.LogInformation($"✅ 角色ID已获取 | 角色名: {roleName} | 角色ID: {role.RoleId}");
            return role.RoleId;
        }

        #endregion

        /// <summary>
        /// 获取用户详情（基于用户ID）
        /// </summary>
        public async Task<UserDetailDto?> GetUserDetailByIdAsync(int userId)
        {
            var user = await _dbContext.Users
                .Where(u => u.UserId == userId)
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            return MapUserToDetailDto(user);
        }

        /// <summary>
        /// 获取用户详情（基于UserGuid）
        /// </summary>
        public async Task<UserDetailDto?> GetUserDetailByGuidAsync(string userGuid)
        {
            var user = await _dbContext.Users
                .Where(u => u.UserGuid == userGuid)
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            return MapUserToDetailDto(user);
        }

        /// <summary>
        /// 将User实体映射为UserDetailDto
        /// 这样可以复用投影逻辑
        /// </summary>
        private UserDetailDto MapUserToDetailDto(User user)
        {
            return new UserDetailDto
            {
                id = user.UserId,
                Guid = user.UserGuid,
                phone = user.PhoneNumber ?? string.Empty,
                nickname = user.WxName ?? user.RealName ?? string.Empty,
                avatar = user.WxImage ?? "https://example.com/default-avatar.jpg",
                gender = user.Gender ?? "未设置",
                loginTime = user.RegisterTime?.ToString("yyyy年MM月dd日 HH:mm") ?? "无记录",
                realName = user.RealName ?? string.Empty,
                wxOpenId = user.WxOpenId ?? string.Empty,
                roleId = user.RoleId,
            };
        }
    }
}
