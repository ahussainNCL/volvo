using System.IO.Pipes;
using Nordlink.Contracts;
using Nordlink.HardwareWorker;

if (args.Length != 2 || args[0] != "--pipe" || !args[1].StartsWith("nordlink-", StringComparison.Ordinal) || args[1].Length > 100) return 2;
using var lifetime = new CancellationTokenSource(TimeSpan.FromHours(8));
using var pipe = new NamedPipeClientStream(".", args[1], PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
using var device = new J2534Device();
try
{
    await pipe.ConnectAsync(10_000, lifetime.Token);
    while (pipe.IsConnected)
    {
        var request = await PipeProtocol.Read<WorkerRequest>(pipe, lifetime.Token);
        if (request.Version != PipeProtocol.Version || !Guid.TryParseExact(request.RequestId, "N", out _)) return 3;
        WorkerResponse response;
        try
        {
            response = request.Command switch
            {
                "discover" => new(1, request.RequestId, true, null, DriverDiscovery.Discover(), false, null, null, null, false),
                "open" => Open(request),
                "close" => Close(request),
                _ => new(1, request.RequestId, false, "Command unsupported; no vehicle protocol definitions installed.", [], false, null, null, null, false)
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or BadImageFormatException or DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        { response = new(1, request.RequestId, false, "Driver operation failed. Inspect the official driver installation, architecture, power and USB. No vehicle operation was retried.", [], false, null, null, null, false); }
        await PipeProtocol.Write(pipe, response, lifetime.Token);
    }
}
catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException or System.Text.Json.JsonException) { return 4; }
return 0;

WorkerResponse Open(WorkerRequest r)
{
    var v = device.Open(r.DriverPath ?? throw new ArgumentException("Driver path required."));
    return new(1, r.RequestId, true, null, [], true, v.Driver, v.Firmware, v.Api, false);
}
WorkerResponse Close(WorkerRequest r)
{ device.Dispose(); return new(1, r.RequestId, true, null, [], false, null, null, null, false); }
