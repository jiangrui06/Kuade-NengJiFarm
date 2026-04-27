using System.ComponentModel.DataAnnotations;

namespace WebAPI.Dtos;

public class LoginRequest
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    public string? Platform { get; set; }

    public string? Version { get; set; }
}

public class WechatLoginRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    public string? Nickname { get; set; }

    public string? Avatar { get; set; }

    public string? EncryptedData { get; set; }

    public string? Iv { get; set; }
}

public class WxPhoneLoginRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string PhoneCode { get; set; } = string.Empty;

    public string? Nickname { get; set; }

    public string? Avatar { get; set; }
}

public class PhoneLoginRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    public string? Code { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public long ExpireMinutes { get; set; }

    public AuthUserDto User { get; set; } = new();

    public AuthUserDto UserInfo { get; set; } = new();
}

public class AuthUserDto
{
    public int Id { get; set; }

    public string UserNo { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public string Avatar { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
}
