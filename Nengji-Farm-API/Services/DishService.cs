using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

namespace WebAPI.Services;

public class DishService : IDishService
{
    private readonly ManageAppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public DishService(ManageAppDbContext dbContext, IWebHostEnvironment env)
    {
        _dbContext = dbContext;
        _env = env;
    }

    public async Task<(List<DishListItemDto> Records, int Total)> GetDishListAsync(
        int pageNum, int pageSize, string? keyword, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Dishes.AsNoTracking()
            .Where(x => x.IsdeleteId == 0);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordTrimmed = keyword.Trim();
            query = query.Where(x =>
                x.DishName.Contains(keywordTrimmed) ||
                x.DishId.ToString().Contains(keywordTrimmed));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusId = MapStatusToId(status);
            query = query.Where(x => x.Status == statusId);
        }

        var total = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(x => x.DishId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DishListItemDto
            {
                Id = x.DishId.ToString(),
                Name = x.DishName,
                Price = x.DishPrice,
                Stock = x.DishRemainingQuantity,
                Status = MapStatusToText(x.Status),
                Image = x.ImageUrl ?? string.Empty,
                UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Description = x.DishDescription ?? string.Empty,
                DishType = x.DishType,
            })
            .ToListAsync(cancellationToken);

        // Batch-load spec images
        var dishIds = records.Select(r => int.Parse(r.Id)).ToList();
        var dishImages = await _dbContext.Set<DishImage>()
            .Where(x => dishIds.Contains(x.DishId))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var imageGroups = dishImages.GroupBy(x => x.DishId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var r in records)
        {
            r.Image = MediaHelper.NormalizeImageUrl(r.Image);
            if (imageGroups.TryGetValue(int.Parse(r.Id), out var imgs))
            {
                r.SpecImages = imgs.Select(i => MediaHelper.NormalizeImageUrl(i.ImageUrl)).ToList();
            }
        }

        return (records, total);
    }

    public async Task<DishDetailDto?> GetDishDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var dish = await _dbContext.Dishes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DishId == id && x.IsdeleteId == 0, cancellationToken);

        if (dish is null)
            return null;

        var images = await _dbContext.Set<DishImage>()
            .Where(x => x.DishId == id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var specImages = images
            .Select(x => MediaHelper.NormalizeImageUrl(x.ImageUrl))
            .Take(5)
            .ToList();

        return new DishDetailDto
        {
            Id = dish.DishId.ToString(),
            Name = dish.DishName,
            Price = dish.DishPrice,
            Stock = dish.DishRemainingQuantity,
            Status = MapStatusToText(dish.Status),
            Image = MediaHelper.NormalizeImageUrl(dish.ImageUrl),
            CoverImage = MediaHelper.NormalizeImageUrl(dish.ImageUrl),
            SpecImages = specImages,
            Description = dish.DishDescription,
            UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            DishType = dish.DishType,
        };
    }

    public async Task<int> CreateDishAsync(CreateDishDto dto, CancellationToken cancellationToken = default)
    {
        var price = dto.Price > 0 ? dto.Price : 0.01m;
        var stock = dto.Stock >= int.MaxValue / 2 ? 0 : Math.Max(0, dto.Stock);

        var dish = new Dish
        {
            DishName = dto.Name,
            DishPrice = price,
            DishRemainingQuantity = stock,
            Status = MapStatusToId(dto.Status),
            ImageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath),
            DishDescription = dto.Description ?? string.Empty,
            DishCategoryId = 1,
            AttributeName = string.Empty,
            LimitedEdition = 0,
            DishSold = 0,
            UserPurchaseLimit = 0,
            DishType = dto.DishType,
        };

        _dbContext.Dishes.Add(dish);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (dto.SpecImages?.Count > 0)
        {
            var specMaterials = dto.SpecImages
                .Select((url, index) => new DishImage
                {
                    DishId = dish.DishId,
                    ImageUrl = MediaHelper.ProcessImageData(url, _env.WebRootPath),
                    SortOrder = index,
                })
                .ToList();

            _dbContext.Set<DishImage>().AddRange(specMaterials);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return dish.DishId;
    }

    public async Task<bool> UpdateDishAsync(UpdateDishDto dto, CancellationToken cancellationToken = default)
    {
        var dish = await _dbContext.Dishes
            .FirstOrDefaultAsync(x => x.DishId == dto.Id, cancellationToken);

        if (dish is null)
            return false;

        dish.DishName = dto.Name;
        dish.DishPrice = dto.Price > 0 ? dto.Price : 0.01m;
        dish.DishRemainingQuantity = dto.Stock >= int.MaxValue / 2 ? 0 : Math.Max(0, dto.Stock);
        dish.Status = MapStatusToId(dto.Status);
        dish.ImageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath);
        dish.DishDescription = dto.Description ?? string.Empty;
        dish.DishType = dto.DishType;

        // Replace spec images
        var oldImages = await _dbContext.Set<DishImage>()
            .Where(x => x.DishId == dto.Id)
            .ToListAsync(cancellationToken);

        if (oldImages.Count > 0)
            _dbContext.Set<DishImage>().RemoveRange(oldImages);

        if (dto.SpecImages?.Count > 0)
        {
            var specMaterials = dto.SpecImages
                .Select((url, index) => new DishImage
                {
                    DishId = dish.DishId,
                    ImageUrl = MediaHelper.ProcessImageData(url, _env.WebRootPath),
                    SortOrder = index,
                })
                .ToList();

            _dbContext.Set<DishImage>().AddRange(specMaterials);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteDishAsync(int id, CancellationToken cancellationToken = default)
    {
        var dish = await _dbContext.Dishes
            .FirstOrDefaultAsync(x => x.DishId == id && x.IsdeleteId == 0, cancellationToken);

        if (dish is null)
            return false;

        dish.IsdeleteId = 1;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteDishBatchAsync(int[] ids, CancellationToken cancellationToken = default)
    {
        var dishes = await _dbContext.Dishes
            .Where(x => ids.Contains(x.DishId) && x.IsdeleteId == 0)
            .ToListAsync(cancellationToken);

        if (dishes.Count == 0)
            return false;

        foreach (var d in dishes)
            d.IsdeleteId = 1;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static int MapStatusToId(string status)
    {
        var normalized = status.StartsWith("已", StringComparison.Ordinal)
            ? status[1..]
            : status;

        return normalized switch
        {
            "上架" => 1,
            "售空" => 3,
            _ => 2 // 已下架或其他默认为下架
        };
    }

    private static string MapStatusToText(int status)
    {
        return status switch
        {
            1 => "已上架",
            3 => "已售空",
            _ => "已下架"
        };
    }
}
