using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure DbContext with MySQL
var conn = builder.Configuration.GetConnectionString("Default");
try
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(conn, ServerVersion.AutoDetect(conn)));
}
catch (Exception ex)
{
    Console.WriteLine($"数据库连接配置错误: {ex.Message}");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
