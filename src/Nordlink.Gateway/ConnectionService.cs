using Nordlink.Contracts;
using Nordlink.Storage;
using Nordlink.Vehicle;

namespace Nordlink.Gateway;

public sealed class ConnectionService : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly string dataRoot, workerRoot;
    private WorkerSupervisor? worker;
    private ConnectionSnapshot snapshot = new(EnvironmentKind.Hardware, ConnectionPhase.Disconnected, false, false, null, [], ["Select an environment, inspect driver health, then connect."], 0);
    public readonly CommunicationScheduler Scheduler = new();
    public readonly OperationReviews Reviews = new();
    public ConnectionSnapshot Snapshot { get { lock (sync) return snapshot; } }
    public WorkspaceStore Store => new(dataRoot, Snapshot.Environment);
    public ConnectionService(string dataRoot, string workerRoot)
    {
        this.dataRoot = dataRoot; this.workerRoot = workerRoot;
        foreach (var env in Enum.GetValues<EnvironmentKind>()) new WorkspaceStore(dataRoot, env).RecoverInterruptedJobs();
    }
    private void Update(Func<ConnectionSnapshot, ConnectionSnapshot> update)
    { lock (sync) { snapshot = update(snapshot) with { Revision = snapshot.Revision + 1 }; Reviews.Invalidate(); } }
    public void ChangeEnvironment(EnvironmentKind environment)
    {
        if (Snapshot.Phase != ConnectionPhase.Disconnected) throw new InvalidOperationException("Disconnect before switching environments.");
        Update(s => new(environment, ConnectionPhase.Disconnected, false, false, null, [], [environment == EnvironmentKind.Hardware ? "Windows and the official TOPDON installation are required." : "Simulation is isolated and cannot establish vehicle evidence."], s.Revision));
    }
    public async Task<ConnectionSnapshot> Inspect(CancellationToken ct) => await Scheduler.Exclusive(async token =>
    {
        if (Snapshot.Phase != ConnectionPhase.Disconnected) throw new InvalidOperationException("Disconnect before inspecting driver registrations again.");
        if (Snapshot.Environment == EnvironmentKind.Simulation)
        { Update(s => s with { Drivers = [], Blockers = ["Simulation fixture available. No hardware driver is used."] }); return Snapshot; }
        if (!OperatingSystem.IsWindows())
        { Update(s => s with { Drivers = [], Blockers = ["TOPDON registry/PE inspection requires Windows. The reported missing DLL has not been inspected on this machine."] }); return Snapshot; }
        Update(s => s with { Phase = ConnectionPhase.Inspecting });
        await ReplaceWorker("x64");
        var response = await worker!.Request("discover", ct: token);
        if (!response.Success) throw new IOException("Driver discovery failed.");
        Update(s => s with { Phase = ConnectionPhase.Disconnected, Drivers = response.Drivers,
            Blockers = response.Drivers.Length == 0 ? ["No official TOPDON J2534 04.04 registration found in either registry view."] : response.Drivers.Where(d => d.Problem is not null).Select(d => d.Problem!).ToArray() });
        await worker.DisposeAsync(); worker = null; return Snapshot;
    }, ct);

    public async Task<ConnectionSnapshot> Connect(string? driverPath, CancellationToken ct) => await Scheduler.Exclusive(async token =>
    {
        if (Snapshot.Phase != ConnectionPhase.Disconnected) throw new InvalidOperationException("Disconnect before reconnecting.");
        if (Snapshot.Environment == EnvironmentKind.Simulation)
        {
            var now = DateTimeOffset.UtcNow;
            var context = new EvidenceContext(EnvironmentKind.Simulation, "simulation-v40", "simulation-cem", "explicit demonstration fixture; not a real VIN or module", now, null);
            var vehicle = new VehicleProfile("simulation-v40", null, "V40 demonstration fixture", null,
                [new("simulation-cem", "CEM fixture", null, null, null, context)], context);
            Update(s => s with { Phase = ConnectionPhase.VehicleConfirmed, Vehicle = vehicle, InterfaceAvailable = true,
                VehicleCommunicationConfirmed = true, Blockers = ["SIMULATION ONLY. Real module procedures, diagnostic definitions and decoder algorithm are not available."] });
            Store.Save("vehicle", vehicle.Id, vehicle); return Snapshot;
        }
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("TOPDON requires Windows, the official driver and a connected interface.");
        var driver = Snapshot.Drivers.FirstOrDefault(d => d.Exists && d.Problem is null && d.Path == driverPath)
            ?? throw new InvalidOperationException("Inspect driver health and select a valid registered TOPDON library first.");
        await ReplaceWorker(driver.Architecture);
        var response = await worker!.Request("open", driver.Path, token);
        if (!response.Success || !response.InterfaceOpen || string.IsNullOrWhiteSpace(response.DriverVersion) || string.IsNullOrWhiteSpace(response.FirmwareVersion) || string.IsNullOrWhiteSpace(response.ApiVersion))
        { await DisconnectWorker(); throw new IOException("TOPDON open/version check failed. Inspect USB, power and official installation."); }
        var versions = new InterfaceVersionInfo(response.DriverVersion, response.FirmwareVersion, response.ApiVersion,
            new(EnvironmentKind.Hardware, null, null, "official TOPDON PassThruReadVersion", DateTimeOffset.UtcNow, null));
        Update(s => s with { Phase = ConnectionPhase.InterfaceOpen, InterfaceAvailable = true, VehicleCommunicationConfirmed = false, Versions = versions,
            Blockers = ["Interface opened and versions read. Vehicle communication, voltage, VIN and module identities remain unverified: exact bus/addressing/session definitions required."] });
        Store.Save("interface", Guid.NewGuid().ToString("N"), versions); Store.Audit("interface.opened", driver.Name); return Snapshot;
    }, ct);
    public async Task Disconnect(CancellationToken ct) => await Scheduler.Exclusive(async _ =>
    {
        await DisconnectWorker(); Update(s => s with { Phase = ConnectionPhase.Disconnected, Vehicle = null, Versions = null, InterfaceAvailable = false, VehicleCommunicationConfirmed = false, Blockers = ["Disconnected. Reconnection is explicit."] }); return true;
    }, ct);
    private async Task ReplaceWorker(string architecture)
    {
        await DisconnectWorker(); worker = new(pipe => WorkerSupervisor.WindowsLauncher(workerRoot, architecture, pipe));
        worker.UnexpectedExit += () => Update(s => s with { Phase = ConnectionPhase.Faulted, InterfaceAvailable = false,
            VehicleCommunicationConfirmed = false, Vehicle = null, Blockers = ["Worker exited or timed out. Affected work terminated; disconnect then reconnect explicitly."] });
    }
    private async Task DisconnectWorker()
    {
        var active = worker; worker = null;
        if (active is null) return;
        try
        {
            if (Snapshot.Phase == ConnectionPhase.InterfaceOpen)
            {
                var response = await active.Request("close");
                if (!response.Success) throw new IOException("Official J2534 close failed; worker will be terminated without retry.");
            }
        }
        finally { await active.DisposeAsync(); }
    }
    public async ValueTask DisposeAsync() => await DisconnectWorker();
}
