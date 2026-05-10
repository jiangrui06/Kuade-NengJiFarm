using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("dining_table")]
public class DiningTable
{
    [Key]
    [Column("dining_table_id")]
    public long DiningTableId { get; set; }

    [Column("table_no")]
    [MaxLength(50)]
    public string TableNo { get; set; } = string.Empty;

    [Column("seat_count")]
    public int SeatCount { get; set; }

    [Column("table_status_id")]
    public int TableStatusId { get; set; }

    [Column("qrcode_image_url")]
    [MaxLength(500)]
    public string? QrcodeImageUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
