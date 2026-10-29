using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nordlink.Contracts;

public enum EnvironmentKind { Hardware, Simulation }
public enum EvidenceState { Unknown, Unsupported, Blocked, ImplementedUnverified, HardwareVerified, EquipmentNonApplicable }
public enum ConnectionPhase { Disconnected, Inspecting, DriverDiscovered, InterfaceOpen, VehicleConfirmed, Faulted }
public enum JobState { Reviewed, Running, Completed, Failed, Interrupted }

public static class WireJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        MaxDepth = 32,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };
}

public record EvidenceContext(EnvironmentKind Environment, string? VehicleId, string? ModuleId,
    string Source, DateTimeOffset ObservedAt, string? EvidenceId);
public record VehicleProfile(string Id, string? Vin, string? Model, int? ModelYear,
    ModuleIdentity[] Modules, EvidenceContext Context);
public record ModuleIdentity(string Id, string Name, string? PartNumber, string? SoftwareVersion,
    string? HardwareVersion, EvidenceContext Context);
public record CapabilityDescriptor(string Id, string Category, string Title, string SourceClaim,
    string? ExactOperation, string[] ModuleApplicability, string[] Dependencies, string AcceptanceTest,
    string[] Evidence, EvidenceState State, string ImplementationStatus, string[] ImplementationEvidence);
public record DiagnosticTroubleCode(string ModuleId, string Code, string? Description, string Status);
public record DiagnosticReport(string Id, DiagnosticTroubleCode[] Codes, string[] Blockers, EvidenceContext Context);
public record ParameterDefinition(string Id, string ModuleId, string Name, string Unit, double Scale,
    double Offset, string DefinitionArtifactHash, EvidenceContext Context);
public record ParameterSample(DateTimeOffset Timestamp, string ParameterId, string Unit, double? Value, bool Gap);
public record Recording(string Id, ParameterSample[] Samples, EvidenceContext Context);
// Metadata only. Encrypted credential material has a separate store and no wire DTO.
public record SecurityCredential(string Id, string VehicleId, string ModuleId, string Label,
    string Protection, DateTimeOffset CreatedAt, EvidenceContext Context);
public record ConfigurationBackup(string Id, string ArtifactHash, string Format, EvidenceContext Context);
public record FirmwarePackage(string Id, string ArtifactHash, string Source, string Licence,
    string[] ModuleApplicability, EvidenceContext Context);
public record OperationJob(string Id, string Capability, string VehicleId, string ModuleId,
    Dictionary<string, string> Inputs, string Summary, JobState State, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, string? Failure, EvidenceContext Context);
public record ApiError(string Capability, string Reason, string[] MissingPrerequisites);
public record DriverRegistration(string Name, string RegistryView, string Path, bool Exists,
    string Architecture, string? FileVersion, string? Publisher, string? Problem);
public record InterfaceVersionInfo(string Driver, string Firmware, string Api, EvidenceContext Context);
public record ConnectionSnapshot(EnvironmentKind Environment, ConnectionPhase Phase, bool InterfaceAvailable,
    bool VehicleCommunicationConfirmed, VehicleProfile? Vehicle, DriverRegistration[] Drivers,
    string[] Blockers, long Revision, InterfaceVersionInfo? Versions = null);
public record EnvironmentRequest(EnvironmentKind Environment);
public record BootstrapRequest(string Token);
public record ConnectRequest(string? DriverPath);
public record ReviewRequest(string Capability, string VehicleId, string ModuleId, Dictionary<string, string> Inputs);
public record ReviewResponse(OperationJob Job, string ApprovalToken);
public record ExecuteRequest(string ApprovalToken);
public record ArtifactImport(string ContentBase64, string Source, string Licence, string[] ModuleApplicability,
    string ExpectedSha256, string Kind);
public record ArtifactMetadata(string Sha256, string Source, string Licence, string[] ModuleApplicability,
    string Kind, long Size, EvidenceContext Context);
public record WorkerRequest(int Version, string RequestId, string Command, string? DriverPath);
public record WorkerResponse(int Version, string RequestId, bool Success, string? Error,
    DriverRegistration[] Drivers, bool InterfaceOpen, string? DriverVersion, string? FirmwareVersion,
    string? ApiVersion, bool VehicleCommunicationConfirmed);
public record ProgressEvent(string JobId, JobState State, string Message, DateTimeOffset Timestamp, EvidenceContext Context);
public record LiveValueEvent(ParameterSample Sample, EvidenceContext Context);
