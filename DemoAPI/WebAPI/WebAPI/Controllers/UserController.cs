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
    private readonly AppDbContext _dbContext;

    public UserController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return Ok(ApiResult.Fail("用户不存在", 404));
            }

            return Ok(ApiResult.Success(new UserProfileResponse
            {
                Id = user.UserId,
                Nickname = user.WxName,
                Avatar = user.WxImage,
                Phone = user.PhoneNumber,
                Email = string.Empty
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

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == GetCurrentUserId(), cancellationToken);

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
                user.WxImage = request.Avatar.Trim();
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
    public async Task<IActionResult> Address(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userPhone = await _dbContext.Users
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PhoneNumber)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var addresses = await _dbContext.ShippingAddresses
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.AddressId)
                .ToListAsync(cancellationToken);

            var data = addresses.Select((x, index) => new AddressResponse
            {
                Id = x.AddressId,
                Name = x.ContactName,
                Phone = userPhone,
                Province = x.Province,
                City = x.City,
                District = x.MunicipalDistrict,
                Address = $"{x.Town}{x.HouseNumber}",
                IsDefault = index == 0
            }).ToList();

            return Ok(ApiResult.Success(data));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取收货地址失败：{ex.Message}"));
        }
    }

    [HttpPost("address")]
    public async Task<IActionResult> CreateAddress([FromBody] SaveAddressRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsValidAddressRequest(request, false))
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var (town, houseNumber) = SplitAddress(request!.Address);
            var address = new ShippingAddress
            {
                UserId = GetCurrentUserId(),
                ContactName = request.Name.Trim(),
                Province = request.Province.Trim(),
                City = request.City.Trim(),
                MunicipalDistrict = request.District.Trim(),
                Town = town,
                HouseNumber = houseNumber
            };

            _dbContext.ShippingAddresses.Add(address);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"新增收货地址失败：{ex.Message}"));
        }
    }

    [HttpPut("address")]
    public async Task<IActionResult> UpdateAddress([FromBody] SaveAddressRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsValidAddressRequest(request, true))
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var updateRequest = request!;
            var address = await _dbContext.ShippingAddresses
                .FirstOrDefaultAsync(x => x.AddressId == updateRequest.Id && x.UserId == GetCurrentUserId(), cancellationToken);

            if (address is null)
            {
                return Ok(ApiResult.Fail("地址不存在", 404));
            }

            var (town, houseNumber) = SplitAddress(updateRequest.Address);
            address.ContactName = updateRequest.Name.Trim();
            address.Province = updateRequest.Province.Trim();
            address.City = updateRequest.City.Trim();
            address.MunicipalDistrict = updateRequest.District.Trim();
            address.Town = town;
            address.HouseNumber = houseNumber;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"更新收货地址失败：{ex.Message}"));
        }
    }

    [HttpDelete("address")]
    public async Task<IActionResult> DeleteAddress([FromBody] DeleteAddressRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.Id <= 0)
            {
                return Ok(ApiResult.Fail("请求参数不正确", 400));
            }

            var address = await _dbContext.ShippingAddresses
                .FirstOrDefaultAsync(x => x.AddressId == request.Id && x.UserId == GetCurrentUserId(), cancellationToken);

            if (address is null)
            {
                return Ok(ApiResult.Fail("地址不存在", 404));
            }

            _dbContext.ShippingAddresses.Remove(address);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ApiResult.Success());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除收货地址失败：{ex.Message}"));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("userId");
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("未授权，请重新登录");
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
               && !string.IsNullOrWhiteSpace(request.Province)
               && !string.IsNullOrWhiteSpace(request.City)
               && !string.IsNullOrWhiteSpace(request.District)
               && !string.IsNullOrWhiteSpace(request.Address);
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

    public sealed class UpdateUserProfileRequest
    {
        public string Nickname { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
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
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
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
        public bool IsDefault { get; set; }
    }
}
