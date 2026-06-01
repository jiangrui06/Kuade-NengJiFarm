using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using WebAPI.Common;
using WebAPI.Configuration;
using WebAPI.Data;
using WebAPI.Middleware;
using WebAPI.Options;
using WebAPI.PasswordHash;
using WebAPI.Services;

namespace WebAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient("WeChatSecApi")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<WeChatPayOptions>>().Value;
                var handler = new HttpClientHandler();

                // 从应用根目录加载微信退款证书（参考 WxPayController.Refund 模式）
                var certPath = Path.Combine(AppContext.BaseDirectory, "apiclient_cert.p12");
                if (!File.Exists(certPath))
                    certPath = Path.Combine(Directory.GetCurrentDirectory(), "apiclient_cert.p12");

                if (File.Exists(certPath))
                {
                    try
                    {
                        // 密码使用商户号 MchId（参考项目 WxPayController 做法）
                        var cert = new X509Certificate2(
                            certPath,
                            options.MchId,
                            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
                        handler.ClientCertificates.Add(cert);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"加载微信退款证书失败: {ex.Message}", ex);
                    }
                }
                else
                {
                    throw new FileNotFoundException($"微信退款证书未找到: {certPath}");
                }

                return handler;
            });

        // Options
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<WeChatPayOptions>(builder.Configuration.GetSection(WeChatPayOptions.SectionName));

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");

        // Data Protection
        var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
        Directory.CreateDirectory(dataProtectionPath);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));

        // Controllers
        builder.Services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errorMap = context.ModelState
                        .Where(kvp => kvp.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "invalid" : e.ErrorMessage).ToArray());

                    var message = errorMap.Count > 0
                        ? errorMap.First().Value.FirstOrDefault() ?? "invalid request"
                        : "invalid request";

                    return new OkObjectResult(ApiResult.Fail(message, 400, new { errors = errorMap }));
                };
            });

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AdminCors", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        // Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Farm Mini Program API",
                Version = "v1",
                Description = "能记农场小程序后端接口文档（含管理端API）"
            });
            options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", ".", StringComparison.Ordinal));

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            options.SchemaGeneratorOptions = new SchemaGeneratorOptions
            {
                IgnoreObsoleteProperties = true,
                SchemaIdSelector = type => type.FullName?.Replace(".", "_")
            };

            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Input: Bearer {your JWT token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Id = JwtBearerDefaults.AuthenticationScheme,
                    Type = ReferenceType.SecurityScheme
                }
            };

            options.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [jwtScheme] = Array.Empty<string>()
            });
        });

        // DbContext - Mini Program DB (nengjidb_v1)
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is missing.");
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        });

        // DbContext - Management DB (nenji_v2)
        builder.Services.AddDbContext<ManageAppDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("ManageConnection")
                ?? throw new InvalidOperationException("ManageConnection is missing.");
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysqlOptions =>
            {
                mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(1),
                    errorNumbersToAdd: new[] { 1040, 1041, 1205 }
                );
            });
        });

        // JWT Authentication
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();

        // JwtSettings (for TokenService in ManageAPI)
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

        // Register Mini-Program Services (NenJi-API)
        builder.Services.AddScoped<JwtHelper>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAppService, AppService>();
        builder.Services.AddScoped<IInventoryStatsService, InventoryStatsService>();
        builder.Services.AddScoped<IInventoryService, InventoryService>();
        builder.Services.AddHttpClient<IWeChatPayService, WeChatPayService>();
        builder.Services.AddScoped<IPointsService, PointsService>();
        builder.Services.AddScoped<ILogisticsTrackService, LogisticsTrackService>();
        builder.Services.AddHostedService<OrderTimeoutService>();

        // Register Management Services (ManageAPI)
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IUserService, BackUserService>();
        builder.Services.AddScoped<IPasswordService, PasswordService>();
        builder.Services.AddScoped<IKitchenService, KitchenService>();
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddScoped<IDishService, DishService>();
        builder.Services.AddScoped<IActivityService, ActivityService>();
        builder.Services.AddScoped<IDiningTableService, DiningTableService>();
        builder.Services.AddScoped<IDishOrderService, DishOrderService>();
        builder.Services.AddScoped<IProductOrderService, ProductOrderService>();
        builder.Services.AddScoped<IActivityOrderService, ActivityOrderService>();
        builder.Services.AddScoped<ICommonService, CommonService>();

        var app = builder.Build();

        // Seed sys_config table (NenJi-API)
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS sys_config (
                        config_id INT AUTO_INCREMENT PRIMARY KEY,
                        config_key VARCHAR(100) NOT NULL UNIQUE,
                        config_value LONGTEXT NOT NULL,
                        description VARCHAR(500) NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                ");
                SeedConfig(db, "home_function_buttons",
                    "[{\"id\":1,\"name\":\"农场优选\",\"color\":\"#4E8B3A\",\"path\":\"/pages/farm-goods/farm-goods\"},{\"id\":2,\"name\":\"热销菜品\",\"color\":\"#FF8A3D\",\"path\":\"/pages/dish/dish\"},{\"id\":3,\"name\":\"活动报名\",\"color\":\"#2F7D8C\",\"path\":\"/pages/activity/activity\"},{\"id\":4,\"name\":\"购物车\",\"color\":\"#C66B3D\",\"path\":\"/pages/cart/cart\"}]",
                    "首页功能按钮配置");
                SeedConfig(db, "search_type_names",
                    "{\"goods\":\"农场优选\",\"dish\":\"热销菜品\",\"activity\":\"活动\",\"acre\":\"认购一亩田\"}",
                    "搜索类型显示名称");
                SeedConfig(db, "farm_introduction",
                    "我们的农场位于风景秀丽的乡村，占地面积超过300亩，是一家集种植、养殖、休闲观光于一体的现代化生态农场。",
                    "农场介绍");
                SeedConfig(db, "farm_philosophy",
                    "我们秉承“自然、健康、可持续”的发展理念，致力于为消费者提供更优质的农产品。",
                    "农场理念");
                SeedConfig(db, "farm_contact",
                    "{\"address\":\"江苏省南京市溧水区能记农场\",\"phone\":\"138-1234-5678\",\"email\":\"info@nengjifarm.com\"}",
                    "农场联系方式");
                SeedConfig(db, "commodity_order_status_names",
                    "{\"1\":\"待付款\",\"2\":\"待发货\",\"3\":\"运输中\",\"4\":\"已完成\",\"5\":\"已取消\",\"6\":\"退款中\",\"7\":\"已退款\",\"8\":\"待核销\",\"9\":\"已核销\"}",
                    "商品订单状态名称映射");
                SeedConfig(db, "activity_order_status_names",
                    "{\"1\":\"待付款\",\"2\":\"待核销\",\"3\":\"已核销\",\"4\":\"已取消\",\"5\":\"退款中\",\"6\":\"已退款\"}",
                    "活动订单状态名称映射");
                SeedConfig(db, "delivery_method_names",
                    "{\"express\":\"快递配送\",\"pickup\":\"到店自提\"}",
                    "配送方式名称映射");
            }
            catch
            {
                // sys_config table initialization failed
            }

            try
            {
                if (!db.PointsCommodityOrderStatuses.Any())
                {
                    db.PointsCommodityOrderStatuses.AddRange(
                        new Entities.PointsCommodityOrderStatus { Id = 1, StatusName = "待核销" },
                        new Entities.PointsCommodityOrderStatus { Id = 2, StatusName = "已核销" },
                        new Entities.PointsCommodityOrderStatus { Id = 3, StatusName = "已取消" }
                    );
                    db.SaveChanges();
                }
            }
            catch { }

            try
            {
                if (!db.PointsCommodityStatuses.Any())
                {
                    db.PointsCommodityStatuses.AddRange(
                        new Entities.PointsCommodityStatus { Id = 1, StatusName = "active" },
                        new Entities.PointsCommodityStatus { Id = 2, StatusName = "inactive" }
                    );
                    db.SaveChanges();
                }
            }
            catch { }
        }

        // Middleware
        app.UseMiddleware<GlobalExceptionMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseCors("AdminCors");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<DisabledUserMiddleware>();
        app.MapControllers();

        // Static files
        app.UseStaticFiles();

        // Serve legacy /images/farm/ from wwwroot/farm/ (files were saved to wwwroot/farm/
        // but DB stores /images/farm/... paths — this mapping makes both work)
        // Always configure the mapping — directory is created on first upload
        var farmImagesPath = Path.Combine(app.Environment.WebRootPath, "farm");
        Directory.CreateDirectory(farmImagesPath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(farmImagesPath),
            RequestPath = "/images/farm"
        });

        // Serve video thumbnails from wwwroot/thumbs/ via /images/thumbs/
        var thumbsPath = Path.Combine(app.Environment.WebRootPath, "thumbs");
        Directory.CreateDirectory(thumbsPath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(thumbsPath),
            RequestPath = "/images/thumbs"
        });

        // Management static files (icons)
        var iconsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Kude-NenJi-Api", "DemoAPI", "WebAPI", "WebAPI", "wwwroot", "icons"));
        if (Directory.Exists(iconsPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(iconsPath),
                RequestPath = "/icons"
            });
        }

        // Management frontend
        var frontendPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "web_management"));
        if (Directory.Exists(frontendPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(frontendPath),
                RequestPath = ""
            });
        }

        app.UseMiddleware<TokenMiddleware>();

        app.Run();
    }

    private static void SeedConfig(Data.AppDbContext db, string key, string value, string description)
    {
        try
        {
            if (!db.SysConfigs.Any(x => x.ConfigKey == key))
            {
                db.SysConfigs.Add(new Entities.SysConfig
                {
                    ConfigKey = key,
                    ConfigValue = value,
                    Description = description
                });
                db.SaveChanges();
            }
        }
        catch
        {
            // Config initialization failed, skip
        }
    }
}
