namespace WebAPI.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Dtos;
using WebAPI.Services;

[ApiController]
[Route("api/activity-manage")]
public class ActivityManageController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ManageAppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public ActivityManageController(IActivityService activityService, ManageAppDbContext dbContext, IWebHostEnvironment env)
    {
        _activityService = activityService;
        _dbContext = dbContext;
        _env = env;
    }

    /// <summary>
    /// 获取活动状态列表
    /// </summary>
    [HttpGet("statuses")]
    public async Task<IActionResult> GetStatuses(CancellationToken cancellationToken)
    {
        try
        {
            var statuses = await _dbContext.ActivityStatuses
                .OrderBy(s => s.ActivityStatusId)
                .Select(s => new
                {
                    statusId = s.ActivityStatusId,
                    statusName = s.StatusName
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(statuses));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取状态列表失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取活动类型列表
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetTypes(CancellationToken cancellationToken)
    {
        try
        {
            var types = await _dbContext.ActivityTypes
                .OrderBy(t => t.ActivityTypeId)
                .Select(t => new
                {
                    typeId = t.ActivityTypeId,
                    typeName = t.TypeName
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult.Success(types));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取活动类型列表失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取活动列表
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (records, total) = await _activityService.GetActivityListAsync(pageNum, pageSize, keyword, cancellationToken);
            var (totalSoldCount, totalVerifiedCount) = await _activityService.GetActivityTotalStatsAsync(cancellationToken);

            return Ok(ApiResult.Success(new
            {
                records,
                total,
                pageNum,
                pageSize,
                pages = (total + pageSize - 1) / pageSize,
                totalActivityCount = total,
                totalSoldCount,
                totalVerifiedCount,
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 获取活动详情
    /// </summary>
    [HttpGet("detail")]
    public async Task<IActionResult> GetDetail(
        [FromQuery] long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id <= 0)
            {
                return Ok(ApiResult.Fail("参数不正确", 400));
            }

            var activity = await _activityService.GetActivityDetailAsync(id, cancellationToken);
            if (activity is null)
            {
                return Ok(ApiResult.Fail("活动不存在或已被删除", 404));
            }

            return Ok(ApiResult.Success(activity));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"获取失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 创建活动 — 支持 multipart/form-data（传文件）和 application/json（兼容旧版）
    /// </summary>
    [HttpPost("add")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        try
        {
            CreateActivityDto dto;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                dto = await BuildCreateDtoFromFormAsync(form);
            }
            else
            {
                dto = (await Request.ReadFromJsonAsync<CreateActivityDto>(cancellationToken: cancellationToken))!;
            }

            if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                return Ok(ApiResult.Fail("活动名称不能为空", 400));

            var id = await _activityService.CreateActivityAsync(dto, cancellationToken);
            return Ok(ApiResult.Success(new { id }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"创建失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 编辑活动 — 支持 multipart/form-data 和 application/json
    /// </summary>
    [HttpPut("edit")]
    [HttpPost("edit")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Update(CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateActivityDto dto;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                dto = await BuildUpdateDtoFromFormAsync(form);
            }
            else
            {
                dto = (await Request.ReadFromJsonAsync<UpdateActivityDto>(cancellationToken: cancellationToken))!;
            }

            if (dto is null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.Name))
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _activityService.UpdateActivityAsync(dto.Id, dto, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("活动不存在或已被删除", 404));

            return Ok(ApiResult.Success("编辑成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"编辑失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 删除活动
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Id <= 0)
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _activityService.DeleteActivityAsync(request.Id, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("活动不存在或已被删除", 404));

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    /// <summary>
    /// 批量删除活动
    /// </summary>
    [HttpPost("deleteBatch")]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] DeleteBatchActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.Ids == null || request.Ids.Length == 0)
                return Ok(ApiResult.Fail("参数不能为空", 400));

            var success = await _activityService.DeleteActivityBatchAsync(request.Ids, cancellationToken);
            if (!success)
                return Ok(ApiResult.Fail("删除失败", 404));

            return Ok(ApiResult.Success("删除成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail($"删除失败：{ex.Message}", 500));
        }
    }

    private async Task<CreateActivityDto> BuildCreateDtoFromFormAsync(IFormCollection form)
    {
        var imageFile = form.Files.GetFile("image");
        var image = await MediaHelper.SaveFileAsync(imageFile, _env.WebRootPath);

        var carouselMedia = new List<CarouselMediaDto>();
        foreach (var file in form.Files.GetFiles("carouselMedia"))
        {
            var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
            if (!string.IsNullOrEmpty(url))
            {
                carouselMedia.Add(new CarouselMediaDto
                {
                    Type = MediaHelper.IsVideoUrl(file.FileName) ? "video" : "image",
                    Url = url
                });
            }
        }

        // 规格图上传
        var specImages = new List<string>();
        foreach (var file in form.Files.GetFiles("specImages"))
        {
            var url = await MediaHelper.SaveFileAsync(file, _env.WebRootPath);
            if (!string.IsNullOrEmpty(url))
            {
                specImages.Add(url);
            }
        }

        return new CreateActivityDto
        {
            Name = form["name"].FirstOrDefault() ?? string.Empty,
            Type = form["type"].FirstOrDefault() ?? string.Empty,
            Price = decimal.TryParse(form["price"].FirstOrDefault(), out var p) ? p : 0,
            StatusId = await _activityService.MapStatusToIdAsync(form["status"].FirstOrDefault() ?? "已上架"),
            Image = image,
            VideoUrl = form["videoUrl"].FirstOrDefault() ?? string.Empty,
            Description = form["description"].FirstOrDefault(),
            Location = form["location"].FirstOrDefault(),
            People = int.TryParse(form["people"].FirstOrDefault(), out var pp) ? pp : null,
            Content = form["content"].FirstOrDefault(),
            Duration = int.TryParse(form["duration"].FirstOrDefault(), out var d) ? d : 0,
            CarouselMedia = carouselMedia,
            SpecImages = specImages,
        };
    }

    private async Task<UpdateActivityDto> BuildUpdateDtoFromFormAsync(IFormCollection form)
    {
        var baseDto = await BuildCreateDtoFromFormAsync(form);
        return new UpdateActivityDto
        {
            Id = long.TryParse(form["id"].FirstOrDefault(), out var id) ? id : 0,
            Name = baseDto.Name,
            Type = baseDto.Type,
            Price = baseDto.Price,
            StatusId = baseDto.StatusId,
            Image = baseDto.Image,
            VideoUrl = baseDto.VideoUrl,
            Description = baseDto.Description,
            Location = baseDto.Location,
            People = baseDto.People,
            Content = baseDto.Content,
            Duration = baseDto.Duration,
            CarouselMedia = baseDto.CarouselMedia,
        };
    }
}

public class DeleteActivityRequest
{
    public long Id { get; set; }
}

public class DeleteBatchActivityRequest
{
    public long[]? Ids { get; set; }
}
