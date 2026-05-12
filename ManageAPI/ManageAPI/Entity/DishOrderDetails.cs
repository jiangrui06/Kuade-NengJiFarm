using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace ManageAPI.Entity
{
    [Table("dish_order_details")]
    public class DishOrderDetails
    {
        [Key]
        [Column("dish_order_details_id")]
        public long DishOrderDetailsId { get; set; }
        [Column("dish_order_id")]
        public long DishOrderId { get; set; }
        [Column("dish_id")]
        public int DishId { get; set; }

        [Column("goods_name")]
        [MaxLength(100)]
        public string GoodsName { get; set; } = string.Empty;

        [Column("image_url")]
        [MaxLength(255)]
        public string? ImageUrl { get; set; }

        [Column("unit_price")]
        [Precision(10, 2)]
        public decimal UnitPrice { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("subtotal_amount")]
        [Precision(10, 2)]
        public decimal SubtotalAmount { get; set; }

        [Column("status_id")]
        public int StatusId { get; set; }
    }
}
