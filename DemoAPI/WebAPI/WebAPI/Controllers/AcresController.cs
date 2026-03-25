using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/acres")]
public class AcresController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AcresController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("index")]
    public async Task<ActionResult<ApiResult>> GetPageData(CancellationToken cancellationToken)
    {
        var projects = await LoadProjectsAsync(cancellationToken);
        var swiperList = await LoadSwiperListAsync(cancellationToken);
        var items = projects.Select(MapListItem).ToList();

        return Ok(ApiResult.Success(new
        {
            swiperList,
            list = items,
            items
        }));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult>> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadProjectsAsync(cancellationToken);
        var filteredItems = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            filteredItems = filteredItems.Where(x => x.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var resultItems = filteredItems.ToList();
        var result = new AcreListResponseDto
        {
            PageIndex = pageIndex <= 0 ? 1 : pageIndex,
            PageSize = pageSize <= 0 ? 10 : pageSize,
            Total = resultItems.Count,
            Items = resultItems
        };

        return Ok(ApiResult.Success(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult>> GetDetail(string id, CancellationToken cancellationToken)
    {
        if (!long.TryParse(id, out var projectId))
        {
            return Ok(ApiResult.Fail("id 参数不正确", 400));
        }

        var project = await _dbContext.AcreProjects
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.AcreProjectId == projectId)
            .Select(x => new
            {
                x.AcreProjectId,
                x.Name,
                x.Description,
                x.Price,
                x.ImageUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return Ok(ApiResult.Fail("认购项目不存在", 404));
        }

        var swiperList = await _dbContext.AcreProjectImages
            .AsNoTracking()
            .Where(x => x.AcreProjectId == projectId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                id = x.Id,
                image = x.ImageUrl
            })
            .ToListAsync(cancellationToken);

        if (swiperList.Count == 0 && !string.IsNullOrWhiteSpace(project.ImageUrl))
        {
            swiperList.Add(new
            {
                id = project.AcreProjectId,
                image = project.ImageUrl
            });
        }

        return Ok(ApiResult.Success(new
        {
            id = project.AcreProjectId.ToString(),
            name = project.Name,
            status = "available",
            price = FormatPrice(project.Price),
            image = project.ImageUrl,
            description = project.Description,
            swiperList
        }));
    }

    [HttpPost("{id}/adopt")]
    public ActionResult<ApiResult> Adopt(string id, [FromBody] object? body)
    {
        return Ok(ApiResult.Success(new
        {
            acreId = id,
            adopted = true
        }));
    }

    [HttpGet("{id}/logs")]
    public ActionResult<ApiResult> Logs(string id)
    {
        var result = new AcreLogsResponseDto
        {
            Logs =
            [
                new AcreLogDto { Time = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss"), Action = "播种" },
                new AcreLogDto { Time = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd HH:mm:ss"), Action = "浇水" },
                new AcreLogDto { Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Action = "施肥" }
            ]
        };

        return Ok(ApiResult.Success(result));
    }

    private async Task<List<AcreDto>> LoadProjectsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.AcreProjects
            .AsNoTracking()
            .Where(x => x.Status == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AcreProjectId)
            .Select(x => new AcreDto
            {
                Id = x.AcreProjectId.ToString(),
                Name = x.Name,
                Status = "available",
                Price = FormatPrice(x.Price),
                Image = x.ImageUrl,
                Description = x.Description
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<object>> LoadSwiperListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Carousels
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.Position == "acres")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => new
            {
                id = x.CarouselId,
                image = x.ImageUrl,
                title = x.Title,
                linkUrl = x.LinkUrl ?? string.Empty
            })
            .Cast<object>()
            .ToListAsync(cancellationToken);
    }

    private static object MapListItem(AcreDto acre)
    {
        return new
        {
            id = acre.Id,
            name = acre.Name,
            description = acre.Description,
            price = acre.Price,
            image = acre.Image,
            status = acre.Status
        };
    }

    private static string FormatPrice(decimal price)
    {
        return $"{price:0.##}元/亩";
    }
}
