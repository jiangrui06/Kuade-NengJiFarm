namespace WebAPI.Dtos;

/// <summary>
/// ²Í×ÀÁĞ±íÏî
/// </summary>
public class DiningTableListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = "¿ÕÏĞ";
    public string Detail { get; set; } = string.Empty;
    public string UpdateTime { get; set; } = string.Empty;
}