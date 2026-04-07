using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAdminApi.Entities
{
    [Table("admin_staff")]
    public class AdminStaffs
    {
        [Key]
        public int id { get; set; }

        [Column("admin_id")]
        public string AdminId { get; set; } = "";
        
        [Column("phone")]
        public string Phone { get; set; } = "未设置";
        
        [Column("nickname")]
        public string NickName { get; set; } = "";
        
        [Column("gender")]
        public string Gender { get; set; } = "保密";
        
        [Column("address")]
        public string Address { get; set; } = "未设置";
        
        [Column("role_id")]    
        public int Role { get; set; }
        
        [Column("status")]
        public string Status { get; set; } = "启用";
        
        [Column("register_time")]
        public DateTime RegisterTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 登录密码（默认：123456）
        /// </summary>
        [Column("password")]
        public string Password { get; set; } = "123456";
        
        /// <summary>
        /// 最后登录时间
        /// </summary>
        [Column("login_time")]
        public DateTime? LoginTime { get; set; }
    }

    [Table("users")]
    public class WeChatUser
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("phone_number")]
        public string PhoneNumber { get; set; } = "未设置";
        [Column("register_time")]
        public DateTime RegisterTime { get; set; } = DateTime.Now;
        [Column("wx_open_id")]
        public string? WxOpenId { get; set; } = "未设置";
        [Column("wx_image")]
        public string WxImage { get; set; } = "未设置";
        [Column("wx_name")]
        public string WxName { get; set; } = "未设置";
        [Column("RoleId")]
        public int RoleId { get; set; }
    }
}
