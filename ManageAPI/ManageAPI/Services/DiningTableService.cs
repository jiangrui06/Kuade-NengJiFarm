using Microsoft.EntityFrameworkCore;

using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

public class DiningTableService : IDiningTableService
{
    private readonly AppDbContext _dbContext;

    public DiningTableService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(List<DiningTableListItemDto> Records, int Total)> GetListAsync(
        int pageNum, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DiningTables.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(t => t.TableNo.Contains(kw));
        }

        var total = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(t => t.DiningTableId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .Join(_dbContext.DiningTableStatusDicts,
                table => table.TableStatus,
                status => status.TableStatusId,
                (table, status) => new DiningTableListItemDto
                {
                    Id = table.DiningTableId,
                    TableNo = table.TableNo,
                    SeatCount = table.SeatCount,
                    TableStatus = table.TableStatus,
                    StatusName = status.StatusName,
                    QrCodeImageUrl = table.QrCodeImageUrl,
                    CreatedAt = table.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
            .ToListAsync(cancellationToken);

        return (records, total);
    }

    public async Task<long> CreateAsync(CreateDiningTableDto dto, CancellationToken cancellationToken = default)
    {
        var table = new DiningTables
        {
            TableNo = dto.TableNo,
            SeatCount = dto.SeatCount,
            TableStatus = dto.TableStatus,
            QrCodeImageUrl = dto.QrCodeImageUrl,
            CreatedAt = DateTime.Now
        };

        _dbContext.DiningTables.Add(table);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return table.DiningTableId;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var table = await _dbContext.DiningTables
            .FirstOrDefaultAsync(t => t.DiningTableId == id, cancellationToken);

        if (table is null)
            return false;

        _dbContext.DiningTables.Remove(table);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
