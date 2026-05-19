using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/user")]
public class UserController : ControllerBase
{
    private const string DefaultFlagProperty = "IsDefault";
    private readonly AppDbContext _dbContext;

    public UserController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("profile-preview")]
    public IActionResult ProfilePreview()
    {
        return Ok(ApiResult.Success(new UserProfileResponse
        {
            Id = 0,
            Nickname = "游客",
            Avatar = string.Empty,
            Gender = "保密",
            Phone = string.Empty,
            Email = string.Empty
        }));
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        try
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            return Ok(ApiResult.Success(new UserProfileResponse
            {
                Id = user.UserId,
                Nickname = user.WxName,
                Avatar = user.WxImage,
                Gender = user.Gender,
                Phone = user.PhoneNumber,
                Email = string.Empty,
                Balance = 0,
                Reward = 0,
                Role = await ResolveUserRoleAsync(user.RoleId, cancellationToken)
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取个人信息失败：{ex.Message}"));
        }
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            if (!string.IsNullOrWhiteSpace(request.Nickname))
            {
                user.WxName = request.Nickname.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Avatar))
            {
                if (IsTemporaryAvatarUrl(request.Avatar))
                {
                    return Ok(ApiResult.Fail("头像请先上传后再保存", 400));
                }

                user.WxImage = request.Avatar.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Gender))
            {
                user.Gender = request.Gender.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                user.PhoneNumber = request.Phone.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var roleId = await ResolveRequestedRoleIdAsync(request.Role, cancellationToken);
                if (roleId <= 0)
                {
                    return Ok(ApiResult.Fail("role is invalid", 400));
                }

                user.RoleId = roleId;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"更新个人信息失败：{ex.Message}"));
        }
    }

    [HttpGet("address")]
    [HttpGet("/api/address/list")]
    public async Task<IActionResult> Address(CancellationToken cancellationToken)
    {
        try
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            var data = await _dbContext.ShippingAddresses
                .AsNoTracking()
                .Where(x => x.UserId == user.UserId)
                .OrderByDescending(x => EF.Property<bool>(x, DefaultFlagProperty))
                .ThenByDescending(x => x.AddressId)
                .Select(x => new AddressResponse
                {
                    Id = x.AddressId,
                    Name = x.ContactName,
                    Phone = x.ContactPhone,
                    Province = x.Province,
                    City = x.City,
                    District = x.MunicipalDistrict,
                    Address = x.Addres,
                    Detail = x.Detail,
                    FullAddress = $"{x.Province}{x.City}{x.MunicipalDistrict}{x.Addres}",
                    IsDefault = EF.Property<bool>(x, DefaultFlagProperty)
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取收货地址失败：{ex.Message}"));
        }
    }

    [HttpGet("/api/address/{id:int}")]
    public async Task<IActionResult> GetAddressById(int id, CancellationToken cancellationToken)
    {
        try
        {
            if (id <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            var data = await _dbContext.ShippingAddresses
                .AsNoTracking()
                .Where(x => x.AddressId == id && x.UserId == user.UserId)
                .Select(x => new AddressResponse
                {
                    Id = x.AddressId,
                    Name = x.ContactName,
                    Phone = x.ContactPhone,
                    Province = x.Province,
                    City = x.City,
                    District = x.MunicipalDistrict,
                    Address = x.Addres,
                    Detail = x.Detail,
                    FullAddress = $"{x.Province}{x.City}{x.MunicipalDistrict}{x.Addres}",
                    IsDefault = EF.Property<bool>(x, DefaultFlagProperty)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (data is null)
            {
                return Ok(ApiResult.Fail("地址不存在", 404));
            }

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取地址详情失败：{ex.Message}"));
        }
    }

    [HttpPost("address")]
    [HttpPost("/api/address")]
    public async Task<IActionResult> CreateAddress([FromBody] SaveAddressRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsValidAddressRequest(request, false))
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            var hasAnyAddress = await _dbContext.ShippingAddresses
                .AsNoTracking()
                .AnyAsync(x => x.UserId == user.UserId, cancellationToken);

            var shouldSetDefault = request!.IsDefault || !hasAnyAddress;
            if (shouldSetDefault)
            {
                await ClearDefaultAddressesAsync(user.UserId, cancellationToken);
            }

            var (town, houseNumber) = SplitAddress(request.Address);
            var address = new ShippingAddress
            {
                UserId = user.UserId,
                ContactName = request.Name.Trim(),
                ContactPhone = request.Phone.Trim(),
                Province = request.Province.Trim(),
                City = request.City.Trim(),
                MunicipalDistrict = request.District.Trim(),
                Addres = request.Address.Trim(),
                Detail = request.Detail.Trim(),
                Town = town,
                HouseNumber = houseNumber
            };

            _dbContext.ShippingAddresses.Add(address);
            SetDefaultFlag(address, shouldSetDefault);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success(new { id = address.AddressId }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"新增收货地址失败：{ex.Message}"));
        }
    }

    [HttpPut("address")]
    public Task<IActionResult> UpdateAddress([FromBody] SaveAddressRequest? request, CancellationToken cancellationToken)
    {
        return UpdateAddressCore(request, null, cancellationToken);
    }

    [HttpPut("/api/address/{id:int}")]
    [HttpPut("address/{id:int}")]
    public Task<IActionResult> UpdateAddressById(int id, [FromBody] SaveAddressRequest? request, CancellationToken cancellationToken)
    {
        request ??= new SaveAddressRequest();
        request.Id = id;
        return UpdateAddressCore(request, id, cancellationToken);
    }

    [HttpDelete("address")]
    public Task<IActionResult> DeleteAddress([FromBody] DeleteAddressRequest? request, CancellationToken cancellationToken)
    {
        return DeleteAddressCore(request?.Id ?? 0, cancellationToken);
    }

    [HttpDelete("/api/address/{id:int}")]
    [HttpDelete("address/{id:int}")]
    public Task<IActionResult> DeleteAddressById(int id, CancellationToken cancellationToken)
    {
        return DeleteAddressCore(id, cancellationToken);
    }

    private async Task<IActionResult> UpdateAddressCore(SaveAddressRequest? request, int? routeId, CancellationToken cancellationToken)
    {
        try
        {
            if (routeId.HasValue && routeId.Value > 0)
            {
                request ??= new SaveAddressRequest();
                request.Id = routeId;
            }

            if (!IsValidAddressRequest(request, true))
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            var updateRequest = request!;
            var address = await _dbContext.ShippingAddresses
                .FirstOrDefaultAsync(x => x.AddressId == updateRequest.Id && x.UserId == user.UserId, cancellationToken);

            if (address is null)
            {
                return Ok(ApiResult.Fail("地址不存在", 404));
            }

            var (town, houseNumber) = SplitAddress(updateRequest.Address);
            address.ContactName = updateRequest.Name.Trim();
            address.ContactPhone = updateRequest.Phone.Trim();
            address.Province = updateRequest.Province.Trim();
            address.City = updateRequest.City.Trim();
            address.MunicipalDistrict = updateRequest.District.Trim();
            address.Addres = updateRequest.Address.Trim();
            address.Detail = updateRequest.Detail.Trim();
            address.Town = town;
            address.HouseNumber = houseNumber;
            SetDefaultFlag(address, updateRequest.IsDefault);

            if (updateRequest.IsDefault)
            {
                await ClearDefaultAddressesAsync(user.UserId, cancellationToken, address.AddressId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await EnsureDefaultAddressExistsAsync(user.UserId, cancellationToken);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"更新收货地址失败：{ex.Message}"));
        }
    }

    private async Task<IActionResult> DeleteAddressCore(int id, CancellationToken cancellationToken)
    {
        try
        {
            if (id <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            var address = await _dbContext.ShippingAddresses
                .FirstOrDefaultAsync(x => x.AddressId == id && x.UserId == user.UserId, cancellationToken);

            if (address is null)
            {
                return Ok(ApiResult.Fail("地址不存在", 404));
            }

            _dbContext.ShippingAddresses.Remove(address);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await EnsureDefaultAddressExistsAsync(user.UserId, cancellationToken);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除收货地址失败：{ex.Message}"));
        }
    }

    private async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId > 0)
        {
            var userById = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (userById is not null)
            {
                return userById;
            }
        }

        var userGuid = GetCurrentUserGuid();
        if (string.IsNullOrWhiteSpace(userGuid))
        {
            return null;
        }

        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserNo == userGuid, cancellationToken);
    }

    private async Task<string> ResolveUserRoleAsync(int roleId, CancellationToken cancellationToken)
    {
        if (roleId <= 0)
        {
            return "user";
        }

        var roleName = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.RoleName)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return "user";
        }

        return roleName.Trim().Equals("staff", StringComparison.OrdinalIgnoreCase) ||
               roleName.Contains("员工", StringComparison.OrdinalIgnoreCase)
            ? "staff"
            : "user";
    }

    private async Task<int> ResolveRequestedRoleIdAsync(string role, CancellationToken cancellationToken)
    {
        var normalizedRole = NormalizeRequestedRole(role);
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return 0;
        }

        var roleId = await _dbContext.Roles
            .AsNoTracking()
            .Where(x =>
                normalizedRole == "staff"
                    ? x.RoleName == "staff" || x.RoleName == "员工"
                    : normalizedRole == "admin"
                        ? x.RoleName == "admin" || x.RoleName == "管理员"
                        : x.RoleName == "user" || x.RoleName == "普通用户")
            .OrderBy(x => x.RoleId)
            .Select(x => x.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        return roleId;
    }

    private static string NormalizeRequestedRole(string role)
    {
        var value = role.Trim().ToLowerInvariant();
        return value switch
        {
            "user" => "user",
            "普通用户" => "user",
            "staff" => "staff",
            "员工" => "staff",
            "admin" => "admin",
            "管理员" => "admin",
            _ => string.Empty
        };
    }

    private int GetCurrentUserId()
    {
        var userIdValue = (User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? string.Empty).Trim();

        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }

    private string GetCurrentUserGuid()
    {
        return (User.FindFirstValue("user_guid")
            ?? User.FindFirstValue("userNo")
            ?? User.FindFirstValue("userGuid")
            ?? string.Empty).Trim();
    }

    private static bool IsValidAddressRequest(SaveAddressRequest? request, bool requireId)
    {
        if (request is null)
        {
            return false;
        }

        if (requireId && request.Id <= 0)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(request.Name)
               && !string.IsNullOrWhiteSpace(request.Phone)
               && IsValidPhone(request.Phone)
               && !string.IsNullOrWhiteSpace(request.Province)
               && !string.IsNullOrWhiteSpace(request.City)
               && !string.IsNullOrWhiteSpace(request.Address);
    }

    private static bool IsValidPhone(string phone)
    {
        var cleaned = phone.Trim()
            .Replace("+86", "")
            .Replace("-", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "");
        return cleaned.Length == 11 && cleaned.All(char.IsDigit) && cleaned.StartsWith("1");
    }

    private static bool IsTemporaryAvatarUrl(string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return false;
        }

        return avatarUrl.StartsWith("wxfile://", StringComparison.OrdinalIgnoreCase)
               || avatarUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
               || avatarUrl.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
               || avatarUrl.StartsWith("http://tmp/", StringComparison.OrdinalIgnoreCase)
               || avatarUrl.StartsWith("https://tmp/", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Town, string HouseNumber) SplitAddress(string address)
    {
        address = address.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            return (string.Empty, string.Empty);
        }

        return address.Length <= 50
            ? (string.Empty, address)
            : (address[..50], address[50..]);
    }

    private async Task ClearDefaultAddressesAsync(int userId, CancellationToken cancellationToken, int? keepAddressId = null)
    {
        var query = _dbContext.ShippingAddresses
            .Where(x => x.UserId == userId && EF.Property<bool>(x, DefaultFlagProperty));

        if (keepAddressId.HasValue)
        {
            query = query.Where(x => x.AddressId != keepAddressId.Value);
        }

        var defaultAddresses = await query.ToListAsync(cancellationToken);
        if (defaultAddresses.Count == 0)
        {
            return;
        }

        foreach (var item in defaultAddresses)
        {
            SetDefaultFlag(item, false);
        }
    }

    private async Task EnsureDefaultAddressExistsAsync(int userId, CancellationToken cancellationToken)
    {
        var addresses = await _dbContext.ShippingAddresses
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AddressId)
            .ToListAsync(cancellationToken);

        if (addresses.Count == 0 || addresses.Any(GetDefaultFlag))
        {
            return;
        }

        SetDefaultFlag(addresses[0], true);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private bool GetDefaultFlag(ShippingAddress address)
    {
        return _dbContext.Entry(address).Property<bool>(DefaultFlagProperty).CurrentValue;
    }

    private void SetDefaultFlag(ShippingAddress address, bool value)
    {
        _dbContext.Entry(address).Property<bool>(DefaultFlagProperty).CurrentValue = value;
    }

    public sealed class UpdateUserProfileRequest
    {
        public string Nickname { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public sealed class SaveAddressRequest
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public sealed class DeleteAddressRequest
    {
        public int Id { get; set; }
    }

    private sealed class UserProfileResponse
    {
        public int Id { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public int Reward { get; set; }
        public string Role { get; set; } = "user";
    }

    private sealed class AddressResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string FullAddress { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
}
