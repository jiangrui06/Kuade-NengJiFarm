namespace WebAPI.Dtos;

public class AcreDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Price { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public class AcreListResponseDto
{
    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int Total { get; set; }

    public List<AcreDto> Items { get; set; } = [];
}

public class AcreLogDto
{
    public string Time { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;
}

public class AcreLogsResponseDto
{
    public List<AcreLogDto> Logs { get; set; } = [];
}
