namespace WebAPI.Dtos;

public class ProductStatsDto
{
    public int TotalProducts { get; set; }
    public int OnSaleCount { get; set; }
    public int StockAlertCount { get; set; }
    public int TotalStock { get; set; }
}
