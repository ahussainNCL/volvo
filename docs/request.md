# Nordlink implementation and validation plan

## Starting point and scope

Build in the empty `/workspace/volvo` repository. Node 24 is available; .NET requires installation. Development runs here on Linux; official TOPDON drivers, DPAPI and vehicle validation run on your separate Windows PC.

Your thirteen capability categories define the acceptance baseline. Use https://volvocartoolapp.com as an additional reference once accessible; this environment currently receives a proxy 403, so its advertised features have not been inspected.

The first deliverable is a tested foundation and connection infrastructure. Full completion remains conditional on real V40 evidence, licensed definitions and validated module procedures. Missing dependencies remain blockers, never vehicle non-applicability.

## Foundation and interfaces

- Create frontend, gateway, hardware-worker, vehicle-layer, shared-contracts, storage, providers, tests and documentation projects. Add project guidance, architecture decisions, implementation backlog, hardware-validation queue and feature register.
- Install and pin .NET 10 and the existing Node 24 toolchain. Use React, TypeScript and Vite; publish frontend assets inside ASP.NET Core.
- Define C# contracts as the source of truth and generate TypeScript API types. Include all ten required contracts: `VehicleProfile`, `ModuleIdentity`, `CapabilityDescriptor`, `DiagnosticReport`, `ParameterDefinition`, `Recording`, `SecurityCredential`, `ConfigurationBackup`, `FirmwarePackage`, `OperationJob`.
- Carry environment, identity, evidence provenance and timestamps through persisted results. Represent unknown, unsupported, blocked, implemented-unverified, hardware-verified and equipment-non-applicable states explicitly.
- Keep security credential metadata separate from encrypted material. Routine APIs, SignalR messages, logs and exports never include PINs. Explicit local reveal uses an authenticated, non-cacheable endpoint.
- Use SQLite for vehicles, operations, recordings and audit history. Store imported artifacts with SHA-256 hashes, source, licence metadata and module applicability. Use Windows DPAPI for hardware-environment credentials.
- Create separate simulation databases and artifact directories. Switching environments requires disconnection; simulated evidence cannot satisfy hardware acceptance tests.

Seed the feature register with every requested capability and empty evidence fields. Each entry records its source claim, exact operation when known, module applicability, dependencies, acceptance test and evidence. Unspecified keys, mileage, recovery and replacement categories remain research entries until decomposed into documented operations.

## Initial executable deliverable

**Guided interface**

Provide Guided, Diagnostics, Live Data, Configuration, Programming, Service Functions and Repair Guide navigation. Guided steps cover environment selection, driver health, interface connection, vehicle/module identification and decoder prerequisites. Show exact blockers and measured connection state; do not manufacture vehicle information.

**Local host**

- Bind exclusively to `127.0.0.1` on an allocated port and serve UI, REST and SignalR from that single origin.
- Generate a cryptographically random per-launch session token. Deliver it through the launch URL fragment, remove the fragment immediately and exchange it for an HttpOnly, SameSite-strict session cookie.
- Validate Host and any supplied Origin against the exact launch origin. Require matching Origin for mutation requests and SignalR connections; require authentication for all operational endpoints. Authenticated same-origin downloads may omit Origin.
- Disable cross-origin access, reject wildcard/network binding overrides and exclude credentials from URLs, logs and caches after bootstrap.
- Use REST for commands/downloads and SignalR for progress/live values. Return structured errors with capability, reason and missing prerequisites.
- Model vehicle mutations as immutable reviewed summaries followed by explicit user execution. Bind approval to vehicle/module identity and exact inputs; reject stale approvals and record results, failures and interruptions.

**TOPDON worker isolation**

- Inspect both Windows registry views for J2534 registrations. Report registered path, existence, PE architecture, file version and publisher information.
- Treat the reported missing library as unverified until inspected on Windows. Document official installer repair/reinstallation; do not point registry entries at guessed DLLs.
- Select separately published x86/x64 workers from the DLL’s PE architecture. Only workers load native libraries.
- Use versioned, length-delimited named-pipe messages with request IDs, timeouts, bounded payloads and current-user access restrictions.
- Implement discovery, driver version checks, open/close and supported connection-status queries. Distinguish library discovery, interface availability and confirmed vehicle communication.
- Supervise worker exits and hangs. Native failures terminate affected jobs without crashing the gateway; reconnection is explicit and never silently repeats vehicle writes.
- Give one scheduler ownership of communication. Exclusive decoding/programming jobs suspend conflicting polling. Persist interrupted state across gateway restarts.

## Ordered capability gates

1. **Foundation:** Deliver the initial implementation above, automated tests and Windows installation/driver diagnostics instructions.
2. **Real identification:** Validate TOPDON on Windows, identify the actual VIN, CEM, ECM and other responding modules using established procedures, and produce the initial compatibility record. Missing bus/addressing definitions block those identification operations.
3. **Diagnostics and recording:** Import licensed definitions; implement scans, supported DTC details, supported clearing granularity and confirmation reports. Add parameter discovery, scaling, units, gauges, tables, graphs, favourites, thresholds, timestamped recordings, replay and CSV.
4. **Own CEM decoder:** Audit public research and licences against the identified CEM. Implement only an established applicable algorithm. Bind checkpoints to vehicle/module identity and algorithm version. Accept recovery only after independent verification across at least three distinct challenges and a fresh connection. Store the reusable PIN with DPAPI and support explicit display.
5. **Configuration and service:** Require original backups and demonstrated restoration before enabling edits. Implement comparison, prerequisites, verified writes, supported retrofits and individually documented service routines.
6. **Programming and replacement:** Establish licensed firmware access and exact module procedures first. Validate package provenance and identity matching, bootloader/block sequencing, checksums, post-programming routines and verification. Test interruption handling on suitable bench/spare hardware. Enable only validated recovery and replacement workflows; never assume resumability.
7. **Repair and remaining security:** Import licensed VIN-relevant repair material locally. Implement code/symptom/system search and each individually documented key, mileage or other security operation. Provider interfaces alone do not satisfy acceptance.
8. **Assistance and release:** Add support-case history, previewable redacted bundles and explicit handoff to an existing provider. Package a Windows installer and validate clean-machine installation, troubleshooting and the final capability matrix.

Maintain separate implementation and hardware-validation queues throughout. Frontend, storage, import validation and replay work may continue while a vehicle-specific gate is blocked; dependent vehicle operations remain disabled.

## Verification and completion

- Test missing DLLs, architecture mismatch, worker crashes/hangs, malformed responses, timeouts, USB removal and gateway restarts.
- Test loopback binding, Host/Origin rejection, missing/expired session credentials, SignalR authentication, secret redaction and Windows DPAPI retrieval.
- Compare real DTC interpretation and parameter scaling with licensed definitions and a trusted reference tool on the same vehicle.
- Verify recordings preserve timestamps, units and gaps, with matching replay and CSV output.
- Test scheduler exclusivity, stale-operation review rejection and interruption persistence.
- Require separate recorded evidence for decoder verification, configuration restoration, each service routine, programming/recovery and replacement/security operations.
- Test responsive layouts, production asset serving, simulation isolation and Windows clean-machine installation.

Assume personal use, V40-first support and Windows hardware execution. No licence purchases or subscriptions are authorised. Exact TOPDON model, installed driver, vehicle module identities, licensed packs, decoder applicability and firmware procedures enter the hardware queue as unresolved dependencies.

Declare completion only when every baseline feature has a tested V40 implementation or evidence-backed non-applicability caused by the vehicle’s equipment. Blocked or unfinished features prevent a full-parity claim.
