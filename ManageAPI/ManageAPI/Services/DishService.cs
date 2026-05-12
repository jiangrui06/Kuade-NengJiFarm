using Microsoft.EntityFrameworkCore;

using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

namespace ManageAPI.Services;

public class DishService : IDishService
{
    private readonly AppDbContext _context;
    private readonly string _iconsDir;

    public DishService(AppDbContext context)
    {
        _context = context;
        _iconsDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Kude-NenJi-Api", "DemoAPI", "WebAPI", "WebAPI", "wwwroot", "icons"));
    }

    public async Task<(List<DishListItemDto> Records, int Total)> GetDishListAsync(
        int pageNum, int pageSize, string? keyword, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Dishes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            if (int.TryParse(kw, out var id))
                query = query.Where(x => x.DishId == id);
            else
                query = query.Where(x => x.DishName.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusId = await _context.Set<DishStatus>()
                .Where(s => s.StatusName == status)
                .Select(s => (int?)s.DishStatusId)
                .FirstOrDefaultAsync(cancellationToken);
            if (statusId.HasValue)
                query = query.Where(x => x.Status == statusId.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var dishes = await query
            .OrderByDescending(x => x.DishId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Build status name map
        var allStatuses = await _context.Set<DishStatus>().ToListAsync(cancellationToken);
        var statusMap = allStatuses.ToDictionary(x => x.DishStatusId, x => x.StatusName);

        // Load all related images
        var dishIds = dishes.Select(x => x.DishId).ToList();
        var dishImages = await _context.Set<DishImage>()
            .Where(x => dishIds.Contains(x.DishId))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var imageGroups = dishImages.GroupBy(x => x.DishId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var records = dishes.Select(x =>
        {
            var imgs = imageGroups.GetValueOrDefault(x.DishId) ?? [];
            return new DishListItemDto
            {
                Id = x.DishId.ToString(),
                Name = x.DishName,
                Image = string.IsNullOrEmpty(x.ImageUrl) ? string.Empty : x.ImageUrl,
                UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Price = x.DishPrice,
                Stock = x.DishRemainingQuantity,
                Status = statusMap.TryGetValue(x.Status, out var s) ? s : "未知",
                Description = x.DishDescription ?? string.Empty,
                CarouselMedia = [],
                SpecImages = imgs.Select(i => i.ImageUrl).ToList()
            };
        }).ToList();

        return (records, total);
    }

    public async Task<DishDetailDto?> GetDishDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var dish = await _context.Dishes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DishId == id, cancellationToken);

        if (dish is null) return null;

        var statusName = await _context.Set<DishStatus>()
            .Where(s => s.DishStatusId == dish.Status)
            .Select(s => s.StatusName)
            .FirstOrDefaultAsync(cancellationToken) ?? "未知";

        var images = await _context.Set<DishImage>()
            .Where(x => x.DishId == id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return new DishDetailDto
        {
            Id = dish.DishId.ToString(),
            Name = dish.DishName,
            Image = dish.ImageUrl ?? string.Empty,
            CoverImage = dish.ImageUrl ?? string.Empty,
            UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            Price = dish.DishPrice,
            Stock = dish.DishRemainingQuantity,
            Status = statusName,
            Description = dish.DishDescription,
            CarouselMedia = [],
            SpecImages = images.Select(x => x.ImageUrl).ToList()
        };
    }

    public async Task<int> CreateDishAsync(CreateDishDto dto, CancellationToken cancellationToken = default)
    {
        var statusId = await ResolveStatusIdAsync(dto.Status, cancellationToken);

        var dish = new Dish
        {
            DishName = dto.Name,
            DishPrice = dto.Price,
            DishRemainingQuantity = dto.Stock,
            Status = statusId,
            ImageUrl = SaveImageFromBase64(dto.Image) ?? string.Empty,
            DishDescription = dto.Description ?? string.Empty,
            DishCategoryId = 1,
            AttributeName = string.Empty,
            LimitedEdition = 0,
            DishSold = 0,
            UserPurchaseLimit = 0
        };

        _context.Dishes.Add(dish);
        await _context.SaveChangesAsync(cancellationToken);

        // Save spec images
        if (dto.SpecImages?.Count > 0)
        {
            for (var i = 0; i < dto.SpecImages.Count; i++)
            {
                _context.Add(new DishImage
                {
                    DishId = dish.DishId,
                    ImageUrl = SaveImageFromBase64(dto.SpecImages[i]) ?? dto.SpecImages[i],
                    SortOrder = i
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return dish.DishId;
    }

    public async Task<bool> UpdateDishAsync(UpdateDishDto dto, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(dto.Id, out var dishId))
            return false;

        var dish = await _context.Dishes.FirstOrDefaultAsync(x => x.DishId == dishId, cancellationToken);
        if (dish is null) return false;

        if (!string.IsNullOrWhiteSpace(dto.Name))
            dish.DishName = dto.Name.Trim();

        if (dto.Price.HasValue)
            dish.DishPrice = dto.Price.Value;

        if (dto.Stock.HasValue)
            dish.DishRemainingQuantity = dto.Stock.Value;

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var statusId = await ResolveStatusIdAsync(dto.Status, cancellationToken);
            dish.Status = statusId;
        }

        if (dto.Image != null)
            dish.ImageUrl = SaveImageFromBase64(dto.Image) ?? string.Empty;

        if (dto.Description != null)
            dish.DishDescription = dto.Description;

        await _context.SaveChangesAsync(cancellationToken);

        // Replace spec images if provided
        if (dto.SpecImages != null)
        {
            var oldImages = await _context.Set<DishImage>()
                .Where(x => x.DishId == dishId)
                .ToListAsync(cancellationToken);
            _context.RemoveRange(oldImages);

            for (var i = 0; i < dto.SpecImages.Count; i++)
            {
                _context.Add(new DishImage
                {
                    DishId = dishId,
                    ImageUrl = SaveImageFromBase64(dto.SpecImages[i]) ?? dto.SpecImages[i],
                    SortOrder = i
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteDishAsync(int id, CancellationToken cancellationToken = default)
    {
        var dish = await _context.Dishes.FirstOrDefaultAsync(x => x.DishId == id, cancellationToken);
        if (dish is null) return false;

        var images = await _context.Set<DishImage>()
            .Where(x => x.DishId == id)
            .ToListAsync(cancellationToken);
        _context.RemoveRange(images);
        _context.Dishes.Remove(dish);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteDishBatchAsync(int[] ids, CancellationToken cancellationToken = default)
    {
        var dishes = await _context.Dishes
            .Where(x => ids.Contains(x.DishId))
            .ToListAsync(cancellationToken);

        if (dishes.Count == 0) return false;

        var dishIds = dishes.Select(x => x.DishId).ToList();
        var images = await _context.Set<DishImage>()
            .Where(x => dishIds.Contains(x.DishId))
            .ToListAsync(cancellationToken);

        _context.RemoveRange(images);
        _context.Dishes.RemoveRange(dishes);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string? SaveImageFromBase64(string? image)
    {
        if (string.IsNullOrEmpty(image))
            return image;

        // If it's a /icons/ URL, strip the prefix to get just the filename
        if (image.StartsWith("/icons/", StringComparison.OrdinalIgnoreCase))
            return image["/icons/".Length..];

        // If it's not base64, return as-is (it's already a filename)
        if (!image.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return image;

        try
        {
            if (!Directory.Exists(_iconsDir))
                Directory.CreateDirectory(_iconsDir);

            var base64Data = image.Substring(image.IndexOf(",") + 1);
            var bytes = Convert.FromBase64String(base64Data);

            var ext = image.Contains("png") ? ".png" : image.Contains("gif") ? ".gif" : ".jpg";
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(_iconsDir, fileName);

            File.WriteAllBytes(filePath, bytes);
            return fileName;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private async Task<int> ResolveStatusIdAsync(string statusName, CancellationToken cancellationToken)
    {
        // Handle "已上架"/"已下架" by stripping the "已" prefix
        var normalized = statusName.StartsWith("已", StringComparison.Ordinal)
            ? statusName[1..]
            : statusName;

        var id = await _context.Set<DishStatus>()
            .Where(s => s.StatusName == normalized)
            .Select(s => (int?)s.DishStatusId)
            .FirstOrDefaultAsync(cancellationToken);

        return id ?? 1;
    }
}
