namespace WebAPI.Services;

using Microsoft.EntityFrameworkCore;

using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Entities;

/// <summary>
/// 产品管理服务
/// </summary>
public class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;

    public ProductService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取产品列表
    /// </summary>
    public async Task<(List<ProductListItemDto> Records, int Total)> GetProductListAsync(
        int pageNum,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Commodities.AsNoTracking();

        // 搜索过滤：按ID或名称模糊搜索
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
                Status = (c.CommodityStatusId ?? 0) == 1 ? "已上架" : "已下架",
                Image = c.ImageUrl ?? string.Empty,
                //UploadTime = c.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync(cancellationToken);

        return (records, total);
    }

    /// <summary>
    /// 获取产品详情
    /// </summary>
    public async Task<ProductDetailDto?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var commodity = await _dbContext.Commodities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CommodityId == id, cancellationToken);

        if (commodity is null)
        {
            return null;
        }

        // 加载素材数据
        var materials = await _dbContext.CommodityMaterials
            .AsNoTracking()
            .Where(m => m.CommodityId == id)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);

        // 分类素材
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
                    //Thumb = material.ThumbUrl
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
                    //Thumb = material.ThumbUrl
                });
            }
        }

        return new ProductDetailDto
        {
            Id = commodity.CommodityId.ToString(),
            Name = commodity.ProductName,
            Price = commodity.UnitPrice ?? 0m,
            Stock = commodity.InStock ?? 0,
            Status = (commodity.CommodityStatusId ?? 0) == 1 ? "已上架" : "已下架",
            Image = commodity.ImageUrl ?? string.Empty,
            CoverImage = commodity.ImageUrl ?? string.Empty,
            CarouselMedia = carouselMedia.Take(5).ToList(),
            //NetWeight = commodity.NetWeight,
            //WeightUnit = commodity.WeightUnit,
            StorageCondition = commodity.StorageCondition,
            SpecImages = specImages.Take(5).ToList(),
            Description = commodity.SpecDescription,
            //UploadTime = commodity.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    /// <summary>
    /// 新增产品
    /// </summary>
    public async Task<int> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        var commodity = new Commodity
        {
            ProductName = dto.Name,
            UnitPrice = dto.Price,
            InStock = dto.Stock,
            CommodityStatusId = dto.Status == "已上架" ? 1 : 0,
            ImageUrl = dto.CoverImage,
            //NetWeight = dto.NetWeight,
            //WeightUnit = dto.WeightUnit,
            StorageCondition = dto.StorageCondition,
            SpecDescription = dto.Description,
            CategoryId = 1, // 默认分类
            //CreatedAt = DateTime.UtcNow
        };

        _dbContext.Commodities.Add(commodity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 保存轮播图和规格图到CommodityMaterial
        var materialsToAdd = new List<CommodityMaterial>();

        if (dto.CarouselMedia.Count > 0)
        {
            var carouselMaterials = dto.CarouselMedia
                .Select((m, index) => new CommodityMaterial
                {
                    CommodityId = commodity.CommodityId,
                    MaterialType = "carousel",
                    MaterialUrl = m.Url,
                    //ThumbUrl = m.Thumb,
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            materialsToAdd.AddRange(carouselMaterials);
        }

        if (dto.SpecImages.Count > 0)
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

    /// <summary>
    /// 编辑产品
    /// </summary>
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
        commodity.CommodityStatusId = dto.Status == "已上架" ? 1 : 0;
        commodity.ImageUrl = dto.CoverImage;
        //commodity.NetWeight = dto.NetWeight;
        //commodity.WeightUnit = dto.WeightUnit;
        commodity.StorageCondition = dto.StorageCondition;
        commodity.SpecDescription = dto.Description;

        // 删除旧的素材数据
        var oldMaterials = await _dbContext.CommodityMaterials
            .Where(m => m.CommodityId == dto.Id)
            .ToListAsync(cancellationToken);

        _dbContext.CommodityMaterials.RemoveRange(oldMaterials);

        // 添加新的素材数据
        var materialsToAdd = new List<CommodityMaterial>();

        if (dto.CarouselMedia.Count > 0)
        {
            var carouselMaterials = dto.CarouselMedia
                .Select((m, index) => new CommodityMaterial
                {
                    CommodityId = commodity.CommodityId,
                    MaterialType = "carousel",
                    MaterialUrl = m.Url,
                    //ThumbUrl = m.Thumb,
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            materialsToAdd.AddRange(carouselMaterials);
        }

        if (dto.SpecImages.Count > 0)
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

        _dbContext.Commodities.Update(commodity);

        if (materialsToAdd.Count > 0)
        {
            _dbContext.CommodityMaterials.AddRange(materialsToAdd);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// 删除产品
    /// </summary>
    public async Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var commodity = await _dbContext.Commodities
            .FirstOrDefaultAsync(c => c.CommodityId == id, cancellationToken);

        if (commodity is null)
        {
            return false;
        }

        // 删除关联的素材
        var materials = await _dbContext.CommodityMaterials
            .Where(m => m.CommodityId == id)
            .ToListAsync(cancellationToken);

        _dbContext.CommodityMaterials.RemoveRange(materials);

        // 兼容旧数据：删除CommodityImage
        var oldImages = await _dbContext.CommodityImages
            .Where(i => i.CommodityId == id)
            .ToListAsync(cancellationToken);

        _dbContext.CommodityImages.RemoveRange(oldImages);

        _dbContext.Commodities.Remove(commodity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// 批量删除产品
    /// </summary>
    public async Task<bool> DeleteProductBatchAsync(int[] ids, CancellationToken cancellationToken = default)
    {
        var commodities = await _dbContext.Commodities
            .Where(c => ids.Contains(c.CommodityId))
            .ToListAsync(cancellationToken);

        if (commodities.Count == 0)
        {
            return false;
        }

        var commodityIds = commodities.Select(c => c.CommodityId).ToList();

        // 删除关联的素材
        var materials = await _dbContext.CommodityMaterials
            .Where(m => commodityIds.Contains(m.CommodityId))
            .ToListAsync(cancellationToken);

        _dbContext.CommodityMaterials.RemoveRange(materials);

        // 兼容旧数据：删除CommodityImage
        var oldImages = await _dbContext.CommodityImages
            .Where(i => commodityIds.Contains(i.CommodityId ?? 0))
            .ToListAsync(cancellationToken);

        _dbContext.CommodityImages.RemoveRange(oldImages);

        _dbContext.Commodities.RemoveRange(commodities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// 判断URL是否为视频
    /// </summary>
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