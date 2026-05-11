namespace WebAPI.Dtos;

/// <summary>
/// ²Í×ÀÏêÇé
/// </summary>
public class DiningTableDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CodeStatus { get; set; }
    public string? Detail { get; set; }
    public string UpdateTime { get; set; } = string.Empty;
}