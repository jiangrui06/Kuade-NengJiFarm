using Microsoft.EntityFrameworkCore;

using WebAPI.Entities;

namespace WebAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }


    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();

    public DbSet<Carousel> Carousels => Set<Carousel>();

    public DbSet<Commodity> Commodities => Set<Commodity>();

    public DbSet<CommodityImage> CommodityImages => Set<CommodityImage>();

    public DbSet<CommodityTagRelation> CommodityTagRelations => Set<CommodityTagRelation>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Dish> Dishes => Set<Dish>();

    public DbSet<DishCategory> DishCategories => Set<DishCategory>();

    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<DishOrderStatus> DishOrderStatuses => Set<DishOrderStatus>();

    public DbSet<ShippingCart> ShippingCarts => Set<ShippingCart>();

    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();

    public DbSet<CommodityOrder> CommodityOrders => Set<CommodityOrder>();

    public DbSet<CommodityOrderDetail> CommodityOrderDetails => Set<CommodityOrderDetail>();

    public DbSet<DishOrder> DishOrders => Set<DishOrder>();

    public DbSet<DishOrderDetail> DishOrderDetails => Set<DishOrderDetail>();

    public DbSet<ActivityOrder> ActivityOrders => Set<ActivityOrder>();

    public DbSet<ActivityOrderDetail> ActivityOrderDetails => Set<ActivityOrderDetail>();

    public DbSet<OrderFood> OrderFoods => Set<OrderFood>();

    public DbSet<MealsOrderDetail> MealsOrderDetails => Set<MealsOrderDetail>();

    public DbSet<Video> Videos => Set<Video>();

    public DbSet<ActivityVerificationRecord> ActivityVerificationRecords => Set<ActivityVerificationRecord>();

    public DbSet<ActivityTypeEntity> ActivityTypes => Set<ActivityTypeEntity>();

    public DbSet<RefundRecord> RefundRecords => Set<RefundRecord>();

    public DbSet<SysConfig> SysConfigs => Set<SysConfig>();

    public DbSet<UserPoints> UserPoints => Set<UserPoints>();

    public DbSet<PointsRecord> PointsRecords => Set<PointsRecord>();

    public DbSet<PointsExchange> PointsExchanges => Set<PointsExchange>();

    public DbSet<PointsRule> PointsRules => Set<PointsRule>();

    public DbSet<PointsCommodity> PointsCommodities => Set<PointsCommodity>();

    public DbSet<PointsCommodityOrderStatus> PointsCommodityOrderStatuses => Set<PointsCommodityOrderStatus>();

    public DbSet<PointsCommodityStatus> PointsCommodityStatuses => Set<PointsCommodityStatus>();

    public DbSet<PointsCommodityImage> PointsCommodityImages => Set<PointsCommodityImage>();

    public DbSet<CommodityVerifyRecord> CommodityVerifyRecords => Set<CommodityVerifyRecord>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<ActivityMaterial> ActivityMaterials => Set<ActivityMaterial>();
    public DbSet<DishImage> DishImages => Set<DishImage>();
    public DbSet<AcreProject> AcreProjects => Set<AcreProject>();
    public DbSet<AcreProjectImage> AcreProjectImages => Set<AcreProjectImage>();

    //public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();

    //public DbSet<Coupon> Coupons => Set<Coupon>();

    //public DbSet<SubscriptionFarm> SubscriptionFarms => Set<SubscriptionFarm>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(x => x.RoleId);
            entity.Property(x => x.RoleId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ActivityEntity>(entity =>
        {
            entity.HasKey(x => x.ActivityId);
            entity.Property(x => x.ActivityId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Carousel>(entity =>
        {
            entity.HasKey(x => x.CarouselId);
            entity.Property(x => x.CarouselId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Commodity>(entity =>
        {
            entity.HasKey(x => x.CommodityId);
            entity.Property(x => x.CommodityId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<CommodityImage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<CommodityTagRelation>(entity =>
        {
            entity.HasKey(x => x.CommodityTagRelationId);
            entity.Property(x => x.CommodityTagRelationId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(x => x.TagId);
            entity.Property(x => x.TagId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Dish>(entity =>
        {
            entity.HasKey(x => x.DishId);
            entity.Property(x => x.DishId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<DishCategory>(entity =>
        {
            entity.HasKey(x => x.DishCategoryId);
            entity.Property(x => x.DishCategoryId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<DiningTable>(entity =>
        {
            entity.HasKey(x => x.DiningTableId);
            entity.Property(x => x.DiningTableId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ShippingCart>(entity =>
        {
            entity.HasKey(x => x.ShippingCartId);
            entity.Property(x => x.ShippingCartId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ShippingAddress>(entity =>
        {
            entity.HasKey(x => x.AddressId);
            entity.Property(x => x.AddressId).ValueGeneratedOnAdd();
            entity.Property<bool>("IsDefault").HasColumnName("is_default").HasDefaultValue(false);
        });

        modelBuilder.Entity<CommodityOrder>(entity =>
        {
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.OrderId).ValueGeneratedOnAdd();
            entity.HasIndex(x => x.OrderNo).IsUnique();
        });

        modelBuilder.Entity<CommodityOrderDetail>(entity =>
        {
            entity.HasKey(x => x.CommodityOrderDetailsId);
            entity.Property(x => x.CommodityOrderDetailsId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<DishOrder>(entity =>
        {
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.OrderId).ValueGeneratedOnAdd();
            entity.HasIndex(x => x.OrderNo).IsUnique();
        });

        modelBuilder.Entity<DishOrderDetail>(entity =>
        {
            entity.HasKey(x => x.DishOrderDetailsId);
            entity.Property(x => x.DishOrderDetailsId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ActivityOrder>(entity =>
        {
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.OrderId).ValueGeneratedOnAdd();
            entity.HasIndex(x => x.OrderNo).IsUnique();
        });

        modelBuilder.Entity<ActivityOrderDetail>(entity =>
        {
            entity.HasKey(x => x.ActivityOrderDetailsId);
            entity.Property(x => x.ActivityOrderDetailsId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<OrderFood>(entity =>
        {
            entity.HasKey(x => x.OrderFoodId);
            entity.Property(x => x.OrderFoodId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<MealsOrderDetail>(entity =>
        {
            entity.HasKey(x => x.MealsOrderDetailsId);
            entity.Property(x => x.MealsOrderDetailsId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Video>(entity =>
        {
            entity.HasKey(x => x.VideoId);
            entity.Property(x => x.VideoId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ActivityTypeEntity>(entity =>
        {
            entity.HasKey(x => x.ActivityTypeId);
            entity.Property(x => x.ActivityTypeId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<RefundRecord>(entity =>
        {
            entity.HasKey(x => x.RefundId);
            entity.Property(x => x.RefundId).ValueGeneratedOnAdd();
            entity.Property(x => x.Images).HasColumnType("json");
        });

        modelBuilder.Entity<SysConfig>(entity =>
        {
            entity.HasKey(x => x.ConfigId);
            entity.Property(x => x.ConfigId).ValueGeneratedOnAdd();
            entity.HasIndex(x => x.ConfigKey).IsUnique();
        });

        modelBuilder.Entity<UserPoints>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<PointsRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PointsExchange>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.HasIndex(x => x.OrderNo).IsUnique();
            entity.HasIndex(x => x.UserId).HasDatabaseName("idx_user_id");
            entity.HasIndex(x => x.StatusId).HasDatabaseName("idx_status");
            entity.HasIndex(x => x.CommodityId).HasDatabaseName("idx_commodity_id");
        });

        modelBuilder.Entity<CommodityVerifyRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        //modelBuilder.Entity<AdminAccount>(entity =>
        //{
        //    entity.HasKey(x => x.AdminId);
        //    entity.Property(x => x.AdminId).ValueGeneratedOnAdd();
        //});

        //modelBuilder.Entity<Coupon>(entity =>
        //{
        //    entity.HasKey(x => x.CouponId);
        //    entity.Property(x => x.CouponId).ValueGeneratedOnAdd();
        //});

        //modelBuilder.Entity<SubscriptionFarm>(entity =>
        //{
        //    entity.HasKey(x => x.SubscriptionFarmId);
        //    entity.Property(x => x.SubscriptionFarmId).ValueGeneratedOnAdd();
        //});
    }
}
