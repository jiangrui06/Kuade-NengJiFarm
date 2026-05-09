using System.ComponentModel.DataAnnotations;

namespace WebAPI.Dtos;

/// <summary>
/// 编辑产品 - 对应 /api/product/edit
/// </summary>
public class UpdateProductDto : CreateProductDto
{
    /// <summary>
    /// 产品ID (必传)
    /// </summary>
    [Required(ErrorMessage = "产品ID不能为空")]
    public int Id { get; set; }
}