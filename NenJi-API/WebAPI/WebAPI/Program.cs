using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Middleware;
using WebAPI.Options;
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
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<WeChatPayOptions>(builder.Configuration.GetSection(WeChatPayOptions.SectionName));
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");
        var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
        Directory.CreateDirectory(dataProtectionPath);

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
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AdminCors", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Farm Mini Program API",
                Version = "v1",
                Description = "能记农场小程序后端接口文档"
            });
            options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", ".", StringComparison.Ordinal));

            // 集成 XML 注释文档
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // 配置Swagger生成选项，忽略JsonIgnore属性
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

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is missing.");
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        });

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
        builder.Services.AddScoped<JwtHelper>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAppService, AppService>();
        builder.Services.AddScoped<IInventoryStatsService, InventoryStatsService>();
        builder.Services.AddScoped<IInventoryService, InventoryService>();
        builder.Services.AddHttpClient<IWeChatPayService, WeChatPayService>();

        builder.Services.AddSingleton<IContentService, ContentService>();
        builder.Services.AddHttpContextAccessor();
        //builder.Services.AddScoped<AppDataSeeder>();


        var app = builder.Build();

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
                    "{\"1\":\"待付款\",\"2\":\"待发货\",\"3\":\"运输中\",\"4\":\"已完成\",\"5\":\"已取消\",\"6\":\"退款中\",\"7\":\"已退款\"}",
                    "商品订单状态名称映射");
                SeedConfig(db, "activity_order_status_names",
                    "{\"1\":\"待付款\",\"2\":\"待核销\",\"3\":\"已核销\",\"4\":\"已取消\",\"5\":\"退款中\",\"6\":\"已退款\"}",
                    "活动订单状态名称映射");
            }
            catch
            {
                // sys_config 表初始化失败，回退到硬编码默认值
            }
        }

        //using (var scope = app.Services.CreateScope())
        //{
        //    var seeder = scope.ServiceProvider.GetRequiredService<AppDataSeeder>();
        //    seeder.SeedAsync().GetAwaiter().GetResult();
        //}

        app.UseMiddleware<GlobalExceptionMiddleware>();
        //app.UseMiddleware<DemoCartMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }
        app.UseCors("AdminCors");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.UseStaticFiles(); // 启用静态文件服务

        var farmImagesPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "farm");
        if (Directory.Exists(farmImagesPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(farmImagesPath),
                RequestPath = "/images/farm"
            });
        }

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
            // 配置初始化失败，跳过
        }
    }

}
