using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Entities;

[Table("shipping_address")]
public class ShippingAddress
{
    [Key]
    [Column("address_id")]
    public int AddressId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("contact_name")]
    [MaxLength(50)]
    public string ContactName { get; set; } = string.Empty;

    [Column("contact_phone")]
    [MaxLength(20)]
    public string ContactPhone { get; set; } = string.Empty;

    [Column("province")]
    [MaxLength(50)]
    public string Province { get; set; } = string.Empty;

    [Column("city")]
    [MaxLength(50)]
    public string City { get; set; } = string.Empty;

    [Column("municipal_district")]
    [MaxLength(50)]
    public string MunicipalDistrict { get; set; } = string.Empty;

    [Column("addres")]
    [MaxLength(255)]
    public string Addres { get; set; } = string.Empty;

    [NotMapped]
    public string Town { get; set; } = string.Empty;

    [NotMapped]
    public string HouseNumber { get; set; } = string.Empty;

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("detail")]
    [MaxLength(500)]
    public string Detail { get; set; } = string.Empty;
}
