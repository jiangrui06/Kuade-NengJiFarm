using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/farm")]
public class FarmController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public FarmController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("intro")]
    public async Task<IActionResult> GetIntro(CancellationToken cancellationToken)
    {
        var mainImage = await _dbContext.Carousels
            .AsNoTracking()
            .Where(x => x.Position == "home")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => x.ImageUrl)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(mainImage))
        {
            mainImage = await _dbContext.Videos
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.VideoUrl))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.VideoId)
                .Select(x => x.VideoUrl)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var features = await _dbContext.Videos
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.VideoUrl))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.VideoId)
            .Take(3)
            .Select(x => new
            {
                id = x.VideoId,
                name = string.Empty,
                image = x.VideoUrl
            })
            .ToListAsync(cancellationToken);

        var configs = await _dbContext.SysConfigs
            .AsNoTracking()
            .Where(x => x.ConfigKey == "farm_name"
                || x.ConfigKey == "farm_introduction"
                || x.ConfigKey == "farm_philosophy"
                || x.ConfigKey == "farm_contact"
                || x.ConfigKey == "farm_image")
            .ToDictionaryAsync(x => x.ConfigKey, x => x.ConfigValue, cancellationToken);

        var name = configs.GetValueOrDefault("farm_name") ?? string.Empty;
        var farmImage = configs.GetValueOrDefault("farm_image") ?? string.Empty;
        var introduction = configs.GetValueOrDefault("farm_introduction") ?? string.Empty;
        var philosophy = configs.GetValueOrDefault("farm_philosophy") ?? string.Empty;

        object contact = new FarmContactDto();
        if (configs.TryGetValue("farm_contact", out var contactJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<FarmContactDto>(contactJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                    contact = parsed;
            }
            catch
            {
                // 解析失败，使用默认空值
            }
        }

        var data = new
        {
            name,
            mainImage = !string.IsNullOrEmpty(farmImage) ? farmImage : (mainImage ?? string.Empty),
            introduction,
            philosophy,
            contact,
            features
        };

        return Ok(ApiResult.Success(data));
    }
}
