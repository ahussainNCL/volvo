// Generated from Nordlink.Contracts. Do not edit; run scripts/generate-contracts.sh.

export interface ApiError {
  capability: string;
  reason: string;
  missingPrerequisites: string[];
}

export interface ArtifactImport {
  contentBase64: string;
  source: string;
  licence: string;
  moduleApplicability: string[];
  expectedSha256: string;
  kind: string;
}

export interface ArtifactMetadata {
  sha256: string;
  source: string;
  licence: string;
  moduleApplicability: string[];
  kind: string;
  size: number;
  context: EvidenceContext;
}

export interface BootstrapRequest {
  token: string;
}

export interface CapabilityDescriptor {
  id: string;
  category: string;
  title: string;
  sourceClaim: string;
  exactOperation: string | null;
  moduleApplicability: string[];
  dependencies: string[];
  acceptanceTest: string;
  evidence: string[];
  state: EvidenceState;
  implementationStatus: string;
  implementationEvidence: string[];
}

export interface ConfigurationBackup {
  id: string;
  artifactHash: string;
  format: string;
  context: EvidenceContext;
}

export type ConnectionPhase = "disconnected" | "inspecting" | "driverDiscovered" | "interfaceOpen" | "vehicleConfirmed" | "faulted";

export interface ConnectionSnapshot {
  environment: EnvironmentKind;
  phase: ConnectionPhase;
  interfaceAvailable: boolean;
  vehicleCommunicationConfirmed: boolean;
  vehicle: VehicleProfile | null;
  drivers: DriverRegistration[];
  blockers: string[];
  revision: number;
  versions: InterfaceVersionInfo | null;
}

export interface ConnectRequest {
  driverPath: string | null;
}

export interface DiagnosticReport {
  id: string;
  codes: DiagnosticTroubleCode[];
  blockers: string[];
  context: EvidenceContext;
}

export interface DiagnosticTroubleCode {
  moduleId: string;
  code: string;
  description: string | null;
  status: string;
}

export interface DriverRegistration {
  name: string;
  registryView: string;
  path: string;
  exists: boolean;
  architecture: string;
  fileVersion: string | null;
  publisher: string | null;
  problem: string | null;
}

export type EnvironmentKind = "hardware" | "simulation";

export interface EnvironmentRequest {
  environment: EnvironmentKind;
}

export interface EvidenceContext {
  environment: EnvironmentKind;
  vehicleId: string | null;
  moduleId: string | null;
  source: string;
  observedAt: string;
  evidenceId: string | null;
}

export type EvidenceState = "unknown" | "unsupported" | "blocked" | "implementedUnverified" | "hardwareVerified" | "equipmentNonApplicable";

export interface ExecuteRequest {
  approvalToken: string;
}

export interface FirmwarePackage {
  id: string;
  artifactHash: string;
  source: string;
  licence: string;
  moduleApplicability: string[];
  context: EvidenceContext;
}

export interface InterfaceVersionInfo {
  driver: string;
  firmware: string;
  api: string;
  context: EvidenceContext;
}

export type JobState = "reviewed" | "running" | "completed" | "failed" | "interrupted";

export interface LiveValueEvent {
  sample: ParameterSample;
  context: EvidenceContext;
}

export interface ModuleIdentity {
  id: string;
  name: string;
  partNumber: string | null;
  softwareVersion: string | null;
  hardwareVersion: string | null;
  context: EvidenceContext;
}

export interface OperationJob {
  id: string;
  capability: string;
  vehicleId: string;
  moduleId: string;
  inputs: Record<string, string>;
  summary: string;
  state: JobState;
  createdAt: string;
  expiresAt: string;
  failure: string | null;
  context: EvidenceContext;
}

export interface ParameterDefinition {
  id: string;
  moduleId: string;
  name: string;
  unit: string;
  scale: number;
  offset: number;
  definitionArtifactHash: string;
  context: EvidenceContext;
}

export interface ParameterSample {
  timestamp: string;
  parameterId: string;
  unit: string;
  value: number | null;
  gap: boolean;
}

export interface ProgressEvent {
  jobId: string;
  state: JobState;
  message: string;
  timestamp: string;
  context: EvidenceContext;
}

export interface Recording {
  id: string;
  samples: ParameterSample[];
  context: EvidenceContext;
}

export interface ReviewRequest {
  capability: string;
  vehicleId: string;
  moduleId: string;
  inputs: Record<string, string>;
}

export interface ReviewResponse {
  job: OperationJob;
  approvalToken: string;
}

export interface SecurityCredential {
  id: string;
  vehicleId: string;
  moduleId: string;
  label: string;
  protection: string;
  createdAt: string;
  context: EvidenceContext;
}

export interface VehicleProfile {
  id: string;
  vin: string | null;
  model: string | null;
  modelYear: number | null;
  modules: ModuleIdentity[];
  context: EvidenceContext;
}

export interface WorkerRequest {
  version: number;
  requestId: string;
  command: string;
  driverPath: string | null;
}

export interface WorkerResponse {
  version: number;
  requestId: string;
  success: boolean;
  error: string | null;
  drivers: DriverRegistration[];
  interfaceOpen: boolean;
  driverVersion: string | null;
  firmwareVersion: string | null;
  apiVersion: string | null;
  vehicleCommunicationConfirmed: boolean;
}
