using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Models.Entities;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommodityController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CommodityController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/commodity?pageIndex=1&pageSize=10
        [HttpGet]
        public async Task<ActionResult<ApiResponse<object>>> GetList([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var query = _db.Commodities.AsNoTracking();
            var total = await query.CountAsync();
            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new
            {
                pageIndex,
                pageSize,
                total,
                items = items.Select(c => new
                {
                    c.CommodityId,
                    c.ProductName,
                    c.SpecDescription,
                    c.InStock,
                    c.Quantity,
                    c.ProductStatus,
                    c.CategoryId,
                    image = Url.Action(nameof(GetImage), new { id = c.CommodityId })
                })
            };

            return ApiResponse<object>.Ok(result);
        }

        // GET /api/commodity/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
        {
            var c = await _db.Commodities.FindAsync(id);
            if (c == null) return ApiResponse<object>.Fail("商品不存在", 404);
            var dto = new
            {
                c.CommodityId,
                c.ProductName,
                c.SpecDescription,
                c.InStock,
                c.Quantity,
                c.ProductStatus,
                c.CategoryId,
                image = Url.Action(nameof(GetImage), new { id = c.CommodityId })
            };
            return ApiResponse<object>.Ok(dto);
        }

        // GET /api/commodity/{id}/image
        [HttpGet("{id}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            var c = await _db.Commodities
                .AsNoTracking()
                .Where(x => x.CommodityId == id)
                .Select(x => new { x.ImageData, x.ImageUrl })
                .FirstOrDefaultAsync();
            if (c == null) return NotFound();

            if (c.ImageData != null && c.ImageData.Length > 0)
            {
                // guess content type from url if present
                var contentType = "application/octet-stream";
                if (!string.IsNullOrEmpty(c.ImageUrl))
                {
                    var ext = Path.GetExtension(c.ImageUrl).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
                    else if (ext == ".png") contentType = "image/png";
                }
                return File(c.ImageData, contentType);
            }

            // fallback: redirect to url
            if (!string.IsNullOrEmpty(c.ImageUrl))
            {
                return Redirect(c.ImageUrl);
            }

            return NotFound();
        }

        // POST /api/commodity  (multipart/form-data)
        [HttpPost]
        public async Task<ActionResult<ApiResponse<object>>> Create([FromForm] CommodityCreateDto dto)
        {
            var entity = new Commodity
            {
                ProductName = dto.ProductName,
                SpecDescription = dto.SpecDescription,
                InStock = dto.InStock,
                Quantity = dto.Quantity,
                ProductStatus = dto.ProductStatus,
                CategoryId = dto.CategoryId,
                ImageUrl = dto.ImageUrl
            };

            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await dto.ImageFile.CopyToAsync(ms);
                entity.ImageData = ms.ToArray();
            }

            _db.Commodities.Add(entity);
            await _db.SaveChangesAsync();

            return ApiResponse<object>.Ok(new { id = entity.CommodityId });
        }

        // PUT /api/commodity/{id}  (multipart/form-data)
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> Update(int id, [FromForm] CommodityCreateDto dto)
        {
            var entity = await _db.Commodities.FindAsync(id);
            if (entity == null) return ApiResponse<object>.Fail("商品不存在", 404);

            entity.ProductName = dto.ProductName;
            entity.SpecDescription = dto.SpecDescription;
            entity.InStock = dto.InStock;
            entity.Quantity = dto.Quantity;
            entity.ProductStatus = dto.ProductStatus;
            entity.CategoryId = dto.CategoryId;
            entity.ImageUrl = dto.ImageUrl;

            if (dto.ImageFile != null && dto.ImageFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await dto.ImageFile.CopyToAsync(ms);
                entity.ImageData = ms.ToArray();
            }

            await _db.SaveChangesAsync();
            return ApiResponse<object>.Ok(null);
        }

        // DELETE /api/commodity/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            var entity = await _db.Commodities.FindAsync(id);
            if (entity == null) return ApiResponse<object>.Fail("商品不存在", 404);

            _db.Commodities.Remove(entity);
            await _db.SaveChangesAsync();
            return ApiResponse<object>.Ok(null);
        }
    }

    public class CommodityCreateDto
    {
        public string ProductName { get; set; } = null!;
        public string? SpecDescription { get; set; }
        public int? InStock { get; set; }
        public int? Quantity { get; set; }
        public int? ProductStatus { get; set; }
        public int CategoryId { get; set; }
        public string? ImageUrl { get; set; }
        public IFormFile? ImageFile { get; set; }
    }
}
