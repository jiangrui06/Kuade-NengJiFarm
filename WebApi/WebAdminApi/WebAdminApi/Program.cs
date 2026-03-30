using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebAdminApi.Configuration;
using WebAdminApi.DBs;
using WebAdminApi.Services;
using WebAdminApi.Middleware;

namespace WebAdminApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 注册服务
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // ========== JWT 配置 ==========
            // 从 appsettings.json 读取 JWT 配置
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

            // 验证 JWT 配置是否存在
            var jwtSection = builder.Configuration.GetSection("Jwt");
            if (jwtSection.Exists())
            {
                var jwtSettings = jwtSection.Get<JwtSettings>();
                if (jwtSettings != null && !string.IsNullOrEmpty(jwtSettings.SecretKey))
                {
                    // JWT 配置已成功加载
                }
            }
            // ========== JWT 配置结束 ==========

            // 服务注册
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IUserService, UserService>();

            // DbContext 配置
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    mysqlOptions => mysqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(1),
                        errorNumbersToAdd: new[] { 1040, 1041, 1205 }
                    )
                );
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // 注册中间件
            app.UseMiddleware<TokenMiddleware>();

            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
