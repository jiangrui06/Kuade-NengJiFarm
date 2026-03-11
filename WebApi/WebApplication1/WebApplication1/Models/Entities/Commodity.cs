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

        // the original design stored an URL, but newer requirements call for
        // storing the binary image data directly in the database. the
        // frontend will still consume a URL that points back to an API endpoint
        // which streams the bytes.
        public string? ImageUrl { get; set; }
        public byte[]? ImageData { get; set; }
    }
}
