using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace WebAPI.Entities
{
    [Table("dish_orders")]
    public class DishOrders
    {
        [Key]
        [Column("order_id")]
        public long OrderId { get; set; }
        [Column("order_no")]
        public string OrderNo { get; set; } = string.Empty;
        [Column("wx_pay_no")]
        public string? WxPayNo { get; set; } = string.Empty;

        [Column("total_amount")]
        [Precision(10, 2)]

        public decimal TotalAmount { get; set; }
        [Column("total_quantity")]

        public int TotalQuantity { get; set; }

        [Column("order_status_id")]
        public int OrderStatusId { get; set; }
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("create_time")]
        public DateTime CreateTime { get; set; }
        
        [Column("dining_table_id")]
        public long DiningTableId { get; set; }

        [Column("remark")]
        public string? Remark { get; set; } = string.Empty;
    }
}
