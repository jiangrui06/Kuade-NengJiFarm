namespace WebAPI.Dtos;

/// <summary>
/// 餐桌列表项 DTO
/// </summary>
public class TableListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Tableno { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int StatusId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreateTime { get; set; } = string.Empty;
}

/// <summary>
/// 餐桌详情 DTO
/// </summary>
public class TableDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Tableno { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Status { get; set; }
    public string CreateTime { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
}

/// <summary>
/// 新增餐桌请求
/// </summary>
public class CreateTableRequestDto
{
    public string Tableno { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// 更新餐桌请求
/// </summary>
public class UpdateTableRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string? Tableno { get; set; }
    public int? Capacity { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// 删除餐桌请求
/// </summary>
public class DeleteTableRequestDto
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// 更新餐桌状态请求
/// </summary>
public class UpdateTableStatusRequestDto
{
    public string Tableno { get; set; } = string.Empty;
    public int Status { get; set; }
}

/// <summary>
/// 创建/编辑餐桌响应（含二维码）
/// </summary>
public class TableMutationResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Tableno { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Status { get; set; }
    public string QrCodeUrl { get; set; } = string.Empty;
}
