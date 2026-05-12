using System.ComponentModel.DataAnnotations;

namespace ManageAPI.Dtos;

public class ActivitySummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
}

public class ActivityListDto
{
    public Dictionary<string, List<ActivitySummaryDto>> Activities { get; set; } = [];
}

