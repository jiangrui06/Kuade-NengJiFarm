using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities.Manage
{
    [Table("dining_table")]
    public class DiningTables
    {
        [Key]
        [Column("dining_table_id")]
        public long DiningTableId { get; set; }
        [Column("table_no")]
        public string TableNo { get; set; } = string.Empty;

        [Column("seat_count")]
        public int SeatCount { get; set; }

        [Column("table_status_id")]
        public int TableStatus { get; set; }

        [Column("qrcode_image_url")]
        public string? QrCodeImageUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
