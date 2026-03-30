using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAdminApi.Entities
{
    [Table("role")]
    public class Roles
    {
        [Key]
        [Column("role_id")]
        public int RoleId { get; set; }
        [Column("role_name")]
        public string RoleName { get; set; } = "Œ¥…Ë÷√";
    }

    [Table("role_staff")]
    public class Role_Staffs
    {
        [Key]
        [Column("role_staff_id")]
        public int RoleStaffId { get; set; }
        [Column("role_staff_name")]
        public string RoleStaffName { get; set; } = "Œ¥…Ë÷√";

    }
}
