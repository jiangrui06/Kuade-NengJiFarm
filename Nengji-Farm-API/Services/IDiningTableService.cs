using WebAPI.Dtos;

namespace WebAPI.Services;

/// <summary>
/// 餐桌管理服务接口
/// 对应两个控制器：TableController (api/table) 和 DiningTableController (api/dining-table)
/// </summary>
public interface IDiningTableService
{
    #region api/dining-table (DiningTableController)

    /// <summary>获取餐桌列表（DiningTableController）</summary>
    Task<(List<DiningTableListItemDto> Records, int Total)> GetListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>新增餐桌（DiningTableController）</summary>
    Task<string> CreateAsync(CreateDiningTableDto dto, CancellationToken cancellationToken = default);

    /// <summary>删除餐桌（DiningTableController）</summary>
    Task<bool> DeleteAsync(string tableNo, CancellationToken cancellationToken = default);

    /// <summary>获取桌台状态列表（从 dining_table_status_dict 表读取）</summary>
    Task<List<DiningTableStatusDto>> GetStatusesAsync(CancellationToken cancellationToken = default);

    #endregion

    #region api/table (TableController)

    /// <summary>获取餐桌列表（支持分页/搜索/状态筛选）</summary>
    Task<(List<TableListItemDto> Records, int Total)> GetTableListAsync(
        int pageNum, int pageSize, string? keyword, string? status, CancellationToken cancellationToken = default);

    /// <summary>获取餐桌详情</summary>
    Task<TableDetailDto?> GetTableDetailAsync(string id, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>新增餐桌（含二维码生成）</summary>
    Task<TableMutationResponseDto> CreateTableAsync(CreateTableRequestDto dto, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>更新餐桌信息（桌号变更时重新生成二维码）</summary>
    Task<TableMutationResponseDto?> UpdateTableAsync(UpdateTableRequestDto dto, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>删除餐桌</summary>
    Task<bool> DeleteTableAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>更新餐桌状态</summary>
    Task<UpdateTableStatusRequestDto?> UpdateTableStatusAsync(UpdateTableStatusRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>重新生成所有餐桌的二维码（修复旧数据路径问题）</summary>
    Task<int> RegenerateAllQrCodesAsync(string baseUrl, CancellationToken cancellationToken = default);

    #endregion
}
