using Microsoft.AspNetCore.Mvc;

using ManageAPI.Common;
using ManageAPI.Dtos;
using ManageAPI.Services;

namespace ManageAPI.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly IWebHostEnvironment _env;

    public ActivityController(IActivityService activityService, IWebHostEnvironment env)
    {
        _activityService = activityService;
        _env = env;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNum = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var (records, total) = await _activityService.GetActivityListAsync(pageNum, pageSize, keyword, cancellationToken);

        return Ok(ApiResult.Success(new
        {
            records,
            total,
            pageNum,
            pageSize,
            pages = (total + pageSize - 1) / pageSize
        }));
    }

    [HttpGet("detail")]
    public async Task<IActionResult> GetDetail(
        [FromQuery] long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Ok(ApiResult.Fail("参数不正确", 400));

        var activity = await _activityService.GetActivityDetailAsync(id, cancellationToken);

        if (activity is null)
            return Ok(ApiResult.Fail("活动不存在或已被删除", 404));

        return Ok(ApiResult.Success(activity));
    }

    /// <summary>创建活动 — 支持 multipart/form-data 和 application/json</summary>
    [HttpPost("add")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
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

    /// <summary>编辑活动 — 支持 multipart/form-data 和 application/json</summary>
    [HttpPut("edit")]
    [HttpPost("edit")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Update(CancellationToken cancellationToken = default)
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

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Id <= 0)
            return Ok(ApiResult.Fail("参数不能为空", 400));

        var success = await _activityService.DeleteActivityAsync(request.Id, cancellationToken);

        if (!success)
            return Ok(ApiResult.Fail("活动不存在或已被删除", 404));

        return Ok(ApiResult.Success("删除成功"));
    }

    [HttpPost("deleteBatch")]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] DeleteBatchActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Ids == null || request.Ids.Length == 0)
            return Ok(ApiResult.Fail("参数不能为空", 400));

        var success = await _activityService.DeleteActivityBatchAsync(request.Ids, cancellationToken);

        if (!success)
            return Ok(ApiResult.Fail("删除失败", 404));

        return Ok(ApiResult.Success("删除成功"));
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

        return new CreateActivityDto
        {
            Name = form["name"].FirstOrDefault() ?? string.Empty,
            Type = form["type"].FirstOrDefault() ?? string.Empty,
            Price = decimal.TryParse(form["price"].FirstOrDefault(), out var p) ? p : 0,
            StatusId = int.TryParse(form["statusId"].FirstOrDefault(), out var sid) ? sid : 1,
            Image = image,
            VideoUrl = form["videoUrl"].FirstOrDefault() ?? string.Empty,
            Description = form["description"].FirstOrDefault(),
            Location = form["location"].FirstOrDefault(),
            People = int.TryParse(form["people"].FirstOrDefault(), out var pp) ? pp : null,
            Content = form["content"].FirstOrDefault(),
            Duration = int.TryParse(form["duration"].FirstOrDefault(), out var d) ? d : 0,
            CarouselMedia = carouselMedia
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
            CarouselMedia = baseDto.CarouselMedia
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
