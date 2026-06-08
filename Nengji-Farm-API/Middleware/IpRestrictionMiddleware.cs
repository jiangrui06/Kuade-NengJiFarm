using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using WebAPI.Configuration;

namespace WebAPI.Middleware;

public class IpRestrictionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpRestrictionMiddleware> _logger;
    private readonly IpRestrictionOptions _options;
    private readonly ConcurrentDictionary<string, (bool Allowed, long Expires)> _ipCache = new();

    private const int CacheSeconds = 60;
    private static readonly char[] Comma = [','];

    public IpRestrictionMiddleware(RequestDelegate next, ILogger<IpRestrictionMiddleware> logger, IOptions<IpRestrictionOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value;
        if (path is null || !_options.ProtectedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIp(context);
        if (string.IsNullOrEmpty(clientIp))
        {
            _logger.LogWarning("IP 限制: 无法获取客户端 IP，路径={Path}", path);
            await Deny(context, "无法识别客户端 IP");
            return;
        }

        if (!IsIpAllowed(clientIp))
        {
            _logger.LogWarning("IP 限制: 拒绝访问 | IP={ClientIp} 路径={Path}", clientIp, path);
            await Deny(context, "IP 不在访问白名单中");
            return;
        }

        await _next(context);
    }

    private bool IsIpAllowed(string clientIp)
    {
        if (_options.Whitelist.Length == 0)
            return false;

        // 缓存查找
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_ipCache.TryGetValue(clientIp, out var cached) && cached.Expires > now)
            return cached.Allowed;

        if (!IPAddress.TryParse(clientIp, out var address))
        {
            _ipCache[clientIp] = (false, now + CacheSeconds);
            return false;
        }

        var allowed = _options.Whitelist.Any(w => IpMatchesCidr(address, w));
        _ipCache[clientIp] = (allowed, now + CacheSeconds);
        return allowed;
    }

    private static bool IpMatchesCidr(IPAddress address, string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        if (cidr.Contains('/'))
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2) return false;
            if (!IPAddress.TryParse(parts[0], out var networkAddress)) return false;
            if (!int.TryParse(parts[1], out var prefixLength)) return false;

            if (address.AddressFamily != networkAddress.AddressFamily)
                return false;

            var addressBytes = address.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            var maskBytes = new byte[addressBytes.Length];
            for (var i = 0; i < maskBytes.Length; i++)
            {
                var bits = Math.Max(0, Math.Min(8, prefixLength - i * 8));
                maskBytes[i] = (byte)(0xFF << (8 - bits) & 0xFF);
            }

            for (var i = 0; i < addressBytes.Length; i++)
            {
                if ((addressBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
                    return false;
            }
            return true;
        }

        return string.Equals(cidr, address.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetClientIp(HttpContext context)
    {
        // 优先取 X-Forwarded-For（代理转发场景）
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.Split(Comma, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            if (IPAddress.TryParse(firstIp, out _))
                return firstIp;
        }

        // X-Real-IP（nginx 代理）
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp) && IPAddress.TryParse(realIp, out _))
            return realIp;

        // 直连
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static async Task Deny(HttpContext context, string message)
    {
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { code = 403, message });
    }
}
