using System.Diagnostics;
using System.IO.Pipes;
using Nordlink.Contracts;

namespace Nordlink.Gateway;

public sealed class WorkerSupervisor : IAsyncDisposable
{
    private readonly Func<string, ProcessStartInfo> launcher;
    private readonly TimeSpan timeout;
    private readonly SemaphoreSlim requests = new(1, 1);
    private NamedPipeServerStream? pipe;
    private Process? process;
    public event Action? UnexpectedExit;
    public WorkerSupervisor(Func<string, ProcessStartInfo> launcher, TimeSpan? timeout = null)
    { this.launcher = launcher; this.timeout = timeout ?? TimeSpan.FromSeconds(8); }

    public async Task<WorkerResponse> Request(string command, string? path = null, CancellationToken ct = default)
    {
        await requests.WaitAsync(ct);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(timeout);
            try
            {
                if (pipe is null) await Start(deadline.Token);
                var id = Guid.NewGuid().ToString("N");
                await PipeProtocol.Write(pipe!, new WorkerRequest(1, id, command, path), deadline.Token);
                var response = await PipeProtocol.Read<WorkerResponse>(pipe!, deadline.Token);
                if (response.Version != 1 || response.RequestId != id || response.VehicleCommunicationConfirmed)
                    throw new InvalidDataException("Unexpected worker protocol response.");
                return response;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Stop(); UnexpectedExit?.Invoke();
                throw new IOException("Hardware worker failed, timed out or returned malformed data. Connection closed; reconnect explicitly. No vehicle writes were retried.", ex);
            }
        }
        finally { requests.Release(); }
    }
    private async Task Start(CancellationToken ct)
    {
        var name = "nordlink-" + Guid.NewGuid().ToString("N");
        pipe = new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var start = launcher(name); start.UseShellExecute = false; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        process = Process.Start(start) ?? throw new IOException("Worker could not start.");
        _ = Drain(process.StandardOutput); _ = Drain(process.StandardError);
        var observed = process;
        _ = ObserveExit(observed);
        await pipe.WaitForConnectionAsync(ct);
    }
    private static async Task Drain(StreamReader reader)
    {
        var buffer = new char[2048];
        try { while (await reader.ReadAsync(buffer) > 0) { } }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }
    private async Task ObserveExit(Process observed)
    {
        try
        {
            await observed.WaitForExitAsync();
            if (ReferenceEquals(process, observed)) { pipe?.Dispose(); UnexpectedExit?.Invoke(); }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
    }
    private void Stop()
    {
        var old = process; process = null; pipe?.Dispose(); pipe = null;
        if (old is not null)
        {
            try { if (!old.HasExited) old.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            old.Dispose();
        }
    }
    public async ValueTask DisposeAsync() { await requests.WaitAsync(); try { Stop(); } finally { requests.Release(); } }
    public static ProcessStartInfo WindowsLauncher(string root, string architecture, string pipe)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Hardware execution requires Windows.");
        if (architecture is not ("x86" or "x64")) throw new ArgumentException("Unsupported driver architecture.");
        var executable = Path.Combine(root, "win-" + architecture, "Nordlink.HardwareWorker.exe");
        if (!File.Exists(executable)) throw new IOException("Publish the matching Windows hardware worker first.");
        var start = new ProcessStartInfo(executable); start.ArgumentList.Add("--pipe"); start.ArgumentList.Add(pipe); return start;
    }
}
