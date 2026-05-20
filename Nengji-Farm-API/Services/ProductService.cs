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
                (c.ProductName != null && c.ProductName.Contains(keywordTrimmed)) ||
                (c.CommodityId.ToString().Contains(keywordTrimmed)));
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
                c.ProductType,
            })
            .ToListAsync(cancellationToken);

        var mapped = records.Select(c =>
        {
            var (netWeight, weightUnit) = ParseWeightText(c.WeightText);
            return new ProductListItemDto
            {
                Id = c.CommodityId.ToString(),
                Name = c.ProductName ?? string.Empty,
                Price = c.UnitPrice ?? 0m,
                Stock = c.InStock ?? 0,
                Status = MapStatusToText(c.CommodityStatusId),
                Image = MediaHelper.NormalizeImageUrl(c.ImageUrl),
                UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                NetWeight = netWeight,
                WeightUnit = weightUnit,
                ProductType = c.ProductType,
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

        return new ProductDetailDto
        {
            Id = commodity.CommodityId.ToString(),
            Name = commodity.ProductName,
            Price = commodity.UnitPrice ?? 0m,
            Stock = commodity.InStock ?? 0,
            Status = MapStatusToText(commodity.CommodityStatusId),
            Image = MediaHelper.NormalizeImageUrl(commodity.ImageUrl),
            CoverImage = MediaHelper.NormalizeImageUrl(commodity.ImageUrl),
            CarouselMedia = carouselList,
            NetWeight = netWeight,
            WeightUnit = weightUnit,
            StorageCondition = commodity.StorageCondition,
            SpecImages = specList,
            Description = commodity.SpecDescription,
            UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            ProductType = commodity.ProductType,
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
            CommodityStatusId = MapStatusToId(dto.Status),
            ImageUrl = MediaHelper.ProcessImageData(dto.CoverImage, _env.WebRootPath),
            StorageCondition = dto.StorageCondition,
            SpecDescription = dto.Description,
            WeightText = BuildWeightText(dto.NetWeight, dto.WeightUnit),
            UnitId = dto.UnitId,
            CategoryId = 1,
            ProductType = dto.ProductType,
        };

        _dbContext.Commodities.Add(commodity);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        commodity.ProductName = dto.Name;
        commodity.UnitPrice = dto.Price;
        commodity.InStock = dto.Stock;
        commodity.Quantity = dto.Stock;
        commodity.CommodityStatusId = MapStatusToId(dto.Status);
        commodity.ImageUrl = MediaHelper.ProcessImageData(dto.CoverImage, _env.WebRootPath);
        commodity.StorageCondition = dto.StorageCondition;
        commodity.SpecDescription = dto.Description;
        commodity.WeightText = BuildWeightText(dto.NetWeight, dto.WeightUnit);
        commodity.UnitId = dto.UnitId;
        commodity.ProductType = dto.ProductType;

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

    public async Task<List<CommodityCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<CommodityCategory>()
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
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

    private static string MapStatusToText(int? statusId)
    {
        return statusId switch
        {
            1 => "已上架",
            3 => "已售空",
            _ => "已下架"
        };
    }

}
