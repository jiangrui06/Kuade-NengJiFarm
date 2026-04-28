using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities
{
    [Table("admin")]
    public class Admin
    {
        [Key]
        [Column("admin_id")]
        public int AdminId { get; set; }

        [Column("user_no")]
        public string UserNo { get; set; } = null!;

        [Column("user_password")]
        public string UserPassword { get; set; } = null!;
    }
}
