namespace WebApplication1.Models.Entities
{
    public class Commodity
    {
        public int CommodityId { get; set; }
        public string? SpecDescription { get; set; }
        public int? InStock { get; set; }
        public int? Quantity { get; set; }
        public int? ProductStatus { get; set; }
        public string ProductName { get; set; } = null!;
        public int CategoryId { get; set; }
        public string? ImageUrl { get; set; }
    }
}
