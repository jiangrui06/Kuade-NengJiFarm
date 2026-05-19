using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManageAPI.Entity
{
    [Table("commodity_material")]
    public class CommodityMaterial
    {
        [Key]
        [Column("material_id")]
        public long MaterialId { get; set; }

        [Column("commodity_id")]
        public int CommodityId { get; set; }

        /// <summary>
        /// 素材类型：0=轮播图, 1=详情图/规格图, 2=视频
        /// </summary>
        [Column("material_type")]
        public int MaterialType { get; set; }

        [Column("material_url")]
        public string MaterialUrl { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
