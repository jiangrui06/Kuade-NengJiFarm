namespace ManageAPI.Dtos;

public class CreateDiningTableDto
{
    public string TableNo { get; set; } = string.Empty;
    public int SeatCount { get; set; }
    public int TableStatus { get; set; }
    public string QrCodeImageUrl { get; set; } = string.Empty;
}

public class DiningTableListItemDto
{
    public long Id { get; set; }
    public string TableNo { get; set; } = string.Empty;
    public int SeatCount { get; set; }
    public int TableStatus { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string QrCodeImageUrl { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
