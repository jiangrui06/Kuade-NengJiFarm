using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using WebAPI.Entities.Entities;

namespace WebAPI.Entities;

[Table("activity")]
public class ActivityEntity
{
    [Key]
    [Column("activity_id")]
    public long ActivityId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("price")]
    public decimal Price { get; set; } // 数据库是 decimal(10,2)，这里改用 decimal

    [Column("start_date")]
    public DateTime StartDate { get; set; } // 数据库是 datetime

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; } = null!;

    [Column("status_id")]
    public int StatusId { get; set; } // 对应数据库 status_id

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("type_id")]
    public int TypeId { get; set; }

    [InverseProperty("Activity")] // 对应 ActivityMaterial 里的 Activity 属性
    public virtual ICollection<ActivityMaterial> ActivityMaterials { get; set; } = new List<ActivityMaterial>();

    [InverseProperty("Activity")] // 对应 ActivityOrderDetail 类里的 Activity 属性名
    public virtual ICollection<ActivityOrderDetail> ActivityOrderDetails { get; set; } = new List<ActivityOrderDetail>();
}
