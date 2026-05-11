using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities
{
    [Table("dish_order_status")]
    public class DishOrderStatus
    {
        [Key]
        [Column("order_status_id")]
        public int OrderStatusId { get; set; }
        [Column("status_name")]
        public string StatusName { get; set; } = string.Empty;
    }
}
