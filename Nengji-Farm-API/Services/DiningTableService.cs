using Microsoft.EntityFrameworkCore;

using QRCoder;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

public class DiningTableService : IDiningTableService
{
    private readonly ManageAppDbContext _dbContext;
    private readonly ILogger<DiningTableService> _logger;

    public DiningTableService(ManageAppDbContext dbContext, ILogger<DiningTableService> logger)
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

    /// <summary>构建完整二维码访问URL（兼容旧数据中的完整URL和新数据中的相对路径）</summary>
    private static string BuildQrCodeFullUrl(string storedUrl, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(storedUrl))
            return string.Empty;

        if (storedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return storedUrl;

        return $"{baseUrl.TrimEnd('/')}{storedUrl}";
    }

    /// <summary>生成餐桌二维码图片，返回可公开访问的 URL</summary>
    private async Task<string> GenerateQrCodeAsync(string tableno, string baseUrl, CancellationToken ct)
    {
        var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var qrDir = Path.Combine(wwwroot, "images", "qrcode");
        Directory.CreateDirectory(qrDir);

        // 从 "X号桌" 格式中提取数字部分，用于二维码标识
        var raw = tableno.Trim();
        if (raw.EndsWith("号桌", StringComparison.Ordinal) && int.TryParse(raw[..^2], out var no))
        {
            raw = no.ToString();
        }

        // 统一格式化：纯数字补零为 a001 / a012 格式
        string formattedNo;
        if (int.TryParse(raw, out int num))
        {
            formattedNo = $"a{num:D3}";
        }
        else
        {
            formattedNo = raw.ToLower();
        }

        var contentUrl = $"weixin://dl/business/?appid=wx986e22f241e13ba2&path=subpkg/order/order&query=tableId={formattedNo}&secret=^mFIT!xzJ@j55QN%R^4yZ0vx";

        // 使用格式化后的名称作为文件名
        var fileName = $"table_{formattedNo}.png";
        var filePath = Path.Combine(qrDir, fileName);

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(contentUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var bytes = qrCode.GetGraphic(20);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        _logger.LogInformation("二维码已生成: {FilePath}, 内容: {Content}", filePath, contentUrl);
        return $"/images/qrcode/{fileName}";
    }

    // =====================================================
    //  DiningTableController (api/dining-table)
    // =====================================================

    public async Task<(List<DiningTableListItemDto> Records, int Total)> GetListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken ct)
    {
        var query = _dbContext.DiningTables
            .AsNoTracking();

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
        var existing = await _dbContext.DiningTables
            .FirstOrDefaultAsync(t => t.TableNo == dto.TableNo, ct);

        if (existing != null)
        {
            var disabledId = await ResolveStatusIdAsync("停用", ct);
            if (disabledId.HasValue && existing.TableStatus == disabledId.Value)
            {
                existing.SeatCount = dto.SeatCount;
                existing.TableStatus = dto.TableStatus;
                existing.QrCodeImageUrl = dto.QrCodeImageUrl;
                existing.CreatedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync(ct);
                return existing.TableNo;
            }

            throw new InvalidOperationException($"桌号 '{dto.TableNo}' 已存在");
        }

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

        var disabledId = await ResolveStatusIdAsync("停用", ct);
        table.TableStatus = disabledId ?? 3; // 停用
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    // =====================================================
    //  TableController (api/table)
    // =====================================================

    public async Task<(List<TableListItemDto> Records, int Total)> GetTableListAsync(
        int pageNum, int pageSize, string? keyword, string? status, CancellationToken ct)
    {
        var query = _dbContext.DiningTables
            .AsNoTracking();

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

    public async Task<TableDetailDto?> GetTableDetailAsync(string id, string baseUrl, CancellationToken ct)
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
            QrCodeUrl = BuildQrCodeFullUrl(table.QrCodeImageUrl, baseUrl),
        };
    }

    public async Task<TableMutationResponseDto> CreateTableAsync(
        CreateTableRequestDto dto, string baseUrl, CancellationToken ct)
    {
        if (dto.Capacity < 1 || dto.Capacity > 30)
            throw new ArgumentException("容纳人数必须在 1-30 之间");

        var existing = await _dbContext.DiningTables
            .FirstOrDefaultAsync(t => t.TableNo == dto.Tableno, ct);

        if (existing != null)
        {
            var disabledId = await ResolveStatusIdAsync("停用", ct);
            if (disabledId.HasValue && existing.TableStatus == disabledId.Value)
            {
                var status = dto.Status ?? 1;
                var qrPath = await GenerateQrCodeAsync(dto.Tableno, baseUrl, ct);

                existing.SeatCount = dto.Capacity;
                existing.TableStatus = status;
                existing.QrCodeImageUrl = qrPath;
                existing.CreatedAt = DateTime.Now;

                await _dbContext.SaveChangesAsync(ct);

                _logger.LogInformation("重新启用停用桌台: {Tableno}", dto.Tableno);

                return new TableMutationResponseDto
                {
                    Id = dto.Tableno,
                    Tableno = dto.Tableno,
                    Capacity = dto.Capacity,
                    Status = status,
                    QrCodeUrl = BuildQrCodeFullUrl(qrPath, baseUrl),
                };
            }

            throw new InvalidOperationException($"桌号 '{dto.Tableno}' 已存在");
        }

        // 生成二维码（存储相对路径，返回完整URL）
        var newQrPath = await GenerateQrCodeAsync(dto.Tableno, baseUrl, ct);

        var newStatus = dto.Status ?? 1;

        var entity = new DiningTables
        {
            TableNo = dto.Tableno,
            SeatCount = dto.Capacity,
            TableStatus = newStatus,
            QrCodeImageUrl = newQrPath,
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
            Status = newStatus,
            QrCodeUrl = BuildQrCodeFullUrl(newQrPath, baseUrl),
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
            QrCodeUrl = BuildQrCodeFullUrl(table.QrCodeImageUrl, baseUrl),
        };
    }

    public async Task<bool> DeleteTableAsync(string id, CancellationToken ct)
    {
        var table = await _dbContext.DiningTables.FirstOrDefaultAsync(t => t.TableNo == id, ct);
        if (table is null) return false;

        var disabledId = await ResolveStatusIdAsync("停用", ct);
        table.TableStatus = disabledId ?? 3; // 停用
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

    public async Task<int> RegenerateAllQrCodesAsync(string baseUrl, CancellationToken ct)
    {
        var disabledId = await ResolveStatusIdAsync("停用", ct);
        var tables = await _dbContext.DiningTables
            .Where(t => !disabledId.HasValue || t.TableStatus != disabledId.Value)
            .ToListAsync(ct);

        var count = 0;
        foreach (var table in tables)
        {
            try
            {
                var qrPath = await GenerateQrCodeAsync(table.TableNo, baseUrl, ct);
                table.QrCodeImageUrl = qrPath;
                count++;
                _logger.LogInformation("二维码已重新生成: {TableNo} → {QrPath}", table.TableNo, qrPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "二维码重新生成失败: {TableNo}", table.TableNo);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("二维码重新生成完成，共处理 {Count} 张", count);
        return count;
    }
}
