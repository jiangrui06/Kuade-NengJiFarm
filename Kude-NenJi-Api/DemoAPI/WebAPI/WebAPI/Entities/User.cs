using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("user")]
public class User
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("user_guid")]
    public string UserNo { get; set; } = string.Empty;

    [Column("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Column("register_time")]
    public DateTime? RegisterTime { get; set; }

    [Column("wx_openid")]
    public string WxOpenId { get; set; } = string.Empty;

    [Column("wx_image")]
    public string WxImage { get; set; } = string.Empty;

    [Column("wx_nickname")]
    public string WxName { get; set; } = string.Empty;

    [Column("real_name")]
    public string RealName { get; set; } = string.Empty;

    [Column("gender")]
    public string Gender { get; set; } = "保密";

    [Column("role_id")]
    public int RoleId { get; set; }

    public Role? Role { get; set; }
}
