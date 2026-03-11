using System;

namespace WebApplication1.Models.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public string UserNo { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public DateTime? RegisterTime { get; set; }
        public string? WxOpenId { get; set; }
        public string? WxImage { get; set; }
        public string? WxNickname { get; set; }
        public int RoleId { get; set; }
    }
}
