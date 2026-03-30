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
        public int user_id { get; set; }

        public string phone_number { get; set; } = "未设置";
        public DateTime register_time { get; set; } = DateTime.Now;
        public string wx_open_id { get; set; } = "未设置";
        public string wx_image { get; set; } = "未设置";   
        public string wx_name { get; set; } = "未设置";
        public int role_id { get; set; }
    }
}
