using System.ComponentModel.DataAnnotations;

namespace WebAPI.Dtos;

/// <summary>
/// 新增/编辑券品
/// </summary>
public class CreateCouponDto
{
    [Required(ErrorMessage = "券品名称不能为空")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "券品类型不能为空")]
    public string Type { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "售价必须大于0")]
    public decimal Price { get; set; }

    [Range(1, 10000, ErrorMessage = "库存必须大于0")]
    public int Stock { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "单次限购数量必须大于等于1")]
    public int LimitPerOrder { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "有效期必须大于等于1")]
    public int ValidityPeriod { get; set; }

    [Required(ErrorMessage = "有效期单位不能为空")]
    public string ValidityUnit { get; set; } = "天";

    [Required(ErrorMessage = "退款规则不能为空")]
    public string RefundRule { get; set; } = string.Empty;

    [Required(ErrorMessage = "使用规则不能为空")]
    public string UsageRules { get; set; } = string.Empty;

    public string? Image { get; set; }

    public List<CarouselMediaDto> CarouselMedia { get; set; } = [];
}

/// <summary>
/// 编辑券品
/// </summary>
public class UpdateCouponDto : CreateCouponDto
{
    [Range(1, long.MaxValue, ErrorMessage = "券品ID不能为空")]
    public long Id { get; set; }
}