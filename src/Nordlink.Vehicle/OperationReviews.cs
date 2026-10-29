using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Nordlink.Contracts;
using Nordlink.Storage;

namespace Nordlink.Vehicle;

public sealed class OperationReviews
{
    private record Approval(string Hash, long Revision, DateTimeOffset ExpiresAt, string JobDigest);
    private readonly ConcurrentDictionary<string, Approval> approvals = new();
    public ReviewResponse Review(WorkspaceStore store, ConnectionSnapshot connection, ReviewRequest request)
    {
        if (connection.Environment != EnvironmentKind.Simulation || !connection.VehicleCommunicationConfirmed ||
            request.Capability != "simulation.exercise" || request.VehicleId != connection.Vehicle?.Id ||
            !connection.Vehicle.Modules.Any(m => m.Id == request.ModuleId))
            throw new InvalidOperationException("Only an explicitly connected simulation fixture supports this foundation operation. Vehicle writes require validated procedures.");
        if (request.Inputs.Count != 1 || !request.Inputs.TryGetValue("note", out var note) || note is null || note.Length > 120 ||
            note.Any(char.IsControl) || new[] { "pin", "secret", "password", "token" }.Any(s => note.Contains(s, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("The fixture accepts one non-sensitive note of at most 120 characters.");
        var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid().ToString("N");
        var job = new OperationJob(id, request.Capability, request.VehicleId, request.ModuleId, new(request.Inputs),
            $"SIMULATION ONLY: record fixture note ‘{note}’ for {request.VehicleId}/{request.ModuleId}. No vehicle write.",
            JobState.Reviewed, now, now.AddMinutes(5), null,
            new(EnvironmentKind.Simulation, request.VehicleId, request.ModuleId, "simulation fixture", now, null));
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        approvals[id] = new(Hash(token), connection.Revision, job.ExpiresAt, Hash(System.Text.Json.JsonSerializer.Serialize(job, WireJson.Options)));
        store.Save("job", id, job); store.Audit("operation.reviewed", id);
        return new(job, token);
    }
    public OperationJob Consume(WorkspaceStore store, ConnectionSnapshot connection, string id, string token)
    {
        if (!approvals.TryRemove(id, out var approval) || approval.ExpiresAt < DateTimeOffset.UtcNow ||
            approval.Revision != connection.Revision || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(approval.Hash), Encoding.ASCII.GetBytes(Hash(token))))
            throw new InvalidOperationException("Approval is expired, stale, invalid or already used; review again.");
        var job = store.Get<OperationJob>("job", id) ?? throw new InvalidOperationException("Review not found.");
        if (job.State != JobState.Reviewed || job.ExpiresAt < DateTimeOffset.UtcNow ||
            Hash(System.Text.Json.JsonSerializer.Serialize(job, WireJson.Options)) != approval.JobDigest || job.Context.Environment != connection.Environment ||
            job.VehicleId != connection.Vehicle?.Id || !connection.Vehicle.Modules.Any(m => m.Id == job.ModuleId))
            throw new InvalidOperationException("Vehicle/module identity changed; review again.");
        var running = job with { State = JobState.Running }; store.Save("job", id, running); store.Audit("operation.executed", id);
        return running;
    }
    public void Invalidate() => approvals.Clear();
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
