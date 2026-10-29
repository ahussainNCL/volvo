using Nordlink.Contracts;

namespace Nordlink.Vehicle;

// Contracts for later sourced implementations. No arbitrary protocol/address probing,
// temporary-access substitution or invented decoder algorithm is implemented here.
public interface IVehicleTransport
{
    string DefinitionArtifactHash { get; }
    Task<ModuleIdentity[]> IdentifyRespondingModules(CancellationToken ct);
    Task<VehicleProfile> IdentifyVehicle(CancellationToken ct);
}
public interface IDiagnosticSession
{
    ModuleIdentity Module { get; }
    Task<DiagnosticReport> Scan(CancellationToken ct);
    Task<ParameterDefinition[]> DiscoverParameters(CancellationToken ct);
    Task<ParameterSample[]> ReadParameters(string[] parameterIds, CancellationToken ct);
}
public interface ISecurityAccess
{
    string ProcedureId { get; }
    string AlgorithmVersion { get; }
    string[] RequiredEvidence { get; }
    Task<SecurityCredential> RecoverAndIndependentlyVerifyReusableCredential(ModuleIdentity module, CancellationToken ct);
}
