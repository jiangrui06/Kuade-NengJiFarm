namespace WebAPI.Services;

using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities.Manage;

public class ProductService : IProductService
{
    private readonly ManageAppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public ProductService(ManageAppDbContext dbContext, IWebHostEnvironment env)
    {
        _dbContext = dbContext;
        _env = env;
    }

    public async Task<(List<ProductListItemDto> Records, int Total)> GetProductListAsync(
        int pageNum,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Commodities.AsNoTracking()
            .Where(c => c.IsdeleteId == 0);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordTrimmed = keyword.Trim();
            query = query.Where(c =>
                c.ProductName != null && c.ProductName.Contains(keywordTrimmed));
        }

        var total = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(c => c.CommodityId)
            .Skip((pageNum - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.CommodityId,
                c.ProductName,
                c.UnitPrice,
                c.InStock,
                c.CommodityStatusId,
                c.ImageUrl,
                c.WeightText,
                c.CategoryId,
                c.UploadTime,
            })
            .ToListAsync(cancellationToken);

        var categoryIds = records.Select(r => r.CategoryId).Distinct().ToList();
        var categories = await _dbContext.Set<CommodityCategory>()
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CategoryName, cancellationToken);

        var (statusIdToName, _) = await LoadStatusMappingAsync(cancellationToken);

        var mapped = records.Select(c =>
        {
            var (netWeight, weightUnit) = ParseWeightText(c.WeightText);
            return new ProductListItemDto
            {
                Id = c.CommodityId.ToString(),
                Name = c.ProductName ?? string.Empty,
                Price = c.UnitPrice ?? 0m,
                Stock = c.InStock ?? 0,
                Status = MapStatusToText(c.CommodityStatusId, statusIdToName),
                Image = MediaHelper.NormalizeImageUrl(c.ImageUrl),
                UploadTime = c.UploadTime.ToString("yyyy-MM-dd HH:mm"),
                NetWeight = netWeight,
                WeightUnit = weightUnit,
                ProductType = categories.GetValueOrDefault(c.CategoryId) ?? "实物",
            };
        }).ToList();

        return (records: mapped, total);
    }

    public async Task<ProductDetailDto?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var commodity = await _dbContext.Commodities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CommodityId == id && c.IsdeleteId == 0, cancellationToken);

        if (commodity is null)
        {
            return null;
        }

        var materials = await _dbContext.CommodityMaterials
            .AsNoTracking()
            .Where(m => m.CommodityId == id)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        var carouselMedia = new List<CarouselMediaDto>();
        var specImages = new List<string>();

        foreach (var material in materials)
        {
            if (material.MaterialType == 0)
            {
                carouselMedia.Add(new CarouselMediaDto
                {
                    Type = MediaHelper.IsVideoUrl(material.MaterialUrl) ? "video" : "image",
                    Url = material.MaterialUrl ?? string.Empty,
                });
            }
            else if (material.MaterialType == 1)
            {
                specImages.Add(material.MaterialUrl ?? string.Empty);
            }
            else if (material.MaterialType == 2)
            {
                carouselMedia.Add(new CarouselMediaDto
                {
                    Type = "video",
                    Url = material.MaterialUrl ?? string.Empty,
                });
            }
        }

        var (netWeight, weightUnit) = ParseWeightText(commodity.WeightText);

        var carouselList = carouselMedia.Take(5).ToList();
        foreach (var m in carouselList)
        {
            m.Url = MediaHelper.NormalizeImageUrl(m.Url);
        }

        var specList = specImages.Take(5).Select(MediaHelper.NormalizeImageUrl).ToList();

        var categoryName = await _dbContext.Set<CommodityCategory>()
            .AsNoTracking()
            .Where(c => c.Id == commodity.CategoryId)
            .Select(c => c.CategoryName)
            .FirstOrDefaultAsync(cancellationToken);

        var (statusIdToName, _) = await LoadStatusMappingAsync(cancellationToken);

        return new ProductDetailDto
        {
            Id = commodity.CommodityId.ToString(),
            Name = commodity.ProductName,
            Price = commodity.UnitPrice ?? 0m,
            Stock = commodity.InStock ?? 0,
            Status = MapStatusToText(commodity.CommodityStatusId, statusIdToName),
            Image = MediaHelper.NormalizeImageUrl(commodity.ImageUrl),
            CoverImage = MediaHelper.NormalizeImageUrl(commodity.ImageUrl),
            CarouselMedia = carouselList,
            NetWeight = netWeight,
            WeightUnit = weightUnit,
            StorageCondition = commodity.StorageCondition,
            SpecImages = specList,
            Description = commodity.SpecDescription,
            UploadTime = commodity.UploadTime.ToString("yyyy-MM-dd HH:mm"),
            ProductType = categoryName ?? "实物",
        };
    }

    public async Task<int> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        var commodity = new Commodity
        {
            ProductName = dto.Name,
            UnitPrice = dto.Price,
            InStock = dto.Stock,
            Quantity = dto.Stock,
            CommodityStatusId = await MapStatusToIdAsync(dto.Status, cancellationToken),
            ImageUrl = MediaHelper.ProcessImageData(dto.CoverImage, _env.WebRootPath),
            StorageCondition = dto.StorageCondition,
            SpecDescription = dto.Description,
            WeightText = BuildWeightText(dto.NetWeight, dto.WeightUnit),
            UnitId = dto.UnitId,
            CategoryId = await ResolveCategoryIdAsync(dto.ProductType, cancellationToken),
        };

        _dbContext.Commodities.Add(commodity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 如果新增时状态为上架，记录上架时间
        var statusId = await MapStatusToIdAsync(dto.Status, cancellationToken);
        if (statusId == 1)
        {
            commodity.UploadTime = DateTime.Now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var materialsToAdd = new List<CommodityMaterial>();

        if (dto.CarouselMedia?.Count > 0)
        {
            var carouselMaterials = dto.CarouselMedia
                .Select((m, index) => new CommodityMaterial
                {
                    CommodityId = commodity.CommodityId,
                    MaterialType = 0,
                    MaterialUrl = MediaHelper.ProcessImageData(m.Url, _env.WebRootPath),
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            materialsToAdd.AddRange(carouselMaterials);
        }

        if (dto.SpecImages?.Count > 0)
        {
            var specMaterials = dto.SpecImages
                .Select((url, index) => new CommodityMaterial
                {
                    CommodityId = commodity.CommodityId,
                    MaterialType = 1,
                    MaterialUrl = MediaHelper.ProcessImageData(url, _env.WebRootPath),
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            materialsToAdd.AddRange(specMaterials);
        }

        if (materialsToAdd.Count > 0)
        {
            _dbContext.CommodityMaterials.AddRange(materialsToAdd);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return commodity.CommodityId;
    }

    public async Task<bool> UpdateProductAsync(UpdateProductDto dto, CancellationToken cancellationToken = default)
    {
        var commodity = await _dbContext.Commodities
            .FirstOrDefaultAsync(c => c.CommodityId == dto.Id, cancellationToken);

        if (commodity is null)
        {
            return false;
        }

        // 检查状态变化：仅从下架/售罄变为上架时更新上架时间
        var oldStatusId = commodity.CommodityStatusId;
        var newStatusId = await MapStatusToIdAsync(dto.Status, cancellationToken);
        var wasOffShelf = oldStatusId.HasValue && oldStatusId.Value != 1;

        commodity.ProductName = dto.Name;
        commodity.UnitPrice = dto.Price;
        commodity.InStock = dto.Stock;
        commodity.Quantity = dto.Stock;
        commodity.CommodityStatusId = newStatusId;
        commodity.ImageUrl = MediaHelper.ProcessImageData(dto.CoverImage, _env.WebRootPath);
        commodity.StorageCondition = dto.StorageCondition;
        commodity.SpecDescription = dto.Description;
        commodity.WeightText = BuildWeightText(dto.NetWeight, dto.WeightUnit);
        commodity.UnitId = dto.UnitId;
        commodity.CategoryId = await ResolveCategoryIdAsync(dto.ProductType, cancellationToken);

        // 从非上架状态变为上架时，记录上架时间
        if (wasOffShelf && newStatusId == 1)
        {
            commodity.UploadTime = DateTime.Now;
        }

        var oldMaterials = await _dbContext.CommodityMaterials
            .Where(m => m.CommodityId == dto.Id)
            .ToListAsync(cancellationToken);

        if (oldMaterials.Count > 0)
            _dbContext.CommodityMaterials.RemoveRange(oldMaterials);

        var materialsToAdd = new List<CommodityMaterial>();

        if (dto.CarouselMedia?.Count > 0)
        {
            var carouselMaterials = dto.CarouselMedia
                .Select((m, index) => new CommodityMaterial
                {
                    CommodityId = commodity.CommodityId,
                    MaterialType = 0,
                    MaterialUrl = MediaHelper.ProcessImageData(m.Url, _env.WebRootPath),
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            materialsToAdd.AddRange(carouselMaterials);
        }

        if (dto.SpecImages?.Count > 0)
        {
            var specMaterials = dto.SpecImages
                .Select((url, index) => new CommodityMaterial
                {
                    CommodityId = commodity.CommodityId,
                    MaterialType = 1,
                    MaterialUrl = MediaHelper.ProcessImageData(url, _env.WebRootPath),
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            materialsToAdd.AddRange(specMaterials);
        }

        if (materialsToAdd.Count > 0)
            _dbContext.CommodityMaterials.AddRange(materialsToAdd);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var commodity = await _dbContext.Commodities
            .FirstOrDefaultAsync(c => c.CommodityId == id && c.IsdeleteId == 0, cancellationToken);

        if (commodity is null)
            return false;

        commodity.IsdeleteId = 1;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteProductBatchAsync(int[] ids, CancellationToken cancellationToken = default)
    {
        var commodities = await _dbContext.Commodities
            .Where(c => ids.Contains(c.CommodityId) && c.IsdeleteId == 0)
            .ToListAsync(cancellationToken);

        if (commodities.Count == 0)
            return false;

        foreach (var c in commodities)
            c.IsdeleteId = 1;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<int> ResolveCategoryIdAsync(string? productType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productType))
            return 1;

        var category = await _dbContext.Set<CommodityCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryName == productType.Trim(), cancellationToken);

        return category?.Id ?? 1;
    }

    /// <summary>从 commodity_status 表加载状态映射（id ↔ name）</summary>
    private async Task<(Dictionary<int, string> IdToName, Dictionary<string, int> NameToId)> LoadStatusMappingAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _dbContext.Set<CommodityStatus>().ToListAsync(cancellationToken);
        var idToName = new Dictionary<int, string>();
        var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in statuses)
        {
            idToName[s.CommodityStatusId] = s.StatusName;
            var raw = s.StatusName;
            nameToId[raw] = s.CommodityStatusId;
            if (raw.StartsWith("已", StringComparison.Ordinal))
                nameToId[raw[1..]] = s.CommodityStatusId;
        }

        return (idToName, nameToId);
    }

    public async Task<List<CommodityCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<CommodityCategory>()
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CommodityStatus>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<CommodityStatus>()
            .AsNoTracking()
            .OrderBy(s => s.CommodityStatusId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Unit>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Unit>()
            .AsNoTracking()
            .Where(u => u.IsEnabled == 1)
            .OrderBy(u => u.UnitId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductStatsDto> GetProductStatsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _dbContext.Commodities
            .AsNoTracking()
            .Where(c => c.IsdeleteId == 0)
            .ToListAsync(cancellationToken);

        return new ProductStatsDto
        {
            TotalProducts = all.Count,
            OnSaleCount = all.Count(c => c.CommodityStatusId == 1),
            StockAlertCount = all.Count(c => (c.InStock ?? 0) <= 5),
            TotalStock = all.Sum(c => c.InStock ?? 0),
        };
    }

    private static string BuildWeightText(decimal? netWeight, string? weightUnit)
    {
        if (netWeight == null || netWeight == 0)
            return string.Empty;

        var unit = string.IsNullOrWhiteSpace(weightUnit) ? string.Empty : weightUnit.Trim();
        return $"{netWeight}{unit}";
    }

    private static (decimal? netWeight, string? weightUnit) ParseWeightText(string? weightText)
    {
        if (string.IsNullOrWhiteSpace(weightText))
            return (null, null);

        var numericPart = new string(weightText.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        var unitPart = new string(weightText.SkipWhile(c => char.IsDigit(c) || c == '.').ToArray());

        if (!decimal.TryParse(numericPart, out var weight))
            return (null, null);

        return (weight, string.IsNullOrEmpty(unitPart) ? null : unitPart);
    }

    public async Task<int> MapStatusToIdAsync(string status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(status))
            return 1;

        var normalized = status.Trim();
        var (_, nameToId) = await LoadStatusMappingAsync(cancellationToken);
        return nameToId.TryGetValue(normalized, out var id) ? id : 1;
    }

    private static string MapStatusToText(int? statusId, Dictionary<int, string>? statusMap = null)
    {
        if (statusId.HasValue && statusMap != null && statusMap.TryGetValue(statusId.Value, out var name))
            return name;
        return statusId switch
        {
            1 => "已上架",
            3 => "已售空",
            _ => "已下架"
        };
    }

    public async Task<List<WeightTagOptionDto>> GetWeightTagOptionsAsync(CancellationToken cancellationToken = default)
    {
        var weightTexts = await _dbContext.Commodities
            .AsNoTracking()
            .Where(c => c.IsdeleteId == 0 && c.WeightText != null && c.WeightText.Trim() != "")
            .Select(c => c.WeightText!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return weightTexts
            .Select(wt =>
            {
                var (netWeight, weightUnit) = ParseWeightText(wt);
                return new WeightTagOptionDto
                {
                    Label = wt,
                    NetWeight = netWeight,
                    WeightUnit = weightUnit
                };
            })
            .OrderByDescending(x => x.NetWeight.HasValue)
            .ThenBy(x => x.Label)
            .ToList();
    }
}
