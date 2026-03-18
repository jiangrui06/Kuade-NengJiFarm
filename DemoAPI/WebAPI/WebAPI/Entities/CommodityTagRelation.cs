using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("commodity_tag_relation")]
public class CommodityTagRelation
{
    [Key]
    [Column("id")]
    public int CommodityTagRelationId { get; set; }

    [Column("commodity_id")]
    public int CommodityId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }
}
