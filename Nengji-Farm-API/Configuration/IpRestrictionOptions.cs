namespace WebAPI.Configuration;

public class IpRestrictionOptions
{
    public const string SectionName = "IpRestriction";

    /// <summary>是否启用 IP 白名单（默认 true）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>白名单 IP/CIDR 列表</summary>
    public string[] Whitelist { get; set; } = [];

    /// <summary>受保护的路径前缀</summary>
    public string[] ProtectedPaths { get; set; } = [
        "/api/back-",
        "/api/admin/",
        "/api/activity-manage",
        "/api/staff",
        "/api/kitchen",
    ];
}
