//using Microsoft.EntityFrameworkCore;
//using WebAPI.Data;
//using WebAPI.Entities;

//namespace WebAPI.Services;

//public class AppDataSeeder
//{
//    private readonly AppDbContext _dbContext;
//    private readonly ILogger<AppDataSeeder> _logger;

//    public AppDataSeeder(AppDbContext dbContext, ILogger<AppDataSeeder> logger)
//    {
//        _dbContext = dbContext;
//        _logger = logger;
//    }

//    public async Task SeedAsync(CancellationToken cancellationToken = default)
//    {
//        await SeedRolesAsync(cancellationToken);
//        await SeedCategoriesAsync(cancellationToken);
//        await SeedTagsAsync(cancellationToken);
//        await SeedCommoditiesAsync(cancellationToken);
//        await SeedCommodityImagesAsync(cancellationToken);
//        await SeedCommodityTagRelationsAsync(cancellationToken);
//        await SeedDishesAsync(cancellationToken);
//    }

//    private async Task SeedRolesAsync(CancellationToken cancellationToken)
//    {
//        if (await _dbContext.Roles.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var roles = new[]
//        {
//            new Role
//            {
//                RoleName = "默认角色"
//            }
//        };

//        _dbContext.Roles.AddRange(roles);
//        await _dbContext.SaveChangesAsync(cancellationToken);
//        _logger.LogInformation("已初始化角色数据 {Count} 条", roles.Length);
//    }

//    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
//    {
//        if (await _dbContext.Categories.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var categories = new[]
//        {
//            new Category
//            {
//                CategoryName = "新鲜蔬菜",
//                CategoryDescription = "农场每日采摘的新鲜蔬菜",
//                CategoryStatus = 1,
//                SortOrder = 1
//            },
//            new Category
//            {
//                CategoryName = "优选水果",
//                CategoryDescription = "自然成熟的农场水果",
//                CategoryStatus = 1,
//                SortOrder = 2
//            },
//            new Category
//            {
//                CategoryName = "肉类禽蛋",
//                CategoryDescription = "农场散养禽蛋和肉类",
//                CategoryStatus = 1,
//                SortOrder = 3
//            },
//            new Category
//            {
//                CategoryName = "乳制品",
//                CategoryDescription = "新鲜乳制品",
//                CategoryStatus = 1,
//                SortOrder = 4
//            },
//            new Category
//            {
//                CategoryName = "主食粮油",
//                CategoryDescription = "优选主食粮油",
//                CategoryStatus = 1,
//                SortOrder = 5
//            }
//        };

//        _dbContext.Categories.AddRange(categories);
//        await _dbContext.SaveChangesAsync(cancellationToken);
//        _logger.LogInformation("已初始化商品分类数据 {Count} 条", categories.Length);
//    }

//    private async Task SeedTagsAsync(CancellationToken cancellationToken)
//    {
//        if (await _dbContext.Tags.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var tags = new[]
//        {
//            new Tag { TagName = "新鲜" },
//            new Tag { TagName = "热销" },
//            new Tag { TagName = "农场直供" },
//            new Tag { TagName = "有机" }
//        };

//        _dbContext.Tags.AddRange(tags);
//        await _dbContext.SaveChangesAsync(cancellationToken);
//        _logger.LogInformation("已初始化标签数据 {Count} 条", tags.Length);
//    }

//    private async Task SeedCommoditiesAsync(CancellationToken cancellationToken)
//    {
//        if (await _dbContext.Commodities.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var categoryMap = await _dbContext.Categories
//            .AsNoTracking()
//            .ToDictionaryAsync(x => x.CategoryName, x => x.Id, cancellationToken);

//        var now = DateTime.Now;

//        var commodities = new[]
//        {
//            new Commodity
//            {
//                ProductName = "有机生菜",
//                CategoryId = categoryMap["新鲜蔬菜"],
//                SpecDescription = "脆嫩清甜，适合沙拉和清炒",
//                InStock = 50,
//                Quantity = 500,
//                ProductStatus = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1622206151226-18ca2c9ab4a1?auto=format&fit=crop&w=600&q=80",
//                UnitPrice = 12.8m,
//                CreatedAt = now.AddDays(-5),
//                StorageCondition = "冷藏保存",
//                WeightUnit = "g"
//            },
//            new Commodity
//            {
//                ProductName = "农家西红柿",
//                CategoryId = categoryMap["新鲜蔬菜"],
//                SpecDescription = "自然成熟，酸甜多汁",
//                InStock = 60,
//                Quantity = 500,
//                ProductStatus = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1546094096-0df4bcaaa337?auto=format&fit=crop&w=600&q=80",
//                UnitPrice = 9.9m,
//                CreatedAt = now.AddDays(-4),
//                StorageCondition = "常温通风",
//                WeightUnit = "g"
//            },
//            new Commodity
//            {
//                ProductName = "红富士苹果",
//                CategoryId = categoryMap["优选水果"],
//                SpecDescription = "果肉清脆，香甜可口",
//                InStock = 80,
//                Quantity = 1000,
//                ProductStatus = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1567306226416-28f0efdc88ce?auto=format&fit=crop&w=600&q=80",
//                UnitPrice = 15.8m,
//                CreatedAt = now.AddDays(-3),
//                StorageCondition = "阴凉保存",
//                WeightUnit = "g"
//            },
//            new Commodity
//            {
//                ProductName = "土猪肉",
//                CategoryId = categoryMap["肉类禽蛋"],
//                SpecDescription = "农场散养土猪，现切配送",
//                InStock = 30,
//                Quantity = 500,
//                ProductStatus = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1607623814075-e51df1bdc82f?auto=format&fit=crop&w=600&q=80",
//                UnitPrice = 38m,
//                CreatedAt = now.AddDays(-3),
//                StorageCondition = "冷冻保存",
//                WeightUnit = "g"
//            },
//            new Commodity
//            {
//                ProductName = "土鸡蛋",
//                CategoryId = categoryMap["肉类禽蛋"],
//                SpecDescription = "散养鸡蛋，营养丰富",
//                InStock = 80,
//                Quantity = 10,
//                ProductStatus = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1506976785307-8732e854ad03?auto=format&fit=crop&w=600&q=80",
//                UnitPrice = 16.8m,
//                CreatedAt = now.AddDays(-2),
//                StorageCondition = "阴凉干燥",
//                WeightUnit = "枚"
//            },
//            new Commodity
//            {
//                ProductName = "鲜牛奶",
//                CategoryId = categoryMap["乳制品"],
//                SpecDescription = "牧场直供鲜牛奶",
//                InStock = 40,
//                Quantity = 500,
//                ProductStatus = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1550583724-b2692b85b150?auto=format&fit=crop&w=600&q=80",
//                UnitPrice = 19.9m,
//                CreatedAt = now.AddDays(-1),
//                StorageCondition = "冷藏保存",
//                WeightUnit = "ml"
//            },
//            new Commodity
//            {
//                ProductName = "农家大米",
//                CategoryId = categoryMap["主食粮油"],
//                SpecDescription = "颗粒饱满，米香浓郁",
//                InStock = 100,
//                Quantity = 5000,
//                ProductStatus = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1586201375761-83865001e31c?auto=format&fit=crop&w=600&q=80",
//                UnitPrice = 49.9m,
//                CreatedAt = now,
//                StorageCondition = "阴凉干燥",
//                WeightUnit = "g"
//            }
//        };

//        _dbContext.Commodities.AddRange(commodities);
//        await _dbContext.SaveChangesAsync(cancellationToken);
//        _logger.LogInformation("已初始化商品数据 {Count} 条", commodities.Length);
//    }

//    private async Task SeedCommodityImagesAsync(CancellationToken cancellationToken)
//    {
//        if (await _dbContext.CommodityImages.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var commodities = await _dbContext.Commodities
//            .AsNoTracking()
//            .ToListAsync(cancellationToken);

//        if (commodities.Count == 0)
//        {
//            return;
//        }

//        var images = commodities.Select(x => new CommodityImage
//        {
//            CommodityId = x.CommodityId,
//            Url = x.ImageUrl,
//            SortOrder = 1,
//            ImageType = 1
//        }).ToList();

//        _dbContext.CommodityImages.AddRange(images);
//        await _dbContext.SaveChangesAsync(cancellationToken);
//        _logger.LogInformation("已初始化商品图片数据 {Count} 条", images.Count);
//    }

//    private async Task SeedCommodityTagRelationsAsync(CancellationToken cancellationToken)
//    {
//        if (await _dbContext.CommodityTagRelations.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var commodities = await _dbContext.Commodities
//            .AsNoTracking()
//            .ToListAsync(cancellationToken);

//        var tagMap = await _dbContext.Tags
//            .AsNoTracking()
//            .ToDictionaryAsync(x => x.TagName, x => x.TagId, cancellationToken);

//        if (commodities.Count == 0 || tagMap.Count == 0)
//        {
//            return;
//        }

//        var relations = new List<CommodityTagRelation>();

//        foreach (var commodity in commodities)
//        {
//            relations.Add(new CommodityTagRelation
//            {
//                CommodityId = commodity.CommodityId,
//                TagId = tagMap["新鲜"]
//            });

//            if (commodity.ProductName == "有机生菜")
//            {
//                relations.Add(new CommodityTagRelation
//                {
//                    CommodityId = commodity.CommodityId,
//                    TagId = tagMap["有机"]
//                });
//            }
//            else if (commodity.ProductName is "土猪肉" or "农家大米" or "鲜牛奶")
//            {
//                relations.Add(new CommodityTagRelation
//                {
//                    CommodityId = commodity.CommodityId,
//                    TagId = tagMap["农场直供"]
//                });
//            }
//            else
//            {
//                relations.Add(new CommodityTagRelation
//                {
//                    CommodityId = commodity.CommodityId,
//                    TagId = tagMap["热销"]
//                });
//            }
//        }

//        _dbContext.CommodityTagRelations.AddRange(relations);
//        await _dbContext.SaveChangesAsync(cancellationToken);
//        _logger.LogInformation("已初始化商品标签关系数据 {Count} 条", relations.Count);
//    }

//    private async Task SeedDishesAsync(CancellationToken cancellationToken)
//    {
//        if (await _dbContext.Dishes.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var now = DateTime.Now;

//        var dishes = new[]
//        {
//            new Dish
//            {
//                DishName = "农家小炒肉",
//                DishDescription = "肥瘦相间，现炒出锅",
//                DishPrice = 32m,
//                DishCategoryId = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1604908554027-8b0dcdc5f10d?auto=format&fit=crop&w=600&q=80",
//                AttributeName = "招牌",
//                Status = 1,
//                LimitedEdition = 100,
//                DishSold = 26,
//                DishRemainingQuantity = 74,
//                UserPurchaseLimit = 5,
//                CreatedAt = now.AddDays(-2)
//            },
//            new Dish
//            {
//                DishName = "番茄牛腩",
//                DishDescription = "汤汁浓郁，适合配饭",
//                DishPrice = 46m,
//                DishCategoryId = 1,
//                ImageUrl = "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=600&q=80",
//                AttributeName = "热销",
//                Status = 1,
//                LimitedEdition = 80,
//                DishSold = 15,
//                DishRemainingQuantity = 65,
//                UserPurchaseLimit = 3,
//                CreatedAt = now.AddDays(-1)
//            }
//        };

//        _dbContext.Dishes.AddRange(dishes);
//        await _dbContext.SaveChangesAsync(cancellationToken);
//        _logger.LogInformation("已初始化菜品数据 {Count} 条", dishes.Length);
//    }
//}