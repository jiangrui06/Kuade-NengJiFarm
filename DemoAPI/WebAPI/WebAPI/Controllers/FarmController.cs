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
            .Where(x => x.Status == 1 && x.Position == "home")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => x.ImageUrl)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(mainImage))
        {
            mainImage = await _dbContext.Videos
                .AsNoTracking()
                .Where(x => x.Status == 1)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.VideoId)
                .Select(x => x.CoverUrl)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var features = await _dbContext.Videos
            .AsNoTracking()
            .Where(x => x.Status == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.VideoId)
            .Take(3)
            .Select(x => new
            {
                id = x.VideoId,
                name = x.Title,
                image = x.CoverUrl
            })
            .ToListAsync(cancellationToken);

        var data = new
        {
            mainImage = mainImage ?? string.Empty,
            introduction = "我们的农场位于风景秀丽的乡村，占地面积超过300亩，是一家集种植、养殖、休闲观光于一体的现代化生态农场。农场采用绿色环保的种植方式，严格控制投入品使用，确保农产品安全、健康、可\n追溯。",
            philosophy = "我们秉承“自然、健康、可持续”的发展理念，致力于为消费者提供更优质的农产品。通过科学管理和生态种养结合，我们不仅提升了农产品品质，也尽可能保护土壤与水源环境，推动农场长期稳定发\n展。",
            contact = new
            {
                address = "地址:   中国江苏省南京市溧水区能记农场",
                phone = "138-1234-5678",
                email = "info@nengjifarm.com"
            },
            features
        };

        return Ok(ApiResult.Success(data));
    }
}
