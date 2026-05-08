using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;
using WebAPI.Options;

namespace WebAPI.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtHelper _jwtHelper;
    private readonly JwtOptions _jwtOptions;

    public AuthService(AppDbContext dbContext, JwtHelper jwtHelper, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtHelper = jwtHelper;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> LoginByDeviceAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var deviceId = TrimToLength(request.DeviceId, 45);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserNo == deviceId, cancellationToken);

        if (user is null)
        {
            user = await CreateDefaultUserAsync(cancellationToken);
            user.UserNo = deviceId;

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginByWechatAsync(WechatLoginRequest request, CancellationToken cancellationToken = default)
    {
        var openId = BuildWechatOpenId(request);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.WxOpenId == openId, cancellationToken);

        if (user is null)
        {
            user = await CreateDefaultUserAsync(cancellationToken);
            user.UserNo = $"wx_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            user.WxOpenId = openId;
            user.WxName = TrimToLength(request.Nickname, 45);
            user.WxImage = TrimToLength(request.Avatar, 45);

            _dbContext.Users.Add(user);
        }
        else
        {
            user.WxName = string.IsNullOrWhiteSpace(request.Nickname) ? user.WxName : TrimToLength(request.Nickname, 45);
            user.WxImage = string.IsNullOrWhiteSpace(request.Avatar) ? user.WxImage : TrimToLength(request.Avatar, 45);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginByPhoneAsync(int? currentUserId, PhoneLoginRequest request, CancellationToken cancellationToken = default)
    {
        User? user = null;

        if (currentUserId.HasValue)
        {
            user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == currentUserId.Value, cancellationToken);
        }

        if (user is null)
        {
            user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.PhoneNumber == request.Phone, cancellationToken);
        }

        if (user is null)
        {
            user = await CreateDefaultUserAsync(cancellationToken);
            user.UserNo = TrimToLength($"phone_{request.Phone}", 45);
            user.PhoneNumber = TrimToLength(request.Phone, 45);
            _dbContext.Users.Add(user);
        }
        else
        {
            user.PhoneNumber = TrimToLength(request.Phone, 45);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return BuildAuthResponse(user);
    }

    public async Task<AuthUserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Where(x => x.UserId == userId)
            .Select(x => new AuthUserDto
            {
                Id = x.UserId,
                UserNo = x.UserNo,
                Nickname = x.WxName,
                Avatar = x.WxImage,
                Phone = x.PhoneNumber,
                Role = GetRoleString(x.RoleId)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var roleString = GetRoleString(user.RoleId);

        var userDto = new AuthUserDto
        {
            Id = user.UserId,
            UserNo = user.UserNo,
            Nickname = user.WxName,
            Avatar = user.WxImage,
            Phone = user.PhoneNumber,
            Role = roleString
        };



        return new AuthResponse
        {
            Token = _jwtHelper.GenerateToken(user),
            ExpireMinutes = _jwtOptions.ExpireMinutes,
            User = userDto,
            UserInfo = userDto
        };
    }

        private string GetRoleString(int roleId)
    {
        if (roleId <= 0)
            return "user";

        var role = _dbContext.Roles.FirstOrDefault(r => r.RoleId == roleId);

        if (role == null)
            return "user";

        // 根据角色名称判断
        string roleName = role.RoleName.ToLower();
        if (roleName.Contains("staff") || roleName.Contains("员工"))
            return "staff";

        return "user";
    }

    private async Task<User> CreateDefaultUserAsync(CancellationToken cancellationToken)
    {
        var roleId = await _dbContext.Roles
            .OrderBy(x => x.RoleId)
            .Select(x => x.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId <= 0)
        {
            var role = new Role
            {
                RoleName = "user"
            };

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync(cancellationToken);
            roleId = role.RoleId;
        }

        return new User
        {
            PhoneNumber = string.Empty,
            RegisterTime = DateTime.UtcNow,
            WxOpenId = string.Empty,
            WxImage = string.Empty,
            WxName = string.Empty,
            RoleId = roleId
        };
    }

    private static string BuildWechatOpenId(WechatLoginRequest request)
    {
        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(request.EncryptedData) && string.IsNullOrWhiteSpace(request.Iv))
        {
            return TrimToLength(code, 255);
        }

        return TrimToLength($"wx_{code}", 255);
    }

    private static string TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
