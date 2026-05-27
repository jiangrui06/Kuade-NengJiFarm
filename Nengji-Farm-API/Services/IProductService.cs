using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

public interface IProductService
{
    Task<(List<ProductListItemDto> Records, int Total)> GetProductListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    Task<ProductDetailDto?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default);

    Task<bool> UpdateProductAsync(UpdateProductDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> DeleteProductBatchAsync(int[] ids, CancellationToken cancellationToken = default);

    /// <summary>获取商品分类列表</summary>
    Task<List<CommodityCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>获取商品状态列表</summary>
    Task<List<CommodityStatus>> GetStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>获取已启用的单位列表</summary>
    Task<List<Unit>> GetUnitsAsync(CancellationToken cancellationToken = default);

    /// <summary>获取产品管理统计数据</summary>
    Task<ProductStatsDto> GetProductStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>获取重量标签下拉列表（从现有商品 weight_text 提取去重）</summary>
    Task<List<WeightTagOptionDto>> GetWeightTagOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>根据状态名称映射到状态ID（从 commodity_status 表动态读取）</summary>
    Task<int> MapStatusToIdAsync(string status, CancellationToken cancellationToken = default);
}
