using System.Security.Cryptography;
using Nordlink.Contracts;
using Nordlink.Storage;

namespace Nordlink.Providers;

// Storage validation is not semantic validation of a firmware or definition format.
public sealed class LocalArtifacts(WorkspaceStore store)
{
    public ArtifactMetadata Import(ArtifactImport request, EvidenceContext context)
    {
        if (context.Environment != store.Environment) throw new ArgumentException("Artifact environment mismatch.");
        if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.Licence) ||
            request.ModuleApplicability.Length == 0 || request.ModuleApplicability.Any(string.IsNullOrWhiteSpace) ||
            request.Kind is not ("definitions" or "firmware" or "repair" or "configuration"))
            throw new ArgumentException("Source, licence, exact module applicability and a supported artifact kind are required.");
        if (request.ContentBase64.Length > 6_000_000) throw new ArgumentException("Import limit is 4 MiB.");
        var bytes = Convert.FromBase64String(request.ContentBase64);
        if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024) throw new ArgumentException("Import limit is 4 MiB; empty artifacts are rejected.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!hash.Equals(request.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("SHA-256 does not match the supplied manifest.");
        var destination = Path.Combine(store.Root, "artifacts", hash);
        if (!File.Exists(destination))
        {
            using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
        }
        else if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(destination)), SHA256.HashData(bytes)))
            throw new InvalidDataException("Existing artifact content has changed.");
        var metadata = new ArtifactMetadata(hash, request.Source, request.Licence, request.ModuleApplicability,
            request.Kind, bytes.Length, context);
        store.Save("artifact", hash, metadata); store.Audit("artifact.imported", hash);
        return metadata;
    }
}

// These adapters deliberately have no undocumented online integrations or fake results.
public interface IDiagnosticDefinitions { Task<ParameterDefinition[]> Parameters(ModuleIdentity module, CancellationToken ct); }
public interface IFirmwareCatalogue { Task<FirmwarePackage[]> Find(VehicleProfile vehicle, ModuleIdentity module, CancellationToken ct); }
public interface IRepairInformation { Task<string[]> Search(string query, VehicleProfile vehicle, CancellationToken ct); }
public interface ISecurityProcedure { string ProcedureId { get; } Task<string[]> Prerequisites(ModuleIdentity module, CancellationToken ct); }
