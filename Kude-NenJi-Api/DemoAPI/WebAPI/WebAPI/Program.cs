using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<WeChatPayOptions>(builder.Configuration.GetSection(WeChatPayOptions.SectionName));
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");
        var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
        Directory.CreateDirectory(dataProtectionPath);

        builder.Services.AddControllers();
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

        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

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

            options.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [jwtScheme] = Array.Empty<string>()
            });
        });

        //builder.Services.AddDbContext<AppDbContext>(options =>
        //{
        //    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        //        ?? throw new InvalidOperationException("DefaultConnection is missing.");
        //    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        //});

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            // 获取连接字符串
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is missing.");

            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysqlOptions =>
            {
                mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(1),
                    errorNumbersToAdd: new[] { 1040, 1041, 1205 }
                );
            });
        });

        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IPasswordService, PasswordService>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = false,
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
        builder.Services.AddHttpClient<IWeChatPayService, WeChatPayService>();


        builder.Services.AddSingleton<IContentService, ContentService>();
        builder.Services.AddHttpContextAccessor();
        //builder.Services.AddScoped<AppDataSeeder>();


        var app = builder.Build();

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
        //app.UseHttpsRedirection();

        app.UseCors("AdminCors");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.UseStaticFiles();
        app.UseMiddleware<TokenMiddleware>();

        app.Run();
    }
}

