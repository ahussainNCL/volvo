using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Nordlink.Contracts;
using Nordlink.Gateway;
using Nordlink.Providers;
using Nordlink.Storage;

var options = LaunchOptions.Parse(args);
Directory.CreateDirectory(options.DataRoot);
// A second gateway must not create a second scheduler or recover another live instance's jobs.
using var instanceLock = new FileStream(Path.Combine(options.DataRoot, "nordlink.instance"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
builder.Configuration.Sources.Clear();
builder.Logging.ClearProviders(); // Do not log HTTP bodies, bootstrap fragments, cookies, driver output or credential material.
builder.WebHost.ConfigureKestrel(k => { k.Listen(IPAddress.Loopback, options.Port); k.Limits.MaxRequestBodySize = 7 * 1024 * 1024; });
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false)); o.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow; o.SerializerOptions.RespectNullableAnnotations = true; o.SerializerOptions.RespectRequiredConstructorParameters = true; });
builder.Services.AddSignalR(o => { o.MaximumReceiveMessageSize = 16 * 1024; o.EnableDetailedErrors = false; })
    .AddJsonProtocol(o => o.PayloadSerializerOptions = WireJson.Options);
var security = new LocalSecurity();
var connection = new ConnectionService(options.DataRoot, options.WorkerRoot);
builder.Services.AddSingleton(security); builder.Services.AddSingleton(connection);
builder.Services.AddSingleton<SimulationFeed>(); builder.Services.AddHostedService(p => p.GetRequiredService<SimulationFeed>());
var app = builder.Build();
app.Use(async (context, next) => await security.Guard(context, async c =>
{
    try
    {
        await next(c);
        if (c.Response.StatusCode >= 400 && !c.Response.HasStarted)
            await LocalSecurity.Error(c, c.Response.StatusCode, "request", "Request rejected. Check the documented contract and prerequisites.");
    }
    catch (Exception ex) when (ex is ArgumentException or FormatException or BadHttpRequestException or JsonException)
    { await LocalSecurity.Error(c, 400, "request", "Invalid request. Check the documented contract and prerequisites."); }
    catch (InvalidOperationException ex)
    { await LocalSecurity.Error(c, 409, "prerequisites", ex.Message); }
    catch (PlatformNotSupportedException)
    { await LocalSecurity.Error(c, 409, "hardware", "This operation requires Windows and validated hardware prerequisites.", "Windows validation"); }
    catch (KeyNotFoundException)
    { await LocalSecurity.Error(c, 404, "resource", "Requested local record was not found."); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or CryptographicException or UnauthorizedAccessException)
    { await LocalSecurity.Error(c, 503, "local-dependency", "Local storage or hardware worker operation failed. Disconnect, inspect prerequisites and reconnect explicitly.", "Healthy local storage or official hardware installation"); }
}));
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapPost("/api/session", (BootstrapRequest request, HttpContext context) =>
{
    var cookie = security.Exchange(request.Token);
    if (cookie is null) return Results.Json(new ApiError("local-session", "Bootstrap is invalid, expired or already used. Relaunch Nordlink.", []), statusCode: 401);
    context.Response.Cookies.Append("nordlink-session", cookie, new CookieOptions
    { HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = false, Path = "/", MaxAge = TimeSpan.FromHours(8), IsEssential = true });
    // Retain no launch token on disk after a successful exchange.
    if (File.Exists(options.LaunchFile)) File.Delete(options.LaunchFile);
    return Results.NoContent();
});
app.MapGet("/api/status", () => connection.Snapshot);
app.MapGet("/api/features", () => JsonSerializer.Deserialize<CapabilityDescriptor[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "feature-register.json")), WireJson.Options));
app.MapPost("/api/environment", async (EnvironmentRequest request, CancellationToken ct) =>
    await connection.Scheduler.Exclusive(_ => { connection.ChangeEnvironment(request.Environment); return Task.FromResult(connection.Snapshot); }, ct));
app.MapPost("/api/drivers/inspect", async (CancellationToken ct) => await connection.Inspect(ct));
app.MapPost("/api/connect", async (ConnectRequest request, CancellationToken ct) => await connection.Connect(request.DriverPath, ct));
app.MapPost("/api/disconnect", async (CancellationToken ct) => { await connection.Disconnect(ct); return connection.Snapshot; });
app.MapGet("/api/vehicles", () => connection.Store.List<VehicleProfile>("vehicle"));
app.MapGet("/api/operations", () => connection.Store.List<OperationJob>("job"));
app.MapPost("/api/operations/review", async (ReviewRequest request, CancellationToken ct) =>
    await connection.Scheduler.Exclusive(_ => Task.FromResult(connection.Reviews.Review(connection.Store, connection.Snapshot, request)), ct));
app.MapPost("/api/operations/{id}/execute", async (string id, ExecuteRequest request, IHubContext<ProgressHub> hub, CancellationToken ct) =>
    await connection.Scheduler.Exclusive(async token =>
    {
        var store = connection.Store; var job = connection.Reviews.Consume(store, connection.Snapshot, id, request.ApprovalToken);
        try
        {
            await hub.Clients.All.SendAsync("progress", new ProgressEvent(id, JobState.Running, "Recording the reviewed simulation fixture note.", DateTimeOffset.UtcNow, job.Context), token);
            var completed = job with { State = JobState.Completed }; store.Save("job", id, completed); store.Audit("operation.completed", id);
            await hub.Clients.All.SendAsync("progress", new ProgressEvent(id, JobState.Completed, "Simulation exercise completed. No vehicle write.", DateTimeOffset.UtcNow, job.Context), token);
            return completed;
        }
        catch
        {
            store.Save("job", id, job with { State = JobState.Interrupted, Failure = "Execution interrupted. Review again; no automatic retry." });
            store.Audit("operation.interrupted", id); throw;
        }
    }, ct));
app.MapGet("/api/recordings", () => connection.Store.List<Recording>("recording"));
app.MapPost("/api/live/simulation/start", async (SimulationFeed feed, CancellationToken ct) => await connection.Scheduler.Exclusive(_ => { feed.Start(); return Task.FromResult(new { state = "simulation stream started" }); }, ct));
app.MapPost("/api/live/simulation/stop", (SimulationFeed feed) => { feed.Stop(); return Results.NoContent(); });
app.MapPost("/api/recordings/simulation-fixture", async (CancellationToken ct) => await connection.Scheduler.Exclusive(_ =>
{
    var state = connection.Snapshot;
    if (state.Environment != EnvironmentKind.Simulation || state.Vehicle is null) throw new InvalidOperationException("Connect the simulation fixture first.");
    var start = DateTimeOffset.UtcNow;
    var samples = Enumerable.Range(0, 25).Select(i => new ParameterSample(start.AddMilliseconds(i * 200), "fixture.voltage", "V",
        i is 10 or 11 ? null : Math.Round(12.4 + Math.Sin(i * 0.4) * 0.2, 3), i is 10 or 11)).ToArray();
    var recording = new Recording(Guid.NewGuid().ToString("N"), samples, state.Vehicle.Context with { Source = "deterministic simulation fixture", ObservedAt = start });
    var store = connection.Store; store.SaveRecording(recording); store.Audit("recording.fixture", recording.Id); return Task.FromResult(recording);
}, ct));
app.MapGet("/api/recordings/{id}", (string id) => connection.Store.Get<Recording>("recording", id) ?? throw new KeyNotFoundException());
app.MapGet("/api/recordings/{id}/csv", (string id) =>
{
    var r = connection.Store.Get<Recording>("recording", id) ?? throw new KeyNotFoundException();
    return Results.File(Encoding.UTF8.GetBytes(WorkspaceStore.RecordingCsv(r)), "text/csv", "nordlink-recording.csv");
});
app.MapGet("/api/artifacts", () => connection.Store.List<ArtifactMetadata>("artifact"));
app.MapPost("/api/artifacts", async (ArtifactImport request, CancellationToken ct) => await connection.Scheduler.Exclusive(_ =>
{
    var state = connection.Snapshot;
    return Task.FromResult(new LocalArtifacts(connection.Store).Import(request, new(state.Environment, state.Vehicle?.Id, null, request.Source, DateTimeOffset.UtcNow, null)));
}, ct));
app.MapGet("/api/credentials", () => connection.Store.List<SecurityCredential>("credential"));
app.MapPost("/api/credentials/{id}/reveal", async (string id, HttpContext context, CancellationToken ct) =>
{
    await connection.Scheduler.Exclusive(async _ =>
    {
        var material = new DpapiCredentialVault(connection.Store).Reveal(id);
        try { context.Response.ContentType = "text/plain; charset=utf-8"; await context.Response.Body.WriteAsync(material, ct); }
        finally { CryptographicOperations.ZeroMemory(material); }
        return true;
    }, ct);
});
app.MapPost("/api/capabilities/{id}/run", (string id) => Results.Json(
    new ApiError(id, "No validated vehicle procedure is installed for this operation.",
        ["Real VIN and module identity", "Licensed applicable definitions", "Documented procedure and hardware acceptance evidence"]), statusCode: 409));
app.MapHub<ProgressHub>("/hubs/progress");
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
        await LocalSecurity.Error(context, 404, "route", "Unknown API endpoint.");
    else
    {
        var index = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(index)) { context.Response.ContentType = "text/html"; await context.Response.SendFileAsync(index); }
        else await LocalSecurity.Error(context, 503, "frontend", "Build the production frontend before starting Nordlink.", "npm run build in frontend");
    }
});
await app.StartAsync();
security.Origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
if (!security.Origin.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)) throw new InvalidOperationException("Non-loopback binding rejected.");
Directory.CreateDirectory(Path.GetDirectoryName(options.LaunchFile)!);
var temporaryLaunch = options.LaunchFile + ".tmp-" + Guid.NewGuid().ToString("N");
var launchOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
if (!OperatingSystem.IsWindows()) launchOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
await using (var output = new FileStream(temporaryLaunch, launchOptions))
    await JsonSerializer.SerializeAsync(output, new { url = security.Origin + "/#session=" + security.LaunchToken, origin = security.Origin });
File.Move(temporaryLaunch, options.LaunchFile, overwrite: true);
Console.WriteLine($"Nordlink bound to {security.Origin}. Open the private launch URL from {options.LaunchFile} within five minutes. Treat that file as a credential; it is deleted after bootstrap.");
try { await app.WaitForShutdownAsync(); }
finally { await connection.DisposeAsync(); if (File.Exists(options.LaunchFile)) File.Delete(options.LaunchFile); await app.DisposeAsync(); }

public record LaunchOptions(int Port, string DataRoot, string WorkerRoot, string LaunchFile)
{
    public static LaunchOptions Parse(string[] args)
    {
        var forbidden = new[] { "ASPNETCORE_URLS", "DOTNET_URLS", "ASPNETCORE_HTTP_PORTS", "ASPNETCORE_HTTPS_PORTS" };
        if (forbidden.Any(k => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k))) ||
            Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(k => k.StartsWith("Kestrel__", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Network binding overrides are forbidden. Nordlink binds only 127.0.0.1.");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var data = Path.Combine(local, "Nordlink"); var workers = Path.Combine(AppContext.BaseDirectory, "workers");
        string? launch = null; var port = 0;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length) throw new ArgumentException("Missing launch option value.");
            switch (args[i])
            {
                case "--port": if (!int.TryParse(args[i + 1], out port) || port < 0 || port > 65535) throw new ArgumentException("Invalid loopback port."); break;
                case "--data-root": data = Path.GetFullPath(args[i + 1]); break;
                case "--worker-root": workers = Path.GetFullPath(args[i + 1]); break;
                case "--launch-file": launch = Path.GetFullPath(args[i + 1]); break;
                default: throw new ArgumentException("Unknown option; network-wide binding cannot be configured.");
            }
        }
        return new(port, data, workers, launch ?? Path.Combine(data, "launch.json"));
    }
}
