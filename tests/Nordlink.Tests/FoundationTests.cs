using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nordlink.Contracts;
using Nordlink.Gateway;
using Nordlink.HardwareWorker;
using Nordlink.Providers;
using Nordlink.Storage;
using Nordlink.Vehicle;
using Xunit;

namespace Nordlink.Tests;

public sealed class FoundationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nordlink-tests-" + Guid.NewGuid().ToString("N"));
    private WorkspaceStore Store(EnvironmentKind environment = EnvironmentKind.Simulation) => new(root, environment);
    private static EvidenceContext Context(EnvironmentKind env = EnvironmentKind.Simulation) => new(env, "fixture", "cem", "test fixture", DateTimeOffset.UtcNow, null);
    private static ConnectionSnapshot Connected(long revision = 2) => new(EnvironmentKind.Simulation, ConnectionPhase.VehicleConfirmed, true, true,
        new("fixture", null, "TEST FIXTURE", null, [new("cem", "fixture", null, null, null, Context())], Context()), [], [], revision);
    private static ReviewRequest Request() => new("simulation.exercise", "fixture", "cem", new() { ["note"] = "test review" });
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact] public void HardwareAndSimulationHaveSeparateDatabasesAndArtifacts()
    {
        var simulation = Store(); var hardware = Store(EnvironmentKind.Hardware);
        simulation.Save("vehicle", "fixture", Connected().Vehicle);
        Assert.Empty(hardware.List<VehicleProfile>("vehicle")); Assert.NotEqual(simulation.Root, hardware.Root);
        Assert.Throws<InvalidOperationException>(() => hardware.Save("vehicle", "fixture", Connected().Vehicle));
        Assert.True(Directory.Exists(Path.Combine(hardware.Root, "artifacts")));
    }
    [Fact] public void RecordingRoundTripAndCsvPreserveTimestampUnitAndGap()
    {
        var now = DateTimeOffset.UtcNow;
        var recording = new Recording("capture", [new(now, "voltage", "V", 12.42, false), new(now.AddSeconds(1), "voltage", "V", null, true)], Context());
        var store = Store(); store.SaveRecording(recording); var saved = store.Get<Recording>("recording", "capture")!;
        Assert.Equal(recording.Samples, saved.Samples);
        var csv = WorkspaceStore.RecordingCsv(saved);
        Assert.Contains(now.ToString("O"), csv); Assert.Contains("\"V\",12.42,false", csv); Assert.Contains("\"V\",,true", csv);
    }
    [Fact] public void RecordingRejectsWrongEnvironmentAndUnmarkedGaps()
    {
        var store = Store();
        Assert.Throws<InvalidOperationException>(() => store.SaveRecording(new("r", [], Context(EnvironmentKind.Hardware))));
        Assert.Throws<ArgumentException>(() => store.SaveRecording(new("r", [new(DateTimeOffset.UtcNow, "x", "V", null, false)], Context())));
    }
    [Fact] public void RecordingRejectsOutOfOrderTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => Store().SaveRecording(new("r", [new(now, "x", "V", 1, false), new(now.AddSeconds(-1), "x", "V", 2, false)], Context())));
    }
    [Fact] public void CsvNeutralizesSpreadsheetFormulas()
    {
        var csv = WorkspaceStore.RecordingCsv(new("r", [new(DateTimeOffset.UtcNow, "=cmd()", "@unit", 1, false)], Context()));
        Assert.Contains("\"'=cmd()\"", csv); Assert.Contains("\"'@unit\"", csv);
    }
    [Fact] public void ArtifactImportVerifiesHashSourceLicenceAndApplicability()
    {
        var bytes = Encoding.UTF8.GetBytes("licensed test fixture"); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var request = new ArtifactImport(Convert.ToBase64String(bytes), "test", "test-only", ["fixture"], hash, "definitions");
        var importer = new LocalArtifacts(Store()); var result = importer.Import(request, Context());
        Assert.Equal(hash.ToLowerInvariant(), result.Sha256); Assert.Equal(result, importer.Import(request, Context()) with { Context = result.Context });
        Assert.Throws<ArgumentException>(() => importer.Import(request with { ExpectedSha256 = "wrong" }, Context()));
        Assert.Throws<ArgumentException>(() => importer.Import(request with { Licence = "" }, Context()));
        Assert.Throws<ArgumentException>(() => importer.Import(request with { ModuleApplicability = [] }, Context()));
    }
    [Fact] public void ArtifactImportRejectsCrossEnvironmentEvidence()
    {
        Assert.Throws<ArgumentException>(() => new LocalArtifacts(Store()).Import(new("", "source", "licence", ["cem"], "", "definitions"), Context(EnvironmentKind.Hardware)));
    }
    [Fact] public void RestartMarksReviewedAndRunningJobsInterruptedWithoutRetry()
    {
        var store = Store(); var review = new OperationReviews().Review(store, Connected(), Request());
        store.Save("job", "running", review.Job with { Id = "running", State = JobState.Running });
        Assert.Equal(2, new WorkspaceStore(root, EnvironmentKind.Simulation).RecoverInterruptedJobs());
        Assert.All(store.List<OperationJob>("job"), j => Assert.Equal(JobState.Interrupted, j.State));
        Assert.Equal(0, store.RecoverInterruptedJobs());
    }
    [Fact] public void ApprovalBindsIdentityAndInputsAndIsSingleUse()
    {
        var reviews = new OperationReviews(); var store = Store(); var request = Request();
        var review = reviews.Review(store, Connected(), request); request.Inputs["note"] = "changed afterwards";
        var running = reviews.Consume(store, Connected(), review.Job.Id, review.ApprovalToken);
        Assert.Equal("test review", running.Inputs["note"]); Assert.Equal(JobState.Running, running.State);
        Assert.Throws<InvalidOperationException>(() => reviews.Consume(store, Connected(), review.Job.Id, review.ApprovalToken));
    }
    [Fact] public void ApprovalRejectsChangedConnection()
    {
        var reviews = new OperationReviews(); var store = Store(); var review = reviews.Review(store, Connected(), Request());
        Assert.Throws<InvalidOperationException>(() => reviews.Consume(store, Connected(3), review.Job.Id, review.ApprovalToken));
    }
    [Fact] public void ExpiredReviewedJobCannotExecute()
    {
        var reviews = new OperationReviews(); var store = Store(); var review = reviews.Review(store, Connected(), Request());
        store.Save("job", review.Job.Id, review.Job with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        Assert.Throws<InvalidOperationException>(() => reviews.Consume(store, Connected(), review.Job.Id, review.ApprovalToken));
    }
    [Theory] [InlineData("pin")] [InlineData("secret")] [InlineData("password")]
    public void FixtureRejectsSensitiveInputs(string note)
    { var r = Request(); r.Inputs["note"] = note; Assert.Throws<ArgumentException>(() => new OperationReviews().Review(Store(), Connected(), r)); }
    [Fact] public void ReviewRejectsHardwareOperationsAndWrongModule()
    {
        var r = new OperationReviews();
        Assert.Throws<InvalidOperationException>(() => r.Review(Store(), Connected() with { Environment = EnvironmentKind.Hardware }, Request()));
        Assert.Throws<InvalidOperationException>(() => r.Review(Store(), Connected(), Request() with { ModuleId = "wrong" }));
        Assert.Throws<InvalidOperationException>(() => r.Review(Store(), Connected(), Request() with { Capability = "programming" }));
    }
    [Fact] public async Task ExclusiveJobSuspendsPollingAndOtherCommunication()
    {
        var scheduler = new CommunicationScheduler(); var entered = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var exclusive = scheduler.Exclusive(async _ => { entered.SetResult(); await release.Task; return true; });
        await entered.Task; Assert.True(scheduler.PollingSuspended);
        Assert.False(await scheduler.Poll(_ => throw new Exception("Must not poll")));
        var secondEntered = false; var second = scheduler.Exclusive(_ => { secondEntered = true; return Task.FromResult(true); });
        Assert.False(secondEntered); release.SetResult(); await exclusive; await second;
        Assert.True(secondEntered); Assert.False(scheduler.PollingSuspended); Assert.True(await scheduler.Poll(_ => Task.CompletedTask));
    }
    [Fact] public async Task CancelledExclusiveDoesNotKeepPollingSuspended()
    {
        var scheduler = new CommunicationScheduler(); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scheduler.Exclusive(_ => Task.FromResult(true), cts.Token));
        Assert.False(scheduler.PollingSuspended);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(PipeProtocol.MaxPayload + 1)]
    public async Task PipeRejectsInvalidLength(int length)
    {
        var bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, length);
        await Assert.ThrowsAsync<InvalidDataException>(() => PipeProtocol.Read<WorkerResponse>(new MemoryStream(bytes), CancellationToken.None));
    }
    [Fact] public async Task PipeRejectsTruncation()
    { await Assert.ThrowsAsync<EndOfStreamException>(() => PipeProtocol.Read<WorkerRequest>(new MemoryStream([5, 0, 0, 0, 123]), CancellationToken.None)); }
    [Theory] [InlineData("crash")] [InlineData("hang")] [InlineData("oversize")] [InlineData("malformed")] [InlineData("wrong-id")] [InlineData("wrong-version")] [InlineData("fabricated-vehicle")]
    public async Task WorkerFailureIsBoundedAndDoesNotCrashCaller(string mode)
    {
        await using var client = Fixture(mode); var faulted = false; client.UnexpectedExit += () => faulted = true;
        await Assert.ThrowsAsync<IOException>(() => client.Request("discover")); Assert.True(faulted);
    }
    [Fact] public async Task WorkerCanExchangeVersionedCorrelatedMessages()
    { await using var client = Fixture("ok"); Assert.True((await client.Request("discover")).Success); }
    private static WorkerSupervisor Fixture(string mode)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Nordlink.FixtureWorker/bin", configuration, "net10.0/Nordlink.FixtureWorker.dll"));
        return new(pipe => { var p = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet"); p.ArgumentList.Add(dll); p.ArgumentList.Add(pipe); p.ArgumentList.Add(mode); return p; }, TimeSpan.FromSeconds(5));
    }
    [Fact] public void MissingDllAndNonPeAreRejected()
    {
        Directory.CreateDirectory(root); var file = Path.Combine(root, "not-a-driver.dll"); File.WriteAllText(file, "not a PE");
        Assert.Throws<FileNotFoundException>(() => DriverDiscovery.Architecture(Path.Combine(root, "missing.dll")));
        Assert.ThrowsAny<BadImageFormatException>(() => DriverDiscovery.Architecture(file));
    }
    [Theory] [InlineData((ushort)0x014c, "x86")] [InlineData((ushort)0x8664, "x64")]
    public void PeMachineDeterminesWorkerArchitecture(ushort machine, string architecture)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "header-only-fixture.dll");
        var bytes = new byte[512]; bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c), 0x80);
        bytes[0x80] = (byte)'P'; bytes[0x81] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), machine);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94), (ushort)(architecture == "x86" ? 224 : 240));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x96), 0x2000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x98), (ushort)(architecture == "x86" ? 0x10b : 0x20b));
        File.WriteAllBytes(path, bytes);
        Assert.Equal(architecture, DriverDiscovery.Architecture(path));
        if (architecture != (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X86 ? "x86" : "x64"))
            Assert.Throws<BadImageFormatException>(() => DriverDiscovery.RequireArchitecture(path));
    }
    [Fact] public void SecurityCredentialWireContractHasMetadataOnly()
    {
        var metadata = new SecurityCredential(Guid.NewGuid().ToString("N"), "v", "m", "Reusable CEM credential", "DPAPI CurrentUser", DateTimeOffset.UtcNow, Context(EnvironmentKind.Hardware));
        var json = JsonSerializer.Serialize(metadata, WireJson.Options);
        Assert.DoesNotContain("pin", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("encryptedMaterial", json);
    }
    [Fact] public void SessionBootstrapIsSingleUseAndAuthenticationIsRequired()
    {
        var s = new LocalSecurity(); Assert.False(s.Authenticate(null)); Assert.Null(s.Exchange("invalid"));
        var cookie = s.Exchange(s.LaunchToken); Assert.NotNull(cookie); Assert.True(s.Authenticate(cookie));
        Assert.False(s.Authenticate(cookie + "x")); Assert.Null(s.Exchange(s.LaunchToken));
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    [Fact] public void BootstrapAndSessionExpire()
    {
        var clock = new Clock(); var expiredBootstrap = new LocalSecurity(clock);
        clock.Now = clock.Now.AddMinutes(6); Assert.Null(expiredBootstrap.Exchange(expiredBootstrap.LaunchToken));
        var active = new LocalSecurity(clock); var cookie = active.Exchange(active.LaunchToken);
        Assert.True(active.Authenticate(cookie)); clock.Now = clock.Now.AddHours(8); Assert.False(active.Authenticate(cookie));
    }
    [Fact] public void FeatureRegisterHasThirteenCategoriesAndNoUnevidencedHardwareClaims()
    {
        var register = JsonSerializer.Deserialize<CapabilityDescriptor[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "feature-register.json")), WireJson.Options)!;
        Assert.Equal(13, register.Select(f => f.Category).Distinct().Count());
        Assert.All(register, f => { Assert.NotEmpty(f.Dependencies); Assert.NotEmpty(f.AcceptanceTest); Assert.NotEmpty(f.SourceClaim); });
        Assert.All(register.Where(f => f.State is EvidenceState.HardwareVerified or EvidenceState.EquipmentNonApplicable), f => Assert.NotEmpty(f.Evidence));
        Assert.All(register.Where(f => f.Id.Contains("research")), f => Assert.Null(f.ExactOperation));
    }
    [Fact] public async Task EnvironmentSwitchRequiresDisconnectAndSimulationDoesNotPopulateHardware()
    {
        await using var service = new ConnectionService(root, root);
        service.ChangeEnvironment(EnvironmentKind.Simulation); await service.Connect(null, CancellationToken.None);
        Assert.Throws<InvalidOperationException>(() => service.ChangeEnvironment(EnvironmentKind.Hardware));
        await service.Disconnect(CancellationToken.None); service.ChangeEnvironment(EnvironmentKind.Hardware);
        Assert.Empty(service.Store.List<VehicleProfile>("vehicle")); Assert.Null(service.Snapshot.Vehicle);
    }
    [WindowsFact] public void DpapiRoundTripRequiresCurrentWindowsUser()
    {
        var store = Store(EnvironmentKind.Hardware); var vault = new DpapiCredentialVault(store);
        var id = Guid.NewGuid().ToString("N"); var material = Encoding.UTF8.GetBytes("test-only-not-a-vehicle-pin");
        vault.Save(new(id, "v", "m", "test", "DPAPI CurrentUser", DateTimeOffset.UtcNow, Context(EnvironmentKind.Hardware)), material);
        var clear = vault.Reveal(id); try { Assert.Equal(material, clear); } finally { CryptographicOperations.ZeroMemory(clear); }
        Assert.DoesNotContain(Encoding.UTF8.GetString(material), File.ReadAllText(Path.Combine(store.Root, "credentials", id + ".dpapi")));
    }
}

public sealed class WindowsFactAttribute : FactAttribute
{ public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Requires real Windows DPAPI CurrentUser; Linux simulation is not evidence."; } }
