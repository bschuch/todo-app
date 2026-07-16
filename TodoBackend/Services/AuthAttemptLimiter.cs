using System.Collections.Concurrent;

namespace TodoBackend.Services;

public class AuthAttemptLimiter(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
{
    private readonly ConcurrentDictionary<string, AttemptWindow> windows = new();
    private readonly int requestLimit = Math.Max(1, configuration.GetValue("RateLimit:AuthenticationRequestsPerMinute", 10));

    public void RequirePermit(string action)
    {
        var address = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"{action}:{address}";
        var now = DateTime.UtcNow;
        var window = windows.AddOrUpdate(
            key,
            _ => new AttemptWindow(now, 1),
            (_, current) => now - current.StartedAt >= TimeSpan.FromMinutes(1)
                ? new AttemptWindow(now, 1)
                : current with { Count = current.Count + 1 });

        if (window.Count > requestLimit)
        {
            throw new GraphQLException("Too many attempts. Please wait a minute and try again.");
        }
    }

    private sealed record AttemptWindow(DateTime StartedAt, int Count);
}
