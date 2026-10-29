# Architecture and decisions

## ADR-001: one exact loopback origin

ASP.NET Core binds explicitly to `IPAddress.Loopback` and port 0 by default. Only `--port`, `--data-root`, `--worker-root` and `--launch-file` are accepted. URL/port and Kestrel environment binding overrides are rejected. Configuration sources are cleared so appsettings/environment cannot add listeners. UI assets, REST and SignalR use the same origin. The host never starts Vite.

A 256-bit per-launch token is delivered only through the private launch URL fragment. Frontend startup erases the fragment before network requests. An exact-Origin POST exchanges it once within five minutes for a separate 256-bit HttpOnly, SameSite=Strict cookie. Cookies expire after eight hours and are invalidated by process restart. HTTP is intentional on exact loopback; `Secure` cookies would not work across ordinary HTTP clients, so cookie scope and Host/Origin validation are mandatory. A same-user local process or compromised OS is outside this browser-origin boundary.

Host must equal the actual allocated `127.0.0.1:port`. Any supplied Origin must match. Mutations and every SignalR request require matching Origin; ordinary authenticated downloads may omit it. OPTIONS, foreign origins, opaque origins and cross-site fetches are rejected. No CORS middleware or bearer tokens in query strings. Every API except the bootstrap exchange and every hub endpoint require the session cookie. Unknown API paths never return SPA HTML. Responses have no-store, no-referrer, nosniff and a self-only CSP. Request/body logging and native worker output logging are disabled. Credentials never appear in routine DTOs/events/exports. A launch file is an ephemeral credential, deleted after exchange or graceful exit; on forced termination its expired token may remain until next launch overwrites it.

## ADR-002: workers contain native failures

Only the hardware-worker executable calls `NativeLibrary.Load`. It inspects `HKLM\SOFTWARE\PassThruSupport.04.04` through both registry views, filters TOPDON registrations, checks FunctionLibrary existence, reads PE machine type and version/publisher metadata, and reports failures without guessing replacement paths. Publisher metadata is descriptive, not a successful signature-trust assertion. J2534 05.00 registrations and Windows ARM64 are not established by this foundation.

The gateway starts a discovery worker and then a separately published x86/x64 worker selected from inspected architecture. Windows x64 is the gateway target. Each worker has a random current-user-only named pipe. Messages use little-endian four-byte length framing, version 1, UUID request IDs, strict JSON contracts and a 1 MiB bound. Calls have an eight-second deadline. Unexpected exits, hangs, wrong IDs/versions, fabricated vehicle confirmation and malformed/truncated/oversized responses close the pipe and kill the worker. Drained native stdout/stderr is discarded. There is no automatic hardware reconnection or write retry.

The worker supports discovery, PassThruOpen, PassThruReadVersion and PassThruClose. It does **not** claim bus connection, voltage or vehicle identity. Standard/extension status calls must be established for the exact driver before adding them. An open device is `interfaceOpen`; only real measured communication may produce a hardware `vehicleConfirmed` state.

## ADR-003: one communication scheduler

An exclusive per-data-directory process lock prevents a second gateway from creating another scheduler or recovering live jobs. One semaphore owns all communication, connection changes and operation approvals. Pending exclusive jobs suppress polling. Current foundation calls are serialized; no undocumented native cancellation is attempted. Native timeout kills the affected worker. Simulation streaming checks environment and connection revision under scheduler ownership and stops on state changes. Gateway restart marks reviewed/running jobs interrupted in both databases and invalidates in-memory approvals. Reconnection and resubmission require user action.

Reviews copy exact inputs, vehicle/module identity, environment, connection revision, summary and expiry. The saved reviewed object is hashed; approval is bound to that digest plus a one-use random token. Execution checks the digest, identity, revision and expiry before persisting Running. Completion/failure/interruption is recorded. The only executable foundation operation is a non-sensitive simulation note; this is not a vehicle mutation implementation.

## ADR-004: separate evidence and credential material

SQLite is local, with entirely different hardware/simulation databases and artifact directories. Persisted vehicle/job/recording/artifact results carry evidence context (environment, vehicle/module identity when known, source, timestamp and evidence identifier). Unknown identity stays null. Audit rows contain timestamp, environment, action and record identifier; never raw operation payloads or secrets.

All ten required shared types are C# records. The reflection generator emits TypeScript interfaces, enums, nullability and dictionaries; validation compares generated bytes. SecurityCredential contains metadata only. DPAPI CurrentUser encrypts material in separate `.dpapi` files; Linux refuses it. Explicit authenticated same-origin POST reveal writes plaintext with no-store and zeroes the temporary buffer. There is no generic credential upload API and no decoder-generated credential yet. Metadata labels must be non-sensitive. Backup/export tooling must exclude credential material and session files.

Local artifact intake accepts up to 4 MiB with source, licence, module applicability and expected SHA-256; content is addressed by hash. Reimport validates existing bytes. Hash validation provides integrity, not permission, vendor authenticity, format validation or firmware applicability. No imported blob is executed, flashed, automatically unpacked or used as diagnostic definitions. Typed provider adapters need licensed format implementations at later gates.

## ADR-005: gates and truthful capability states

The feature register contains the 13 user-supplied categories and explicit operations. `unknown`, `unsupported`, `blocked`, `implementedUnverified`, `hardwareVerified` and `equipmentNonApplicable` are distinct wire states. Missing licences/algorithms/procedures are blockers, not evidence of absent vehicle equipment. Hardware evidence arrays start empty. Generic keys/mileage/recovery/replacement catalogue entries remain research with `exactOperation: null` until decomposed into sourced procedures. No provider interface or simulation fixture satisfies a real vehicle acceptance test.

The implementation queue and Windows hardware queue are independent. The initial compatibility record records only the target model/year and unknown VIN/module variants. Current website feature claims are uninspected because the managed proxy denies the destination; the user's supplied matrix is the baseline. No purchase/subscription is authorized.
