using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/acres")]
public class AcresController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IInventoryStatsService _inventoryStatsService;

    public AcresController(AppDbContext dbContext, IInventoryStatsService inventoryStatsService)
    {
        _dbContext = dbContext;
        _inventoryStatsService = inventoryStatsService;
    }

    [HttpGet("index")]
    public async Task<ActionResult<ApiResult>> GetPageData(CancellationToken cancellationToken)
    {
        var projects = await LoadProjectsAsync(cancellationToken);
        var swiperList = await LoadSwiperListAsync(cancellationToken);

        return Ok(ApiResult.Success(new
        {
            swiperList,
            list = projects,
            items = projects
        }));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult>> GetList(
        [FromQuery] string? status = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var projects = await LoadProjectsAsync(cancellationToken);
        var swiperList = await LoadSwiperListAsync(cancellationToken);

        var filteredItems = projects.AsEnumerable();
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

        return Ok(ApiResult.Success(new
        {
            pageIndex = result.PageIndex,
            pageSize = result.PageSize,
            total = result.Total,
            swiperList,
            list = result.Items,
            items = result.Items
        }));
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
            .Where(x => x.AcreProjectId == projectId && x.Status == 1)
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
            return Ok(ApiResult.Fail("认养项目不存在", 404));
        }

        var detailImageRows = await _dbContext.AcreProjectImages
            .AsNoTracking()
            .Where(x => x.AcreProjectId == projectId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => x.ImageUrl)
            .ToListAsync(cancellationToken);

        var normalizedImages = detailImageRows
            .Select(x => MediaUrlHelper.Normalize(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        var primaryImage = MediaUrlHelper.Normalize(project.ImageUrl);
        if (normalizedImages.Count == 0 && !string.IsNullOrWhiteSpace(primaryImage))
        {
            normalizedImages.Add(primaryImage);
        }

        var bottomImages = EnsureFourImages(normalizedImages, primaryImage);
        var swiperList = normalizedImages
            .Select((image, index) => new
            {
                id = index + 1L,
                image
            })
            .ToList();

        var videoUrlRaw = await _dbContext.Videos
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.VideoId)
            .Select(x => x.VideoUrl)
            .FirstOrDefaultAsync(cancellationToken);

        var videoUrl = MediaUrlHelper.Normalize(videoUrlRaw);
        var acreStats = (await _inventoryStatsService.GetAcreStatsAsync([(int)projectId], cancellationToken))
            .GetValueOrDefault((int)projectId);

        var detailData = new
        {
            id = project.AcreProjectId.ToString(),
            name = project.Name,
            status = "available",
            price = FormatPrice(project.Price),
            image = primaryImage,
            description = project.Description,
            swiperList,
            videoUrl,
            remainingAcres = (acreStats?.Remaining ?? 50).ToString(),
            soldAcres = acreStats?.Sold ?? 0,
            longExampleImage = bottomImages.FirstOrDefault() ?? primaryImage,
            longExampleImages = bottomImages,
            longExampleImageList = bottomImages,
            bottomImages
        };

        return Ok(ApiResult.Success(new
        {
            acreDetail = detailData,
            id = detailData.id,
            name = detailData.name,
            status = detailData.status,
            price = detailData.price,
            image = detailData.image,
            description = detailData.description,
            swiperList,
            videoUrl = detailData.videoUrl,
            remainingAcres = detailData.remainingAcres,
            soldAcres = detailData.soldAcres,
            longExampleImage = detailData.longExampleImage,
            longExampleImages = detailData.longExampleImages,
            longExampleImageList = detailData.longExampleImageList,
            bottomImages = detailData.bottomImages
        }));
    }

    [HttpPost("{id}/adopt")]
    public ActionResult<ApiResult> Adopt(string id, [FromBody] object? body)
    {
        if (!long.TryParse(id, out var acreId))
        {
            return Ok(ApiResult.Fail("id 参数不正确", 400));
        }

        var orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Ok(ApiResult.Success(new
        {
            acreId,
            adopted = true,
            id = orderId,
            orderId,
            message = "success"
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
        var rows = await _dbContext.AcreProjects
            .AsNoTracking()
            .Where(x => x.Status == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AcreProjectId)
            .Select(x => new
            {
                x.AcreProjectId,
                x.Name,
                x.Price,
                x.ImageUrl,
                x.Description
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new AcreDto
        {
            Id = x.AcreProjectId.ToString(),
            Name = x.Name,
            Status = "available",
            Price = FormatPrice(x.Price),
            Image = MediaUrlHelper.Normalize(x.ImageUrl),
            Description = x.Description
        }).ToList();
    }

    private async Task<List<object>> LoadSwiperListAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Carousels
            .AsNoTracking()
            .Where(x => x.Position == "acres")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => new
            {
                id = x.CarouselId,
                image = x.ImageUrl,
                linkUrl = x.LinkUrl ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => (object)new
        {
            id = x.id,
            image = MediaUrlHelper.Normalize(x.image),
            linkUrl = x.linkUrl
        }).ToList();
    }

    private static string FormatPrice(decimal price)
    {
        return $"¥{price:0.##}/亩";
    }

    private static List<string> EnsureFourImages(IEnumerable<string>? images, string? fallbackImage)
    {
        var result = (images ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        var fallback = string.IsNullOrWhiteSpace(fallbackImage) ? string.Empty : fallbackImage.Trim();
        if (result.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
        {
            result.Add(fallback);
        }

        while (result.Count > 0 && result.Count < 4)
        {
            result.Add(result[result.Count - 1]);
        }

        return result;
    }
}
