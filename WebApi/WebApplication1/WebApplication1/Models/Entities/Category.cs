namespace WebApplication1.Models.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = null!;
        public string? CategoryDescription { get; set; }
        public int? CategoryStatus { get; set; }
        public int? SortOrder { get; set; }
    }
}
