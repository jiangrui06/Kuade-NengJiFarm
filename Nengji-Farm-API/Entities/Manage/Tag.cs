using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage;

[Table("tag")]
public class Tag
{
    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }

    [Column("tag_name")]
    [MaxLength(50)]
    public string TagName { get; set; } = string.Empty;
}
