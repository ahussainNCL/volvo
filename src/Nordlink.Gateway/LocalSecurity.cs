using System.Security.Cryptography;
using System.Text;

namespace Nordlink.Gateway;

public sealed class LocalSecurity
{
    private readonly TimeProvider clock;
    private readonly byte[] bootstrap = RandomNumberGenerator.GetBytes(32);
    private readonly string cookie = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private int exchanged;
    private readonly DateTimeOffset created;
    public LocalSecurity(TimeProvider? clock = null) { this.clock = clock ?? TimeProvider.System; created = this.clock.GetUtcNow(); }
    public string Origin { get; set; } = "";
    public string LaunchToken => Convert.ToHexString(bootstrap);
    public bool Authenticate(string? value) => created.AddHours(8) > clock.GetUtcNow() &&
        Volatile.Read(ref exchanged) == 1 && Equal(value, cookie);
    public string? Exchange(string value)
    {
        if (created.AddMinutes(5) < clock.GetUtcNow() || !Equal(value, LaunchToken) || Interlocked.CompareExchange(ref exchanged, 1, 0) != 0) return null;
        return cookie;
    }
    private static bool Equal(string? left, string right) => left is not null && left.Length == right.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    public async Task Guard(HttpContext context, RequestDelegate next)
    {
        var request = context.Request; var response = context.Response;
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
        var host = new Uri(Origin).Authority;
        var suppliedOrigin = request.Headers.Origin.ToString();
        var signalR = request.Path.StartsWithSegments("/hubs");
        var mutation = request.Method is not ("GET" or "HEAD" or "OPTIONS");
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (request.Host.Value != host || request.Scheme != "http" ||
            (suppliedOrigin.Length > 0 && suppliedOrigin != Origin) ||
            ((mutation || signalR) && suppliedOrigin != Origin) ||
            fetchSite is "cross-site")
        { await Error(context, 403, "local-session", "Host/Origin must match the exact local launch origin."); return; }
        if (request.Method == "OPTIONS") { await Error(context, 403, "local-session", "Cross-origin access is disabled."); return; }
        if ((request.Path.StartsWithSegments("/api") || signalR) && request.Path != "/api/session" && !Authenticate(request.Cookies["nordlink-session"]))
        { await Error(context, 401, "local-session", "Session is missing or expired. Relaunch Nordlink."); return; }
        await next(context);
    }
    public static async Task Error(HttpContext context, int status, string capability, string reason, params string[] prerequisites)
    { context.Response.StatusCode = status; await context.Response.WriteAsJsonAsync(new Contracts.ApiError(capability, reason, prerequisites), Contracts.WireJson.Options); }
}
