using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Common;
using WebAPI.Data;
using WebAPI.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/admin/home")]
public class AdminHomeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminHomeController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ==================== 轮播图管理 ====================

    [HttpGet("carousels")]
    public async Task<IActionResult> GetCarousels()
    {
        var list = await _db.Carousels
            .AsNoTracking()
            .Where(x => x.Position == "home")
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CarouselId)
            .Select(x => new
            {
                id = x.CarouselId,
                imageUrl = x.ImageUrl,
                linkUrl = x.LinkUrl,
                sortOrder = x.SortOrder
            })
            .ToListAsync();

        return Ok(ApiResult.Success(new { records = list }));
    }

    [HttpPost("carousel")]
    public async Task<IActionResult> CreateCarousel([FromBody] AdminCarouselCreateReq req)
    {
        var entity = new Carousel
        {
            ImageUrl = req.ImageUrl ?? string.Empty,
            LinkUrl = req.LinkUrl,
            Position = "home",
            SortOrder = req.SortOrder ?? 0
        };

        _db.Carousels.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(ApiResult.Success(new { id = entity.CarouselId }));
    }

    [HttpPut("carousel/{id:long}")]
    public async Task<IActionResult> UpdateCarousel(long id, [FromBody] AdminCarouselUpdateReq req)
    {
        var entity = await _db.Carousels.FindAsync(id);
        if (entity == null)
            return Ok(ApiResult.Fail("轮播图不存在", 404));

        if (req.ImageUrl != null)
            entity.ImageUrl = req.ImageUrl;
        if (req.LinkUrl != null)
            entity.LinkUrl = req.LinkUrl;
        if (req.SortOrder.HasValue)
            entity.SortOrder = req.SortOrder.Value;

        await _db.SaveChangesAsync();
        return Ok(ApiResult.Success());
    }

    [HttpDelete("carousel/{id:long}")]
    public async Task<IActionResult> DeleteCarousel(long id)
    {
        var entity = await _db.Carousels.FindAsync(id);
        if (entity == null)
            return Ok(ApiResult.Fail("轮播图不存在", 404));

        _db.Carousels.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResult.Success());
    }

    [HttpPost("carousel/sort")]
    public async Task<IActionResult> SortCarousels([FromBody] AdminSortReq req)
    {
        var items = await _db.Carousels
            .Where(x => req.Ids.Contains(x.CarouselId))
            .ToListAsync();

        for (var i = 0; i < items.Count; i++)
            items[i].SortOrder = i + 1;

        await _db.SaveChangesAsync();
        return Ok(ApiResult.Success());
    }

    // ==================== 视频管理 ====================

    [HttpGet("videos")]
    public async Task<IActionResult> GetVideos()
    {
        var list = await _db.Videos
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.VideoId)
            .Select(x => new
            {
                id = x.VideoId,
                videoUrl = x.VideoUrl,
                sortOrder = x.SortOrder
            })
            .ToListAsync();

        return Ok(ApiResult.Success(new { records = list }));
    }

    [HttpPost("video")]
    public async Task<IActionResult> CreateVideo([FromBody] AdminVideoCreateReq req)
    {
        var entity = new Video
        {
            VideoUrl = req.VideoUrl ?? string.Empty,
            SortOrder = req.SortOrder ?? 0
        };

        _db.Videos.Add(entity);
        await _db.SaveChangesAsync();

        // 保存后异步压缩视频
        MediaHelper.QueueVideoCompression(req.VideoUrl, _env.WebRootPath);

        return Ok(ApiResult.Success(new { id = entity.VideoId }));
    }

    [HttpPut("video/{id:long}")]
    public async Task<IActionResult> UpdateVideo(long id, [FromBody] AdminVideoUpdateReq req)
    {
        var entity = await _db.Videos.FindAsync(id);
        if (entity == null)
            return Ok(ApiResult.Fail("视频不存在", 404));

        if (req.VideoUrl != null)
            entity.VideoUrl = req.VideoUrl;
        if (req.SortOrder.HasValue)
            entity.SortOrder = req.SortOrder.Value;

        await _db.SaveChangesAsync();

        // 保存后异步压缩视频
        MediaHelper.QueueVideoCompression(req.VideoUrl, _env.WebRootPath);

        return Ok(ApiResult.Success());
    }

    [HttpDelete("video/{id:long}")]
    public async Task<IActionResult> DeleteVideo(long id)
    {
        var entity = await _db.Videos.FindAsync(id);
        if (entity == null)
            return Ok(ApiResult.Fail("视频不存在", 404));

        _db.Videos.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResult.Success());
    }

    [HttpPost("video/sort")]
    public async Task<IActionResult> SortVideos([FromBody] AdminSortReq req)
    {
        var items = await _db.Videos
            .Where(x => req.Ids.Contains(x.VideoId))
            .ToListAsync();

        for (var i = 0; i < items.Count; i++)
            items[i].SortOrder = i + 1;

        await _db.SaveChangesAsync();
        return Ok(ApiResult.Success());
    }

    // ==================== 农场简介 ====================

    [HttpGet("farm-intro")]
    public async Task<IActionResult> GetFarmIntro()
    {
        var configs = await _db.SysConfigs
            .AsNoTracking()
            .Where(x => x.ConfigKey == "farm_name"
                     || x.ConfigKey == "farm_introduction"
                     || x.ConfigKey == "farm_philosophy"
                     || x.ConfigKey == "farm_contact"
                     || x.ConfigKey == "farm_image")
            .ToListAsync();

        var dict = configs.ToDictionary(x => x.ConfigKey, x => x.ConfigValue);

        FarmContactDto? contact = null;
        if (dict.TryGetValue("farm_contact", out var contactJson))
        {
            try { contact = JsonSerializer.Deserialize<FarmContactDto>(contactJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        }

        var data = new
        {
            name = dict.GetValueOrDefault("farm_name", ""),
            introduction = dict.GetValueOrDefault("farm_introduction", ""),
            philosophy = dict.GetValueOrDefault("farm_philosophy", ""),
            image = dict.GetValueOrDefault("farm_image", ""),
            contact = contact ?? new FarmContactDto()
        };

        return Ok(ApiResult.Success(data));
    }

    [HttpPost("farm-intro")]
    public async Task<IActionResult> UpdateFarmIntro([FromBody] AdminFarmIntroReq req)
    {
        if (req.Name != null)
            await UpsertConfig("farm_name", req.Name);
        if (req.Introduction != null)
            await UpsertConfig("farm_introduction", req.Introduction);
        if (req.Philosophy != null)
            await UpsertConfig("farm_philosophy", req.Philosophy);
        if (req.Image != null)
            await UpsertConfig("farm_image", req.Image);
        if (req.Contact != null)
            await UpsertConfig("farm_contact", JsonSerializer.Serialize(req.Contact));

        await _db.SaveChangesAsync();
        return Ok(ApiResult.Success());
    }

    private async Task UpsertConfig(string key, string value)
    {
        var existing = await _db.SysConfigs.FirstOrDefaultAsync(x => x.ConfigKey == key);
        if (existing != null)
        {
            existing.ConfigValue = value;
        }
        else
        {
            _db.SysConfigs.Add(new SysConfig { ConfigKey = key, ConfigValue = value });
        }
    }
}

// ==================== Request DTOs ====================

public class AdminCarouselCreateReq
{
    public string? Title { get; set; }
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public int? SortOrder { get; set; }
}

public class AdminCarouselUpdateReq
{
    public string? Title { get; set; }
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public int? SortOrder { get; set; }
}

public class AdminVideoCreateReq
{
    public string? Title { get; set; }
    public string? CoverUrl { get; set; }
    public string? VideoUrl { get; set; }
    public int? SortOrder { get; set; }
}

public class AdminVideoUpdateReq
{
    public string? Title { get; set; }
    public string? CoverUrl { get; set; }
    public string? VideoUrl { get; set; }
    public int? SortOrder { get; set; }
}

public class AdminSortReq
{
    public List<long> Ids { get; set; } = [];
}

public class AdminFarmIntroReq
{
    public string? Name { get; set; }
    public string? Introduction { get; set; }
    public string? Philosophy { get; set; }
    public string? Image { get; set; }
    public FarmContactDto? Contact { get; set; }
}

public class FarmContactDto
{
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
}
