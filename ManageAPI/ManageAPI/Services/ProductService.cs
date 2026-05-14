namespace ManageAPI.Services;

using Microsoft.EntityFrameworkCore;

using ManageAPI.Data;
using ManageAPI.Dtos;
using ManageAPI.Entity;

public class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;

    public ProductService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(List<ProductListItemDto> Records, int Total)> GetProductListAsync(
        int pageNum,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Commodities.AsNoTracking();

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
            .Select(c => new ProductListItemDto
            {
                Id = c.CommodityId.ToString(),
                Name = c.ProductName ?? string.Empty,
                Price = c.UnitPrice ?? 0m,
                Stock = c.InStock ?? 0,
                Status = MapStatusToText(c.CommodityStatusId),
                Image = c.ImageUrl ?? string.Empty,
                UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            })
            .ToListAsync(cancellationToken);

        return (records, total);
    }

    public async Task<ProductDetailDto?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var commodity = await _dbContext.Commodities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CommodityId == id, cancellationToken);

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
            if (material.MaterialType == "carousel")
            {
                carouselMedia.Add(new CarouselMediaDto
                {
                    Type = IsVideoUrl(material.MaterialUrl) ? "video" : "image",
                    Url = material.MaterialUrl ?? string.Empty,
                });
            }
            else if (material.MaterialType == "spec")
            {
                specImages.Add(material.MaterialUrl ?? string.Empty);
            }
            else if (material.MaterialType == "video")
            {
                carouselMedia.Add(new CarouselMediaDto
                {
                    Type = "video",
                    Url = material.MaterialUrl ?? string.Empty,
                });
            }
        }

        var (netWeight, weightUnit) = ParseWeightText(commodity.WeightText);

        return new ProductDetailDto
        {
            Id = commodity.CommodityId.ToString(),
            Name = commodity.ProductName,
            Price = commodity.UnitPrice ?? 0m,
            Stock = commodity.InStock ?? 0,
            Status = MapStatusToText(commodity.CommodityStatusId),
            Image = commodity.ImageUrl ?? string.Empty,
            CoverImage = commodity.ImageUrl ?? string.Empty,
            CarouselMedia = carouselMedia.Take(5).ToList(),
            NetWeight = netWeight,
            WeightUnit = weightUnit,
            StorageCondition = commodity.StorageCondition,
            SpecImages = specImages.Take(5).ToList(),
            Description = commodity.SpecDescription,
            UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };
    }

    public async Task<int> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        var commodity = new Commodity
        {
            ProductName = dto.Name,
            UnitPrice = dto.Price,
            InStock = dto.Stock,
            Quantity = 0,
            CommodityStatusId = MapStatusToId(dto.Status),
            ImageUrl = dto.CoverImage,
            StorageCondition = dto.StorageCondition,
            SpecDescription = dto.Description,
            WeightText = BuildWeightText(dto.NetWeight, dto.WeightUnit),
            CategoryId = 1,
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
                    MaterialType = "carousel",
                    MaterialUrl = m.Url,
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
                    MaterialType = "spec",
                    MaterialUrl = url,
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
        commodity.CommodityStatusId = MapStatusToId(dto.Status);
        commodity.ImageUrl = dto.CoverImage;
        commodity.StorageCondition = dto.StorageCondition;
        commodity.SpecDescription = dto.Description;
        commodity.WeightText = BuildWeightText(dto.NetWeight, dto.WeightUnit);

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
                    MaterialType = "carousel",
                    MaterialUrl = m.Url,
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
                    MaterialType = "spec",
                    MaterialUrl = url,
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
            .FirstOrDefaultAsync(c => c.CommodityId == id, cancellationToken);

        if (commodity is null)
        {
            return false;
        }

        commodity.CommodityStatusId = 2; // 已下架
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteProductBatchAsync(int[] ids, CancellationToken cancellationToken = default)
    {
        var commodities = await _dbContext.Commodities
            .Where(c => ids.Contains(c.CommodityId))
            .ToListAsync(cancellationToken);

        if (commodities.Count == 0)
        {
            return false;
        }

        foreach (var commodity in commodities)
        {
            commodity.CommodityStatusId = 2; // 已下架
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string? BuildWeightText(decimal? netWeight, string? weightUnit)
    {
        if (netWeight == null || netWeight == 0)
            return null;

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
        return status switch
        {
            "已上架" => 1,
            "已售空" => 3,
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

    private static bool IsVideoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
        var extension = Path.GetExtension(url).ToLowerInvariant();
        return videoExtensions.Contains(extension);
    }
}
