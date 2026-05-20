using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using WebAPI.Entities.Manage;

namespace WebAPI.Entities.Manage;

[Table("activity")]
public class ActivityEntity
{
    [Key]
    [Column("activity_id")]
    public long ActivityId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("price")]
    public decimal Price { get; set; } // ���ݿ��� decimal(10,2)��������� decimal

    [Column("start_date")]
    public DateTime StartDate { get; set; } // ���ݿ��� datetime

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; } = null!;

    [Column("video_url")]
    public string VideoUrl { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("people")]
    public int? People { get; set; }

    [Column("content", TypeName = "text")]
    public string? Content { get; set; }

    [Column("status_id")]
    public int StatusId { get; set; } // ��Ӧ���ݿ� status_id

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column("type_id")]
    public int TypeId { get; set; }

    [ForeignKey(nameof(TypeId))]
    [InverseProperty("Activities")]
    public virtual ActivityType? Type { get; set; }

    [Column("duration")]
    public int Duration { get; set; } // 活动持续时间，单位为分钟

    [Column("isdelete_id")]
    public byte IsdeleteId { get; set; } // 软删除标志，0表示未删除，1表示已删除

    [InverseProperty("Activity")] // ��Ӧ ActivityMaterial ��� Activity ����
    public virtual ICollection<ActivityMaterial> ActivityMaterials { get; set; } = new List<ActivityMaterial>();

    [InverseProperty("Activity")] // ��Ӧ ActivityOrderDetail ����� Activity ������
    public virtual ICollection<ActivityOrderDetail> ActivityOrderDetails { get; set; } = new List<ActivityOrderDetail>();
}
