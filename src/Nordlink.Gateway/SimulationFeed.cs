using Microsoft.AspNetCore.SignalR;
using Nordlink.Contracts;

namespace Nordlink.Gateway;

public sealed class SimulationFeed(ConnectionService connection, IHubContext<ProgressHub> hub) : BackgroundService
{
    private int enabled;
    private long revision;
    public void Start()
    {
        var state = connection.Snapshot;
        if (state.Environment != EnvironmentKind.Simulation || !state.VehicleCommunicationConfirmed) throw new InvalidOperationException("Connect the simulation fixture before starting its sample feed.");
        revision = state.Revision; Volatile.Write(ref enabled, 1);
    }
    public void Stop() => Volatile.Write(ref enabled, 0);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200)); var index = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (Volatile.Read(ref enabled) == 0) continue;
                await connection.Scheduler.Poll(async token =>
                {
                    var state = connection.Snapshot;
                    if (state.Environment != EnvironmentKind.Simulation || !state.VehicleCommunicationConfirmed || state.Revision != revision) { Stop(); return; }
                    var gap = ++index % 17 == 0;
                    var sample = new ParameterSample(DateTimeOffset.UtcNow, "fixture.voltage", "V", gap ? null : Math.Round(12.4 + 0.2 * Math.Sin(index * 0.4), 3), gap);
                    await hub.Clients.All.SendAsync("live", new LiveValueEvent(sample, state.Vehicle!.Context with { Source = "explicit simulation SignalR feed", ObservedAt = sample.Timestamp }), token);
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
