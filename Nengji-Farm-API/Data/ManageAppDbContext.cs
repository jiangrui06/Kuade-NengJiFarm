using Microsoft.EntityFrameworkCore;

using WebAPI.Entities.Manage;

namespace WebAPI.Data;

public class ManageAppDbContext : DbContext
{
    public ManageAppDbContext(DbContextOptions<ManageAppDbContext> options) : base(options)
    {
    }

    public DbSet<CommodityMaterial> CommodityMaterials => Set<CommodityMaterial>();
    public DbSet<ActivityOrderDetail> ActivityOrderDetails => Set<ActivityOrderDetail>();
    public DbSet<ActivityMaterial> ActivityMaterials => Set<ActivityMaterial>();
    public DbSet<DiningTables> DiningTables => Set<DiningTables>();
    public DbSet<DiningTableStatusDict> DiningTableStatusDicts => Set<DiningTableStatusDict>();
    public DbSet<DishOrderStatus> DishOrderStatuses => Set<DishOrderStatus>();
    public DbSet<DishOrderDetails> DishOrderDetails => Set<DishOrderDetails>();
    public DbSet<DishOrders> DishOrders => Set<DishOrders>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
    public DbSet<Commodity> Commodities => Set<Commodity>();
    public DbSet<CommodityImage> CommodityImages => Set<CommodityImage>();
    public DbSet<CommodityOrder> CommodityOrders => Set<CommodityOrder>();
    public DbSet<CommodityOrderDetail> CommodityOrderDetails => Set<CommodityOrderDetail>();
    public DbSet<CommodityOrderStatus> CommodityOrderStatuses => Set<CommodityOrderStatus>();
    public DbSet<CommodityTagRelation> CommodityTagRelations => Set<CommodityTagRelation>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<OrderFood> OrderFoods => Set<OrderFood>();
    public DbSet<MealsOrderDetail> MealsOrderDetails => Set<MealsOrderDetail>();
    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();
    public DbSet<RefundRecord> RefundRecords => Set<RefundRecord>();
    public DbSet<TrackingType> TrackingTypes => Set<TrackingType>();
    public DbSet<ShippingCart> ShippingCarts => Set<ShippingCart>();
    public DbSet<Carousel> Carousels => Set<Carousel>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<DishImage> DishImages => Set<DishImage>();
    public DbSet<DishStatus> DishStatuses => Set<DishStatus>();
    public DbSet<DishCategory> DishCategories => Set<DishCategory>();

    // TODO: The following entities are not yet in ManageAPI. Add them when needed.
    // public DbSet<AcreProject> AcreProjects => Set<AcreProject>();
    // public DbSet<AcreProjectImage> AcreProjectImages => Set<AcreProjectImage>();

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

        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.OrderId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(x => x.OrderDetailsId);
            entity.Property(x => x.OrderDetailsId).ValueGeneratedOnAdd();
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

        modelBuilder.Entity<ShippingAddress>(entity =>
        {
            entity.HasKey(x => x.AddressId);
            entity.Property(x => x.AddressId).ValueGeneratedOnAdd();
            entity.Property<bool>("IsDefault").HasColumnName("is_default").HasDefaultValue(false);
        });

        modelBuilder.Entity<DishImage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<DishStatus>(entity =>
        {
            entity.HasKey(x => x.DishStatusId);
        });

        modelBuilder.Entity<DishCategory>(entity =>
        {
            entity.HasKey(x => x.DishCategoryId);
            entity.Property(x => x.DishCategoryId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<DiningTableStatusDict>(entity =>
        {
            entity.HasKey(x => x.TableStatusId);
        });
    }
}
