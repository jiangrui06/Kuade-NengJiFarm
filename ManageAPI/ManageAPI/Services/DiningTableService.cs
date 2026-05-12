using Microsoft.EntityFrameworkCore;

using QRCoder;

using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

public class DiningTableService : IDiningTableService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DiningTableService> _logger;

    public DiningTableService(AppDbContext dbContext, ILogger<DiningTableService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ========== 通用辅助方法 ==========

    /// <summary>获取状态字典 (statusId → statusName)</summary>
    private async Task<Dictionary<int, string>> GetStatusMapAsync(CancellationToken ct)
    {
        return await _dbContext.Set<DiningTableStatusDict>()
            .AsNoTracking()
            .ToDictionaryAsync(s => s.TableStatusId, s => s.StatusName, ct);
    }

    /// <summary>根据状态名称查询 statusId</summary>
    private async Task<int?> ResolveStatusIdAsync(string? statusName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(statusName)) return null;
        return await _dbContext.Set<DiningTableStatusDict>()
            .AsNoTracking()
            .Where(s => s.StatusName == statusName.Trim())
            .Select(s => (int?)s.TableStatusId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>生成餐桌二维码图片，返回可公开访问的 URL</summary>
    private async Task<string> GenerateQrCodeAsync(string tableno, string baseUrl, CancellationToken ct)
    {
        var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var qrDir = Path.Combine(wwwroot, "qrcodes");
        Directory.CreateDirectory(qrDir);

        var contentUrl = $"{baseUrl}/table/{tableno}";
        var fileName = $"{tableno}.png";
        var filePath = Path.Combine(qrDir, fileName);

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(contentUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var bytes = qrCode.GetGraphic(20);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        _logger.LogInformation("二维码已生成: {FilePath}", filePath);
        return $"{baseUrl}/qrcodes/{fileName}";
    }

    // =====================================================
    //  DiningTableController (api/dining-table)
    // =====================================================

    public async Task<(List<DiningTableListItemDto> Records, int Total)> GetListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken ct)
    {
        var query = _dbContext.DiningTables.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(t => t.TableNo.Contains(keyword.Trim()));

        var total = await query.CountAsync(ct);

        var tables = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var statusMap = await GetStatusMapAsync(ct);

        var records = tables.Select(t => new DiningTableListItemDto
        {
            Id = t.TableNo,
            TableNo = t.TableNo,
            SeatCount = t.SeatCount,
            TableStatus = t.TableStatus,
            StatusName = statusMap.GetValueOrDefault(t.TableStatus, "未知"),
            QrCodeImageUrl = t.QrCodeImageUrl,
            CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();

        return (records, total);
    }

    public async Task<string> CreateAsync(CreateDiningTableDto dto, CancellationToken ct)
    {
        var entity = new DiningTables
        {
            TableNo = dto.TableNo,
            SeatCount = dto.SeatCount,
            TableStatus = dto.TableStatus,
            QrCodeImageUrl = dto.QrCodeImageUrl,
            CreatedAt = DateTime.Now,
        };

        _dbContext.DiningTables.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        return entity.TableNo;
    }

    public async Task<bool> DeleteAsync(string tableNo, CancellationToken ct)
    {
        var table = await _dbContext.DiningTables.FirstOrDefaultAsync(t => t.TableNo == tableNo, ct);
        if (table is null) return false;

        _dbContext.DiningTables.Remove(table);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    // =====================================================
    //  TableController (api/table)
    // =====================================================

    public async Task<(List<TableListItemDto> Records, int Total)> GetTableListAsync(
        int pageNum, int pageSize, string? keyword, string? status, CancellationToken ct)
    {
        var query = _dbContext.DiningTables.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(t => t.TableNo.Contains(keyword.Trim()));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusId = await ResolveStatusIdAsync(status, ct);
            if (statusId.HasValue)
                query = query.Where(t => t.TableStatus == statusId.Value);
        }

        var total = await query.CountAsync(ct);

        var tables = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var statusMap = await GetStatusMapAsync(ct);

        var records = tables.Select(t => new TableListItemDto
        {
            Id = t.TableNo,
            Tableno = t.TableNo,
            Capacity = t.SeatCount,
            Status = statusMap.GetValueOrDefault(t.TableStatus, "未知"),
            CreateTime = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();

        return (records, total);
    }

    public async Task<TableDetailDto?> GetTableDetailAsync(string id, CancellationToken ct)
    {
        var table = await _dbContext.DiningTables
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TableNo == id, ct);

        if (table is null) return null;

        return new TableDetailDto
        {
            Id = table.TableNo,
            Tableno = table.TableNo,
            Capacity = table.SeatCount,
            Status = table.TableStatus,
            CreateTime = table.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            QrCodeUrl = table.QrCodeImageUrl,
        };
    }

    public async Task<TableMutationResponseDto> CreateTableAsync(
        CreateTableRequestDto dto, string baseUrl, CancellationToken ct)
    {
        // 检查餐桌号是否已存在
        var exists = await _dbContext.DiningTables.AnyAsync(t => t.TableNo == dto.Tableno, ct);
        if (exists)
            throw new InvalidOperationException($"餐桌号 '{dto.Tableno}' 已存在");

        if (dto.Capacity < 1 || dto.Capacity > 30)
            throw new ArgumentException("容纳人数必须在 1-30 之间");

        // 生成二维码
        var qrUrl = await GenerateQrCodeAsync(dto.Tableno, baseUrl, ct);

        var status = dto.Status ?? 1;

        var entity = new DiningTables
        {
            TableNo = dto.Tableno,
            SeatCount = dto.Capacity,
            TableStatus = status,
            QrCodeImageUrl = qrUrl,
            CreatedAt = DateTime.Now,
        };

        _dbContext.DiningTables.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("新增餐桌成功: {Tableno}, 容量: {Capacity}", dto.Tableno, dto.Capacity);

        return new TableMutationResponseDto
        {
            Id = dto.Tableno,
            Tableno = dto.Tableno,
            Capacity = dto.Capacity,
            Status = status,
            QrCodeUrl = qrUrl,
        };
    }

    public async Task<TableMutationResponseDto?> UpdateTableAsync(
        UpdateTableRequestDto dto, string baseUrl, CancellationToken ct)
    {
        var table = await _dbContext.DiningTables.FirstOrDefaultAsync(t => t.TableNo == dto.Id, ct);
        if (table is null) return null;

        // 如果餐桌号变更，检查新号是否被占用
        if (!string.IsNullOrWhiteSpace(dto.Tableno) && dto.Tableno != dto.Id)
        {
            var exists = await _dbContext.DiningTables.AnyAsync(t => t.TableNo == dto.Tableno, ct);
            if (exists)
                throw new InvalidOperationException($"餐桌号 '{dto.Tableno}' 已存在");
        }

        // 更新字段
        var newTableno = dto.Tableno ?? table.TableNo;
        var newCapacity = dto.Capacity ?? table.SeatCount;
        var newStatus = dto.Status ?? table.TableStatus;

        // 如果餐桌号变了，重新生成二维码
        if (dto.Tableno != null && dto.Tableno != table.TableNo)
        {
            table.QrCodeImageUrl = await GenerateQrCodeAsync(dto.Tableno, baseUrl, ct);
        }

        table.TableNo = newTableno;
        table.SeatCount = newCapacity;
        table.TableStatus = newStatus;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("更新餐桌成功: {OldId} → {NewTableno}", dto.Id, newTableno);

        return new TableMutationResponseDto
        {
            Id = newTableno,
            Tableno = newTableno,
            Capacity = newCapacity,
            Status = newStatus,
            QrCodeUrl = table.QrCodeImageUrl,
        };
    }

    public async Task<bool> DeleteTableAsync(string id, CancellationToken ct)
    {
        var table = await _dbContext.DiningTables.FirstOrDefaultAsync(t => t.TableNo == id, ct);
        if (table is null) return false;

        _dbContext.DiningTables.Remove(table);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<UpdateTableStatusRequestDto?> UpdateTableStatusAsync(
        UpdateTableStatusRequestDto dto, CancellationToken ct)
    {
        var table = await _dbContext.DiningTables.FirstOrDefaultAsync(t => t.TableNo == dto.Tableno, ct);
        if (table is null) return null;

        table.TableStatus = dto.Status;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("更新餐桌状态: {Tableno} → Status={Status}", dto.Tableno, dto.Status);

        return dto;
    }
}
