using WebAdminApi.DTOs;
using WebApplication1.Models.Entities;

namespace WebAdminApi.Services
{
    /// <summary>
    /// 用户服务实现类
    /// 负责用户相关的业务逻辑处理
    /// 当前版本使用内存存储，生产环境应替换为数据库访问
    /// </summary>
    public class UserService : IUserService
    {
        /// <summary>
        /// 用户数据集合（内存存储）
        /// 在实际应用中应替换为数据库操作
        /// </summary>
        private static readonly List<User> _users = InitializeUsers();

        /// <summary>
        /// 获取用户列表，支持按昵称或手机号搜索
        /// </summary>
        /// <param name="keyword">搜索关键词（可选）</param>
        /// <returns>用户列表DTO集合</returns>
        public List<UserListItemDto> GetUserList(string? keyword)
        {
            // 创建可枚举集合用于链式查询
            var query = _users.AsEnumerable();

            // 如果提供了搜索关键词，则进行模糊查询
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u =>
                    u.Nickname.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    u.PhoneNumber.Contains(keyword)
                );
            }

            // 将User实体转换为UserListItemDto，用于API返回
            return query.Select(u => new UserListItemDto
            {
                Id = u.UserId,
                Phone = u.PhoneNumber,
                Nickname = u.Nickname,
                LoginTime = u.LoginTime?.ToString("yyyy/M/d HH:mm") ?? "未登录",
                Gender = u.Gender ?? "未设置",
                Address = u.Address ?? "未设置",
                Role = u.Role,
                Status = u.Status,
                Selected = false
            }).ToList();
        }

        /// <summary>
        /// 添加新用户
        /// </summary>
        /// <param name="dto">新用户数据传输对象</param>
        /// <returns>是否添加成功</returns>
        /// <exception cref="Exception">当手机号已存在时抛出异常</exception>
        public bool AddUser(AddUserDto dto)
        {
            // 检查手机号是否已存在
            if (_users.Any(u => u.PhoneNumber == dto.Phone))
            {
                throw new Exception("手机号已存在");
            }

            // 创建新用户实体
            var newUser = new User
            {
                UserId = GenerateUserId(),
                PhoneNumber = dto.Phone,
                Nickname = dto.Nickname,
                Gender = dto.Gender,
                Address = dto.Address,
                Role = dto.Role,
                Status = dto.Status,
                Password = "123456", // 实际应加密
                RegisterTime = DateTime.Now
            };

            // 将用户添加到集合
            _users.Add(newUser);
            return true;
        }

        /// <summary>
        /// 编辑现有用户信息
        /// </summary>
        /// <param name="dto">编辑用户数据传输对象</param>
        /// <returns>是否编辑成功</returns>
        /// <exception cref="Exception">当用户不存在时抛出异常</exception>
        public bool EditUser(EditUserDto dto)
        {
            // 根据用户ID查找用户
            var user = _users.FirstOrDefault(u => u.UserId == dto.Id);
            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            // 更新可修改的字段（仅当值不为空时才更新）
            if (!string.IsNullOrWhiteSpace(dto.Nickname))
                user.Nickname = dto.Nickname;
            if (!string.IsNullOrWhiteSpace(dto.Gender))
                user.Gender = dto.Gender;
            if (!string.IsNullOrWhiteSpace(dto.Address))
                user.Address = dto.Address;
            if (!string.IsNullOrWhiteSpace(dto.Role))
                user.Role = dto.Role;
            if (!string.IsNullOrWhiteSpace(dto.Status))
                user.Status = dto.Status;

            // 更新修改时间
            user.UpdateTime = DateTime.Now;
            return true;
        }

        /// <summary>
        /// 更改用户状态（启用/禁用）
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="status">新状态</param>
        /// <returns>是否更新成功</returns>
        /// <exception cref="Exception">当用户不存在时抛出异常</exception>
        public bool ChangeUserStatus(string userId, string status)
        {
            // 根据用户ID查找用户
            var user = _users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            // 更新用户状态和修改时间
            user.Status = status;
            user.UpdateTime = DateTime.Now;
            return true;
        }

        /// <summary>
        /// 删除指定用户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>是否删除成功</returns>
        /// <exception cref="Exception">当用户不存在时抛出异常</exception>
        public bool DeleteUser(string userId)
        {
            // 根据用户ID查找用户
            var user = _users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            // 从集合中移除用户
            _users.Remove(user);
            return true;
        }

        /// <summary>
        /// 生成用户ID
        /// 格式：U + 日期(yyyyMMdd) + 序号(6位)
        /// 例如：U202601010000001
        /// </summary>
        /// <returns>新生成的用户ID</returns>
        private static string GenerateUserId()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var sequence = _users.Count(u => u.UserId.StartsWith($"U{date}")) + 1;
            return $"U{date}{sequence:D6}";
        }

        /// <summary>
        /// 初始化用户数据（模拟数据库种子数据）
        /// </summary>
        /// <returns>初始化的用户列表</returns>
        private static List<User> InitializeUsers()
        {
            return new List<User>
            {
                new User
                {
                    UserId = "U20260101000001",
                    PhoneNumber = "13758428019",
                    Nickname = "张三",
                    Gender = "男",
                    Address = "筲箕村",
                    Role = "管理员",
                    Status = "禁用",
                    Password = "123456",
                    LoginTime = new DateTime(2026, 1, 1, 17, 0, 0),
                    RegisterTime = new DateTime(2026, 1, 1, 10, 0, 0),
                    WxOpenId = "wx_open_id_001",
                    WxImage = "http://example.com/avatar1.jpg"
                },
                new User
                {
                    UserId = "U20260101000002",
                    PhoneNumber = "18660478321",
                    Nickname = "李四",
                    Gender = "男",
                    Address = "龙洞",
                    Role = "管理员",
                    Status = "启用",
                    Password = "123456",
                    LoginTime = new DateTime(2026, 1, 1, 17, 0, 0),
                    RegisterTime = new DateTime(2026, 1, 1, 11, 0, 0),
                    WxOpenId = "wx_open_id_002",
                    WxImage = "http://example.com/avatar2.jpg"
                },
                new User
                {
                    UserId = "U20260101000003",
                    PhoneNumber = "15612345678",
                    Nickname = "王五",
                    Gender = "女",
                    Address = "天河",
                    Role = "普通用户",
                    Status = "启用",
                    Password = "123456",
                    LoginTime = new DateTime(2026, 1, 1, 15, 30, 0),
                    RegisterTime = new DateTime(2025, 12, 15, 9, 0, 0),
                    WxOpenId = "wx_open_id_003",
                    WxImage = "http://example.com/avatar3.jpg"
                },
                new User
                {
                    UserId = "U20260101000004",
                    PhoneNumber = "13412345678",
                    Nickname = "赵六",
                    Gender = "男",
                    Address = "海珠",
                    Role = "普通用户",
                    Status = "启用",
                    Password = "123456",
                    LoginTime = new DateTime(2026, 1, 1, 14, 0, 0),
                    RegisterTime = new DateTime(2025, 11, 20, 14, 30, 0),
                    WxOpenId = "wx_open_id_004",
                    WxImage = "http://example.com/avatar4.jpg"
                },
                new User
                {
                    UserId = "U20260101000005",
                    PhoneNumber = "17712345678",
                    Nickname = "孙七",
                    Gender = "女",
                    Address = "越秀",
                    Role = "普通用户",
                    Status = "禁用",
                    Password = "123456",
                    LoginTime = null,
                    RegisterTime = new DateTime(2025, 10, 1, 8, 0, 0),
                    WxOpenId = "wx_open_id_005",
                    WxImage = "http://example.com/avatar5.jpg"
                }
            };
        }
    }
}