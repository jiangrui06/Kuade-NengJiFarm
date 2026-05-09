namespace WebAPI.Services;

using WebAPI.Dtos;

/// <summary>
/// 产品管理服务接口
/// </summary>
public interface IProductService
{
    /// <summary>
    /// 获取产品列表
    /// </summary>
    Task<(List<ProductListItemDto> Records, int Total)> GetProductListAsync(
        int pageNum,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取产品详情
    /// </summary>
    Task<ProductDetailDto?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增产品
    /// </summary>
    Task<int> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 编辑产品
    /// </summary>
    Task<bool> UpdateProductAsync(UpdateProductDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除产品
    /// </summary>
    Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除产品
    /// </summary>
    Task<bool> DeleteProductBatchAsync(int[] ids, CancellationToken cancellationToken = default);
}