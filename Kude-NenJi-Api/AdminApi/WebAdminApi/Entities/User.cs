using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAdminApi.Entities
{
    /// <summary>
    /// 用户表实体
    /// </summary>
    [Table("user")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("user_guid")]
        public string UserGuid { get; set; } = null!;

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("register_time")]
        public DateTime RegisterTime { get; set; } = DateTime.Now;

        [Column("wx_openid")]
        public string? WxOpenId { get; set; }

        [Column("wx_image")]
        public string? WxImage { get; set; }

        [Column("wx_nickname")]
        public string? WxNickname { get; set; }

        [Column("real_name")]
        public string? RealName { get; set; }

        [Column("password_hash")]
        public string PasswordHash { get; set; } = "";

        [Column("gender")]
        public string? Gender { get; set; } = "保密";

        [Column("role_id")]
        public int RoleId { get; set; }
    }
}

