using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Services;

public class SysConfigInitializer
{
    private readonly AppDbContext _dbContext;

    public SysConfigInitializer(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InitializeAsync()
    {
        var tableExists = await _dbContext.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_config'");

        _dbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS sys_config (
                config_id INT AUTO_INCREMENT PRIMARY KEY,
                config_key VARCHAR(100) NOT NULL UNIQUE,
                config_value LONGTEXT NOT NULL,
                description VARCHAR(500) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        ");

        await SeedIfEmptyAsync();
    }

    private async Task SeedIfEmptyAsync()
    {
        var count = await _dbContext.SysConfigs.CountAsync();
        if (count > 0) return;

        _dbContext.SysConfigs.AddRange(
            new SysConfig
            {
                ConfigKey = "home_function_buttons",
                ConfigValue = """[{"id":1,"name":"农场优选","color":"#4E8B3A","path":"/pages/farm-goods/farm-goods"},{"id":2,"name":"热销菜品","color":"#FF8A3D","path":"/pages/dish/dish"},{"id":3,"name":"活动报名","color":"#2F7D8C","path":"/pages/activity/activity"},{"id":4,"name":"购物车","color":"#C66B3D","path":"/pages/cart/cart"}]""",
                Description = "首页功能按钮配置"
            },
            new SysConfig
            {
                ConfigKey = "search_type_names",
                ConfigValue = """{"goods":"农场优选","dish":"热销菜品","activity":"活动","acre":"认购一亩田"}""",
                Description = "搜索类型显示名称"
            }
        );
        await _dbContext.SaveChangesAsync();
    }
}
