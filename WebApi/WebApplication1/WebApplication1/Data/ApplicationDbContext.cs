using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Entities;

namespace WebApplication1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Commodity> Commodities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("user");
                b.HasKey(x => x.UserId);
                b.Property(x => x.UserId).HasColumnName("user_id");
                b.Property(x => x.UserNo).HasColumnName("user_no").IsRequired();
                b.Property(x => x.PhoneNumber).HasColumnName("phone_number");
                b.Property(x => x.RegisterTime).HasColumnName("register_time");
                b.Property(x => x.WxOpenId).HasColumnName("wx_openid");
                b.Property(x => x.WxImage).HasColumnName("wx_image");
                b.Property(x => x.WxNickname).HasColumnName("wx_nickname");
                b.Property(x => x.RoleId).HasColumnName("role_id");
            });

            modelBuilder.Entity<Role>(b =>
            {
                b.ToTable("role");
                b.HasKey(x => x.RoleId);
                b.Property(x => x.RoleId).HasColumnName("role_id");
                b.Property(x => x.RoleName).HasColumnName("role_name");
            });

            modelBuilder.Entity<Category>(b =>
            {
                b.ToTable("category");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.CategoryName).HasColumnName("category_name");
                b.Property(x => x.CategoryDescription).HasColumnName("category_description");
                b.Property(x => x.CategoryStatus).HasColumnName("category_status");
                b.Property(x => x.SortOrder).HasColumnName("sort_order");
            });

            modelBuilder.Entity<Commodity>(b =>
            {
                b.ToTable("commodity");
                b.HasKey(x => x.CommodityId);
                b.Property(x => x.CommodityId).HasColumnName("commodity_id");
                b.Property(x => x.ProductName).HasColumnName("product_name");
                b.Property(x => x.SpecDescription).HasColumnName("spec_description");
                b.Property(x => x.InStock).HasColumnName("in_stock");
                b.Property(x => x.Quantity).HasColumnName("quantity");
                b.Property(x => x.ProductStatus).HasColumnName("product_status");
                b.Property(x => x.CategoryId).HasColumnName("category_id");
                b.Property(x => x.ImageUrl).HasColumnName("image_url");

                // the new column holding raw image bytes; make sure the database
                // schema has been updated (e.g. ALTER TABLE ADD image_data LONGBLOB).
                b.Property(x => x.ImageData)
                    .HasColumnName("image_data")
                    .HasColumnType("longblob");
            });
        }
    }
}
