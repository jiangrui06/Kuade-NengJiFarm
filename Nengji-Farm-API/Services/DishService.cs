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
    private readonly IInventoryStatsService _inventoryStatsService;

    public DishService(ManageAppDbContext dbContext, IWebHostEnvironment env, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _env = env;
        _inventoryStatsService = inventoryStatsService;
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
            var statusId = await MapStatusToIdAsync(status, cancellationToken);
            query = query.Where(x => x.Status == statusId);
        }

        var total = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(x => x.DishId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.DishId,
                x.DishName,
                x.DishPrice,
                x.DishRemainingQuantity,
                x.Status,
                x.ImageUrl,
                x.DishDescription,
                x.DishCategoryId,
            })
            .ToListAsync(cancellationToken);

        // Load dish categories for name mapping
        var categoryIds = records.Select(r => r.DishCategoryId).Distinct().ToList();
        var categories = await _dbContext.Set<DishCategory>()
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.DishCategoryId))
            .ToDictionaryAsync(c => c.DishCategoryId, c => c.DishCategoryName, cancellationToken);

        var (statusIdToName, _) = await LoadStatusMappingAsync(cancellationToken);

        var mappedRecords = records.Select(r => new DishListItemDto
        {
            Id = r.DishId.ToString(),
            Name = r.DishName,
            Price = r.DishPrice,
            Stock = r.DishRemainingQuantity,
            Status = MapStatusToText(r.Status, statusIdToName),
            Image = r.ImageUrl ?? string.Empty,
            UploadTime = string.Empty,
            Description = r.DishDescription ?? string.Empty,
            DishType = categories.GetValueOrDefault(r.DishCategoryId) ?? string.Empty,
        }).ToList();

        // Batch-load spec images
        var dishIds = mappedRecords.Select(r => int.Parse(r.Id)).ToList();
        var dishImages = await _dbContext.Set<DishImage>()
            .Where(x => dishIds.Contains(x.DishId))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var imageGroups = dishImages.GroupBy(x => x.DishId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var r in mappedRecords)
        {
            r.Image = MediaHelper.NormalizeImageUrl(r.Image);
            if (imageGroups.TryGetValue(int.Parse(r.Id), out var imgs))
            {
                r.SpecImages = imgs.Select(i => new SpecImageItemDto
                {
                    Url = MediaHelper.NormalizeImageUrl(i.ImageUrl),
                    SortOrder = i.SortOrder
                }).ToList();
            }
        }

        // Batch-load upload times from sys_config
        var uploadTimeKeys = dishIds.Select(id => $"dish_upload_time_{id}").ToList();
        var uploadTimeConfigs = await _dbContext.SysConfigs
            .AsNoTracking()
            .Where(c => uploadTimeKeys.Contains(c.ConfigKey))
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, cancellationToken);

        foreach (var r in mappedRecords)
        {
            if (uploadTimeConfigs.TryGetValue($"dish_upload_time_{r.Id}", out var timeStr))
            {
                r.UploadTime = timeStr;
            }
        }

        return (mappedRecords, total);
    }

    public async Task<DishDetailDto?> GetDishDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var dish = await _dbContext.Dishes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DishId == id && x.IsdeleteId == 0, cancellationToken);

        if (dish is null)
            return null;

        // Get category name
        var categoryName = await _dbContext.Set<DishCategory>()
            .AsNoTracking()
            .Where(c => c.DishCategoryId == dish.DishCategoryId)
            .Select(c => c.DishCategoryName)
            .FirstOrDefaultAsync(cancellationToken);

        var (statusIdToName, _) = await LoadStatusMappingAsync(cancellationToken);

        var images = await _dbContext.Set<DishImage>()
            .Where(x => x.DishId == id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var carouselMedia = images
            .Where(x => x.MaterialType == 0)
            .Select(x => new CarouselMediaDto
            {
                Type = MediaHelper.IsVideoUrl(x.ImageUrl) ? "video" : "image",
                Url = MediaHelper.NormalizeImageUrl(x.ImageUrl),
                SortOrder = x.SortOrder,
                Thumb = MediaHelper.IsVideoUrl(x.ImageUrl) ? MediaHelper.GetVideoThumbUrl(x.ImageUrl) : null,
            })
            .Take(5)
            .ToList();

        var specImages = images
            .Where(x => x.MaterialType != 0)
            .Select(x => new SpecImageItemDto
            {
                Url = MediaHelper.NormalizeImageUrl(x.ImageUrl),
                SortOrder = x.SortOrder
            })
            .Take(5)
            .ToList();

        var stats = (await _inventoryStatsService.GetDishStatsAsync([id], cancellationToken)).GetValueOrDefault(id);
        var sold = stats?.Sold ?? dish.DishSold;

        var detail = new DishDetailDto
        {
            Id = dish.DishId.ToString(),
            Name = dish.DishName,
            Price = dish.DishPrice,
            Stock = dish.DishRemainingQuantity,
            Sold = sold,
            Status = MapStatusToText(dish.Status, statusIdToName),
            Image = MediaHelper.NormalizeImageUrl(dish.ImageUrl),
            CoverImage = MediaHelper.NormalizeImageUrl(dish.ImageUrl),
            CarouselMedia = carouselMedia,
            SpecImages = specImages,
            Description = dish.DishDescription,
            UploadTime = string.Empty,
            DishType = categoryName ?? string.Empty,
        };

        // Load upload time from sys_config
        var uploadTimeConfig = await _dbContext.SysConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == $"dish_upload_time_{id}", cancellationToken);
        if (uploadTimeConfig is not null)
        {
            detail.UploadTime = uploadTimeConfig.ConfigValue;
        }

        return detail;
    }

    public async Task<int> CreateDishAsync(CreateDishDto dto, CancellationToken cancellationToken = default)
    {
        var price = dto.Price > 0 ? dto.Price : 0.01m;
        var stock = dto.Stock >= int.MaxValue / 2 ? 0 : Math.Max(0, dto.Stock);
        var statusId = await MapStatusToIdAsync(dto.Status, cancellationToken);

        var dish = new Dish
        {
            DishName = dto.Name,
            DishPrice = price,
            DishRemainingQuantity = stock,
            Status = statusId,
            ImageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath),
            DishDescription = dto.Description ?? string.Empty,
            DishCategoryId = await ResolveCategoryIdAsync(dto.DishType, cancellationToken),
            AttributeName = string.Empty,
            LimitedEdition = 0,
            DishSold = 0,
        };

        _dbContext.Dishes.Add(dish);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (dto.CarouselMedia?.Count > 0)
        {
            var carouselMaterials = dto.CarouselMedia
                .Select((item, index) => new DishImage
                {
                    DishId = dish.DishId,
                    ImageUrl = MediaHelper.ProcessImageData(item.Url, _env.WebRootPath),
                    SortOrder = item.SortOrder,
                    MaterialType = 0,
                })
                .ToList();

            _dbContext.Set<DishImage>().AddRange(carouselMaterials);
        }

        if (dto.SpecImages?.Count > 0)
        {
            var specMaterials = dto.SpecImages
                .Select((item, index) => new DishImage
                {
                    DishId = dish.DishId,
                    ImageUrl = MediaHelper.ProcessImageData(item.Url, _env.WebRootPath),
                    SortOrder = item.SortOrder,
                    MaterialType = 1,
                })
                .ToList();

            _dbContext.Set<DishImage>().AddRange(specMaterials);
        }

        // 新增时如果状态为上架，记录上架时间
        if (statusId == 1)
        {
            _dbContext.SysConfigs.Add(new WebAPI.Entities.SysConfig
            {
                ConfigKey = $"dish_upload_time_{dish.DishId}",
                ConfigValue = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Description = "菜品上架时间"
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return dish.DishId;
    }

    public async Task<bool> UpdateDishAsync(UpdateDishDto dto, CancellationToken cancellationToken = default)
    {
        var dish = await _dbContext.Dishes
            .FirstOrDefaultAsync(x => x.DishId == dto.Id, cancellationToken);

        if (dish is null)
            return false;

        var oldStatusId = dish.Status;
        var newStatusId = await MapStatusToIdAsync(dto.Status, cancellationToken);

        dish.DishName = dto.Name;
        dish.DishPrice = dto.Price > 0 ? dto.Price : 0.01m;
        dish.DishRemainingQuantity = dto.Stock >= int.MaxValue / 2 ? 0 : Math.Max(0, dto.Stock);
        dish.Status = newStatusId;
        dish.ImageUrl = MediaHelper.ProcessImageData(dto.Image, _env.WebRootPath);
        dish.DishDescription = dto.Description ?? string.Empty;
        dish.DishCategoryId = await ResolveCategoryIdAsync(dto.DishType, cancellationToken);

        // Replace all existing images (carousel + spec)
        var oldImages = await _dbContext.Set<DishImage>()
            .Where(x => x.DishId == dto.Id)
            .ToListAsync(cancellationToken);

        if (oldImages.Count > 0)
            _dbContext.Set<DishImage>().RemoveRange(oldImages);

        var newImages = new List<DishImage>();

        if (dto.CarouselMedia?.Count > 0)
        {
            newImages.AddRange(dto.CarouselMedia
                .Select((item, index) => new DishImage
                {
                    DishId = dish.DishId,
                    ImageUrl = MediaHelper.ProcessImageData(item.Url, _env.WebRootPath),
                    SortOrder = item.SortOrder,
                    MaterialType = 0,
                }));
        }

        if (dto.SpecImages?.Count > 0)
        {
            newImages.AddRange(dto.SpecImages
                .Select((item, index) => new DishImage
                {
                    DishId = dto.Id,
                    ImageUrl = MediaHelper.ProcessImageData(item.Url, _env.WebRootPath),
                    SortOrder = item.SortOrder,
                    MaterialType = 1,
                }));
        }

        if (newImages.Count > 0)
            _dbContext.Set<DishImage>().AddRange(newImages);

        // 只有从非上架变为上架时才更新上架时间
        if (oldStatusId != 1 && newStatusId == 1)
        {
            var key = $"dish_upload_time_{dish.DishId}";
            var existing = await _dbContext.SysConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key, cancellationToken);
            var timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (existing is null)
            {
                _dbContext.SysConfigs.Add(new WebAPI.Entities.SysConfig
                {
                    ConfigKey = key,
                    ConfigValue = timeStr,
                    Description = "菜品上架时间"
                });
            }
            else
            {
                existing.ConfigValue = timeStr;
            }
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

    /// <summary>从 dish_status 表加载状态映射（id ↔ name）</summary>
    private async Task<(Dictionary<int, string> IdToName, Dictionary<string, int> NameToId)> LoadStatusMappingAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _dbContext.DishStatuses.ToListAsync(cancellationToken);
        var idToName = new Dictionary<int, string>();
        var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in statuses)
        {
            idToName[s.DishStatusId] = s.StatusName;
            var raw = s.StatusName;
            nameToId[raw] = s.DishStatusId;
            if (raw.StartsWith("已", StringComparison.Ordinal))
                nameToId[raw[1..]] = s.DishStatusId;
        }

        return (idToName, nameToId);
    }

    public async Task<int> MapStatusToIdAsync(string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(status))
            return 1;

        var normalized = status.Trim();

        // 兼容前端传 statusId（数字字符串），直接返回
        if (int.TryParse(normalized, out var id))
            return id;

        var (_, nameToId) = await LoadStatusMappingAsync(cancellationToken);
        return nameToId.TryGetValue(normalized, out var mappedId) ? mappedId : 1;
    }

    private static string MapStatusToText(int status, Dictionary<int, string>? statusMap = null)
    {
        if (statusMap != null && statusMap.TryGetValue(status, out var name))
            return name;
        return status switch
        {
            1 => "已上架",
            3 => "已售空",
            _ => "已下架"
        };
    }

    /// <summary>
    /// 根据 dishType 菜品类型名称解析分类ID，未匹配或为空时返回默认分类(1)
    /// </summary>
    private async Task<int> ResolveCategoryIdAsync(string? dishType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dishType))
            return 1;

        var category = await _dbContext.Set<DishCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DishCategoryName == dishType.Trim(), cancellationToken);

        return category?.DishCategoryId ?? 1;
    }
}
