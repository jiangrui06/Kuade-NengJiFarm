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

    public DbSet<Commodity> Commodities => Set<Commodity>();

    public DbSet<CommodityImage> CommodityImages => Set<CommodityImage>();

    public DbSet<CommodityTagRelation> CommodityTagRelations => Set<CommodityTagRelation>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Dish> Dishes => Set<Dish>();

    public DbSet<ShippingCart> ShippingCarts => Set<ShippingCart>();

    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

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

        modelBuilder.Entity<ShippingCart>(entity =>
        {
            entity.HasKey(x => x.ShippingCartId);
            entity.Property(x => x.ShippingCartId).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ShippingAddress>(entity =>
        {
            entity.HasKey(x => x.AddressId);
            entity.Property(x => x.AddressId).ValueGeneratedOnAdd();
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
