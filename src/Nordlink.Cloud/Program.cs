using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Nordlink.Cloud;
using Nordlink.Contracts;

var development = args.SequenceEqual(new[] { "--development-loopback" });
if (args.Length != 0 && !development) throw new ArgumentException("Unknown cloud option.");
var origin = Environment.GetEnvironmentVariable("NORDLINK_PUBLIC_ORIGIN") ?? throw new ArgumentException("Set NORDLINK_PUBLIC_ORIGIN to the exact HTTPS website origin.");
if (!Uri.TryCreate(origin, UriKind.Absolute, out var publicUri) || publicUri.AbsolutePath != "/" || publicUri.Query.Length != 0 || publicUri.Fragment.Length != 0 || publicUri.UserInfo.Length != 0 || origin != publicUri.GetLeftPart(UriPartial.Authority) ||
    (publicUri.Scheme != "https" && !(development && publicUri.Scheme == "http" && publicUri.Host == "127.0.0.1"))) throw new ArgumentException("Exact HTTPS origin required; development HTTP is restricted to 127.0.0.1.");
var password = Environment.GetEnvironmentVariable("NORDLINK_ADMIN_PASSWORD") ?? "";
if (password.Length < 20) throw new ArgumentException("Set NORDLINK_ADMIN_PASSWORD securely in the host configuration (20+ characters).");
var salt = RandomNumberGenerator.GetBytes(32);
var expected = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA512, 32);
password = "";
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
builder.Configuration.Sources.Clear(); builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k =>
{
    if (development) k.Listen(IPAddress.Loopback, publicUri.Port);
    else k.ListenAnyIP(int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "8080"));
    k.Limits.MaxRequestBodySize = 1024 * 1024;
});
builder.Services.AddSingleton<Relay>();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "nordlink-cloud"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = development ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", _ => RateLimitPartition.GetFixedWindowLimiter("personal-cloud", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
    o.SerializerOptions.RespectNullableAnnotations = true; o.SerializerOptions.RespectRequiredConstructorParameters = true;
    o.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions = WireJson.Options);
var app = builder.Build();
app.Use(async (c, next) =>
{
    c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["Referrer-Policy"] = "no-referrer";
    c.Response.Headers["X-Content-Type-Options"] = "nosniff";
    c.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    var supplied = c.Request.Headers.Origin.ToString();
    var operational = c.Request.Path.StartsWithSegments("/api") || c.Request.Path.StartsWithSegments("/hubs") || c.Request.Path.StartsWithSegments("/relay");
    if (c.Request.Host.Value != publicUri.Authority || (supplied.Length > 0 && supplied != origin) ||
        (operational && (c.Request.Method != "GET" || c.Request.Path.StartsWithSegments("/hubs") || c.Request.Path.StartsWithSegments("/relay")) && supplied != origin) ||
        c.Request.Method == "OPTIONS" || c.Request.Headers["Sec-Fetch-Site"] == "cross-site")
    { await Error(c, 403, "Exact website Host/Origin required."); return; }
    try
    {
        await next(c);
        if (c.Response.StatusCode >= 400 && !c.Response.HasStarted) await Error(c, c.Response.StatusCode, "Request rejected. Sign in and check helper prerequisites.");
    }
    catch (Exception e) when (e is IOException or WebSocketException or OperationCanceledException or InvalidDataException)
    { if (!c.Response.HasStarted) await Error(c, 503, "Helper connection failed or timed out; outcome may be unknown. Reconnect explicitly; no command was retried."); }
    catch (InvalidOperationException e) { if (!c.Response.HasStarted) await Error(c, 409, e.Message); }
    catch (Exception e) when (e is ArgumentException or JsonException or BadHttpRequestException)
    { if (!c.Response.HasStarted) await Error(c, 400, "Invalid request."); }
});
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/health", () => new { status = "ok", mode = "cloud", hardwareVerified = false });
app.MapPost("/api/cloud/login", async (CloudLogin login, HttpContext c, Relay relay) =>
{
    if (login.Password.Length > 1024 || !CryptographicOperations.FixedTimeEquals(expected,
        Rfc2898DeriveBytes.Pbkdf2(login.Password, salt, 210_000, HashAlgorithmName.SHA512, 32)))
        return Results.Json(new ApiError("cloud-session", "Invalid sign-in.", []), statusCode: 401);
    var id = relay.CreateSession();
    await c.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sid", id)], CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.NoContent();
}).RequireRateLimiting("login");
app.MapPost("/api/helper/pair", (HelperPairRequest request, Relay relay) =>
    relay.Exchange(request.Code) is { } paired ? Results.Ok(paired) : Results.Json(new ApiError("pairing", "Pairing code invalid, expired or already used.", []), statusCode: 401)).RequireRateLimiting("login");
var secure = app.MapGroup("/api").RequireAuthorization();
secure.AddEndpointFilter(async (context, next) =>
{
    var relay = context.HttpContext.RequestServices.GetRequiredService<Relay>();
    return relay.Active(Session(context.HttpContext)) ? await next(context) : Results.Unauthorized();
});
secure.MapGet("/cloud/session", (HttpContext c, Relay r) => new { authenticated = true, helperConnected = r.Connected(Session(c)) });
secure.MapPost("/cloud/pairing", (HttpContext c, Relay r) => r.Pair(Session(c)));
secure.MapPost("/cloud/unpair", (HttpContext c, Relay r) => { r.Detach(Session(c)); return Results.NoContent(); });
secure.MapPost("/cloud/logout", async (HttpContext c, Relay r) => { r.Remove(Session(c)); await c.SignOutAsync(); });
secure.MapGet("/features", () => Results.Text(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "feature-register.json")), "application/json"));
secure.MapMethods("/{**route}", ["GET", "POST"], async (HttpContext c, Relay relay) =>
{
    var route = c.Request.Path.Value!;
    if (!WebSocketFrames.Allowed(c.Request.Method, route))
        return Results.Json(new ApiError("cloud-route", "This operation stays on the Windows PC or lacks a validated vehicle procedure.", ["Supported cloud relay route"]), statusCode: 409);
    using var reader = new StreamReader(c.Request.Body);
    var body = c.Request.Method == "POST" ? await reader.ReadToEndAsync(c.RequestAborted) : null;
    var response = await relay.Request(Session(c), c.Request.Method, route, body, c.RequestAborted);
    if (response.Status < 200 || response.Status > 599 || response.ContentType is not ("application/json" or "text/csv")) throw new InvalidDataException("Invalid helper response.");
    if (response.ContentType == "text/csv") c.Response.Headers.ContentDisposition = "attachment; filename=nordlink-recording.csv";
    return Results.Text(response.Body ?? "", response.ContentType, Encoding.UTF8, response.Status);
});
app.MapHub<CloudHub>("/hubs/progress");
app.Map("/relay/helper", async (HttpContext c, Relay relay, IHubContext<CloudHub> hub) =>
{
    var authorization = c.Request.Headers.Authorization.ToString();
    var id = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? relay.AuthenticateHelper(authorization[7..]) : null;
    if (id is null || !c.WebSockets.IsWebSocketRequest) { await Error(c, 401, "Helper authentication required."); return; }
    using var socket = await c.WebSockets.AcceptWebSocketAsync();
    if (!relay.Attach(id, socket, authorization[7..])) { socket.Abort(); return; }
    using var expiry = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted); expiry.CancelAfter(TimeSpan.FromHours(8));
    try
    {
        while (relay.Active(id) && await WebSocketFrames.Read(socket, expiry.Token) is { } frame)
        {
            if (frame.Kind == "response") relay.Complete(id, frame);
            else if (frame.Kind is "progress" or "live" && frame.Body is not null)
            {
                // Deserialize into allowlisted DTOs; a compromised helper cannot forward arbitrary secret fields.
                if (frame.Kind == "progress") await hub.Clients.Group(id).SendAsync("progress", JsonSerializer.Deserialize<ProgressEvent>(frame.Body, WireJson.Options), expiry.Token);
                else await hub.Clients.Group(id).SendAsync("live", JsonSerializer.Deserialize<LiveValueEvent>(frame.Body, WireJson.Options), expiry.Token);
            }
            else throw new InvalidDataException("Unsupported helper event.");
        }
    }
    finally { relay.Detach(id, socket); }
});
app.MapFallback(async c =>
{
    if (c.Request.Path.StartsWithSegments("/api") || c.Request.Path.StartsWithSegments("/relay")) { await Error(c, 404, "Unknown route."); return; }
    var index = Path.Combine(AppContext.BaseDirectory, "wwwroot/index.html");
    if (File.Exists(index)) { c.Response.ContentType = "text/html"; await c.Response.SendFileAsync(index); }
    else await Error(c, 503, "Build the cloud frontend first.");
});
await app.RunAsync();
static string Session(HttpContext c) => c.User.FindFirst("sid")?.Value ?? "";
static async Task Error(HttpContext c, int code, string reason)
{ c.Response.StatusCode = code; await c.Response.WriteAsJsonAsync(new ApiError("cloud", reason, []), WireJson.Options); }
