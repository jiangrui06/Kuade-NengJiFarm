using System;

namespace WebApplication1.Models.Entities
{
    public class User
    {
        public string UserId { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Nickname { get; set; } = null!;
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string Role { get; set; } = "普通用户";
        public string Status { get; set; } = "启用";
        public string? Password { get; set; }
        public DateTime? LoginTime { get; set; }
        public DateTime RegisterTime { get; set; } = DateTime.Now;
        public DateTime? UpdateTime { get; set; }
        public string? WxOpenId { get; set; }
        public string? WxImage { get; set; }
    }
}
