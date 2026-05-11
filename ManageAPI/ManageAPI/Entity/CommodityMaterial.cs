using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities
{
    [Table("commodity_material")]
    public class CommodityMaterial
    {
        [Key]
        [Column("material_id")]
        public long MaterialId { get; set; }

        [Column("commodity_id")]
        public int CommodityId { get; set; }

        [Column("material_type")]
        public string MaterialType { get; set; } = string.Empty;

        [Column("material_url")]
        public string MaterialUrl { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
