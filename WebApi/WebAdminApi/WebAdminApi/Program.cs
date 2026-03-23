using WebAdminApi.Middleware;
using WebAdminApi.Services;

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
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<ITokenService, TokenService>();

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

    public class TokenMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenMiddleware(RequestDelegate next)
        {
            _next = next; // 仅注入 RequestDelegate
        }

        public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
        {
            // 通过方法参数注入作用域服务
            // tokenService 将从请求作用域中解析
            
            await _next(context);
        }
    }
}
