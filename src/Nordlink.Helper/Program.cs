using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Nordlink.Contracts;

var development = args.Contains("--development-loopback");
string? cloud = null;
var gatewayRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nordlink");
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--development-loopback") continue;
    if (i + 1 >= args.Length) throw new ArgumentException("Missing helper option.");
    switch (args[i++])
    {
        case "--cloud": cloud = args[i]; break;
        case "--gateway-root": gatewayRoot = Path.GetFullPath(args[i]); break;
        case "--data-root": dataRoot = Path.GetFullPath(args[i]); break;
        default: throw new ArgumentException("Unknown helper option.");
    }
}
if (!OperatingSystem.IsWindows() && !development) throw new PlatformNotSupportedException("Hardware helper requires Windows. Linux testing requires explicit loopback development mode.");
if (!Uri.TryCreate(cloud, UriKind.Absolute, out var cloudUri) || cloudUri.AbsolutePath != "/" || cloudUri.Query.Length != 0 || cloudUri.Fragment.Length != 0 || cloudUri.UserInfo.Length != 0 || cloud != cloudUri.GetLeftPart(UriPartial.Authority) ||
    (cloudUri.Scheme != "https" && !(development && cloudUri.Host == "127.0.0.1" && cloudUri.Scheme == "http")))
    throw new ArgumentException("Supply --cloud with the exact HTTPS website origin. HTTP is only available for explicit 127.0.0.1 development.");
Console.WriteLine("Nordlink helper: hardware runs on this PC. Pair only with your own Nordlink website. No vehicle write is retried.");
var code = Environment.GetEnvironmentVariable("NORDLINK_PAIRING_CODE");
if (string.IsNullOrEmpty(code))
{
    Console.Write("Enter the one-time code from your signed-in website: ");
    var entry = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (entry.Length > 0) entry.Length--; }
        else if (!char.IsControl(key.KeyChar) && entry.Length < 16) entry.Append(key.KeyChar);
    }
    Console.WriteLine(); code = entry.ToString();
}
if (code.Length != 16) throw new ArgumentException("Pairing code must contain 16 characters.");
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
using var cloudHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = cloudUri, Timeout = TimeSpan.FromSeconds(15) };
cloudHttp.DefaultRequestHeaders.Add("Origin", cloud);
using var pairResponse = await cloudHttp.PostAsJsonAsync("/api/helper/pair", new HelperPairRequest(code), WireJson.Options, stop.Token);
code = "";
if (!pairResponse.IsSuccessStatusCode) throw new InvalidOperationException("Pairing failed. Create a new code in your website; no credentials were saved.");
var paired = await pairResponse.Content.ReadFromJsonAsync<HelperPairResponse>(WireJson.Options, stop.Token) ?? throw new InvalidDataException("Invalid pairing reply.");
var launch = Path.Combine(dataRoot, "launch-helper-" + Guid.NewGuid().ToString("N") + ".json");
Directory.CreateDirectory(dataRoot);
var executable = Path.Combine(gatewayRoot, "Nordlink.Gateway.exe");
ProcessStartInfo start;
if (OperatingSystem.IsWindows()) start = new(executable);
else
{
    start = new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet");
    start.ArgumentList.Add(Path.Combine(gatewayRoot, "Nordlink.Gateway.dll"));
}
foreach (var option in new[] { "--data-root", dataRoot, "--worker-root", Path.Combine(gatewayRoot, "workers"), "--launch-file", launch }) start.ArgumentList.Add(option);
start.UseShellExecute = false; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
using var gateway = Process.Start(start) ?? throw new IOException("Local gateway could not start.");
_ = gateway.StandardOutput.ReadToEndAsync(); _ = gateway.StandardError.ReadToEndAsync();
HttpClient? localHttp = null; HubConnection? events = null;
using var socket = new ClientWebSocket(); var sendGate = new SemaphoreSlim(1, 1);
try
{
    using var startup = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); startup.CancelAfter(TimeSpan.FromSeconds(20));
    while (!File.Exists(launch))
    {
        if (gateway.HasExited) throw new IOException("Local gateway failed. Close any existing Nordlink instance for this data directory and check the portable build.");
        await Task.Delay(100, startup.Token);
    }
    using var descriptor = JsonDocument.Parse(await File.ReadAllTextAsync(launch, startup.Token));
    var localOrigin = descriptor.RootElement.GetProperty("origin").GetString()!;
    var launchUri = new Uri(descriptor.RootElement.GetProperty("url").GetString()!);
    if (!Uri.TryCreate(localOrigin, UriKind.Absolute, out var localUri) || localUri.Scheme != "http" || localUri.Host != "127.0.0.1" || launchUri.GetLeftPart(UriPartial.Authority) != localOrigin)
        throw new InvalidDataException("Local gateway must bind exclusively to 127.0.0.1.");
    var cookies = new CookieContainer();
    localHttp = new(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false }) { BaseAddress = localUri, Timeout = TimeSpan.FromSeconds(15) };
    localHttp.DefaultRequestHeaders.Add("Origin", localOrigin);
    using var bootstrap = await localHttp.PostAsJsonAsync("/api/session", new BootstrapRequest(launchUri.Fragment["#session=".Length..]), WireJson.Options, startup.Token);
    bootstrap.EnsureSuccessStatusCode();
    socket.Options.SetRequestHeader("Authorization", "Bearer " + paired.Token);
    socket.Options.SetRequestHeader("Origin", cloud); socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
    var websocket = new UriBuilder(cloudUri) { Scheme = cloudUri.Scheme == "https" ? "wss" : "ws", Path = "/relay/helper" };
    await socket.ConnectAsync(websocket.Uri, stop.Token);
    events = new HubConnectionBuilder().WithUrl(localOrigin + "/hubs/progress", o =>
    { o.Cookies = cookies; o.Headers["Origin"] = localOrigin; o.Transports = HttpTransportType.WebSockets; o.SkipNegotiation = true; })
        .AddJsonProtocol(o => o.PayloadSerializerOptions = WireJson.Options).Build();
    events.On<ProgressEvent>("progress", value => Forward("progress", value));
    events.On<LiveValueEvent>("live", value => Forward("live", value));
    await events.StartAsync(stop.Token);
    Console.WriteLine("Helper paired and connected. Return to your website. Closing this helper ends the connection; reconnecting requires a fresh code.");
    stop.CancelAfter(paired.ExpiresAt - DateTimeOffset.UtcNow);
    while (await WebSocketFrames.Read(socket, stop.Token) is { } frame)
    {
        if (!WebSocketFrames.Allowed(frame.Kind, frame.Route)) throw new InvalidDataException("Cloud requested an unapproved route. Connection stopped.");
        using var request = new HttpRequestMessage(new HttpMethod(frame.Kind), frame.Route);
        if (frame.Kind == "POST") request.Content = new StringContent(frame.Body ?? "{}", Encoding.UTF8, "application/json");
        try
        {
            using var response = await localHttp.SendAsync(request, stop.Token);
            var mediaType = response.Content.Headers.ContentType?.MediaType == "text/csv" ? "text/csv" : "application/json";
            if (response.Content.Headers.ContentLength > WebSocketFrames.MaxBytes / 2) throw new InvalidDataException("Response too large for relay.");
            var body = await response.Content.ReadAsStringAsync(stop.Token);
            if (Encoding.UTF8.GetByteCount(body) > WebSocketFrames.MaxBytes / 2) throw new InvalidDataException("Response too large for relay.");
            await WebSocketFrames.Send(socket, new(1, frame.RequestId, "response", frame.Route, body, (int)response.StatusCode, mediaType), sendGate, stop.Token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !stop.IsCancellationRequested)
        {
            var error = JsonSerializer.Serialize(new ApiError("local-helper", "Local request failed or timed out. Outcome may be unknown; no automatic retry.", []), WireJson.Options);
            await WebSocketFrames.Send(socket, new(1, frame.RequestId, "response", frame.Route, error, 503, "application/json"), sendGate, stop.Token);
        }
    }
    async Task Forward<T>(string kind, T value)
    {
        if (socket.State == WebSocketState.Open)
            await WebSocketFrames.Send(socket, new(1, Guid.NewGuid().ToString("N"), kind, "", JsonSerializer.Serialize(value, WireJson.Options), 200, "application/json"), sendGate, stop.Token);
    }
}
catch (Exception e) when (e is OperationCanceledException or WebSocketException or IOException or InvalidDataException)
{ Console.WriteLine("Helper connection ended. Inspect job history before retrying; reconnect explicitly with a new pairing code."); }
finally
{
    if (events is not null) await events.DisposeAsync();
    if (localHttp is not null)
    {
        try { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3)); using var response = await localHttp.PostAsJsonAsync("/api/disconnect", new { }, deadline.Token); }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { }
        localHttp.Dispose();
    }
    if (!gateway.HasExited) { gateway.Kill(entireProcessTree: true); await gateway.WaitForExitAsync(); }
    if (File.Exists(launch)) File.Delete(launch);
}
