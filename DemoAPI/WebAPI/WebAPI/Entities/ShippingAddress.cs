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

    [Column("content_name")]
    [MaxLength(50)]
    public string ContactName { get; set; } = string.Empty;

    [Column("province")]
    [MaxLength(50)]
    public string Province { get; set; } = string.Empty;

    [Column("city")]
    [MaxLength(50)]
    public string City { get; set; } = string.Empty;

    [Column("municipal_districts")]
    [MaxLength(50)]
    public string MunicipalDistrict { get; set; } = string.Empty;

    [Column("town")]
    [MaxLength(50)]
    public string Town { get; set; } = string.Empty;

    [Column("house_number")]
    [MaxLength(50)]
    public string HouseNumber { get; set; } = string.Empty;
}
