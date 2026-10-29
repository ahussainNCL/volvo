# Nordlink

A local browser workspace for a **2013 Volvo V40 D2**, with a React/TypeScript/Vite interface, .NET 10 loopback gateway, SQLite storage and isolated TOPDON J2534 workers.

**This is a tested foundation, not a working vehicle diagnostic/programming release.** Hardware identity is unknown. No CEM decoder algorithm, vehicle transport definitions, licensed diagnostic/repair packs, firmware procedures or validated vehicle writes are included. Simulation cannot establish hardware readiness. [The feature register](docs/feature-register.json) tracks all 13 requested categories; [the hardware queue](docs/hardware-validation.md) lists the evidence needed to progress.

## Online website + Windows helper

The cloud service and Windows helper are documented in [cloud.md](docs/cloud.md). Build with `bash scripts/build-cloud.sh`; test with `NORDLINK_CHROMIUM=/usr/bin/chromium bash scripts/validate-cloud.sh`. Heroku deployment files are included. Public deployment requires your own Heroku app and securely configured workspace password; no app or hosting plan has been created here. Vehicle prerequisites still apply. The local workflow below remains available.

## Run on Linux

Use Node **24.19.0** (`.nvmrc`) and .NET SDK **10.0.403** (`global.json`). In this cloud environment the SDK is installed at `/workspace/.tools/dotnet`, checked against Microsoft's published SHA-512. Work in the existing `/workspace/volvo` checkout; a new worktree is unnecessary.

```bash
cd /workspace/volvo
bash scripts/setup.sh
bash scripts/start.sh
```

The gateway allocates an available port exclusively on `127.0.0.1`. It writes the private launch URL to `data/launch.json` (owner-only permissions on Linux); open its `url` locally within five minutes. The browser removes the token fragment before making requests, exchanges it for a session cookie and the gateway deletes the launch file. Relaunch after a lost/expired session. Do not share the launch file or URL, put it in logs, or publish a loopback preview. The cloud onboarding UI does not expose local browser previews; automated local requests/browser tests validate it here.

Select **Simulation → Connect simulation** to exercise connection state, reviewed operation execution, SignalR progress, timestamped fixture values, recording/replay and CSV. These fixtures contain no real VIN. Hardware/simulation SQLite databases and artifacts are under separate `data/hardware` and `data/simulation` roots. Disconnect before switching. `data/` is ignored by Git.

The SDK's first run may need its normal per-user directories initialized outside the workspace under a restricted execution sandbox. Allow that specific .NET initialization through the environment's escalation flow; do not change HOME or bypass package/TLS verification. Package and HTTP caches are redirected by `scripts/env.sh`.

## Validate

```bash
source scripts/env.sh
export NORDLINK_CHROMIUM=/usr/bin/chromium  # use an existing Chromium, or install Playwright's browser
bash scripts/validate.sh
```

Runs .NET unit/IPC tests, generated-contract comparison, TypeScript and production build, real HTTP host security/lifecycle checks, then a real Chromium workflow and responsive check. Results go to ignored `test-results/`. The DPAPI test skips on Linux and runs on Windows. See [validation.md](docs/validation.md) for executed results and unrun hardware tests.

For an ordinary machine without Chromium, run `cd frontend && npx playwright install chromium` with normal TLS trust, then omit `NORDLINK_CHROMIUM`. Nothing needs a publicly bound Vite server: production UI, REST and SignalR share the gateway origin.

## Windows and next gate

Install the pinned Node and .NET SDK using official installers. Follow [Windows/TOPDON instructions](docs/windows-topdon.md); run `scripts/publish-windows.ps1` then `scripts/start-windows.ps1` in PowerShell. Publishing produces a portable gateway and separate self-contained x86/x64 workers. Self-contained Windows x64 gateway and x86/x64 worker cross-publishing was verified on Linux. Executing the Windows build, PowerShell launcher, DPAPI, clean-machine installation and real hardware remain unverified here.

The reported missing TOPDON library is an **unverified report**, not a measured Windows result. Inspect both registry views and repair the official installer; never redirect registry entries to a guessed DLL. An interface open/version read is not confirmed vehicle communication. No `PassThruConnect`, vehicle writes or undocumented voltage queries are issued.

## Layout

| Path | Responsibility |
| --- | --- |
| `frontend/` | Guided shell and seven navigation sections; production assets generated into gateway `wwwroot` |
| `src/Nordlink.Contracts/` | Authoritative C# DTOs and bounded/versioned IPC framing |
| `src/Nordlink.Gateway/` | Exact-origin local sessions, REST, SignalR, connection lifecycle and worker supervision |
| `src/Nordlink.HardwareWorker/` | Registry/PE discovery; official native J2534 open/version/close only |
| `src/Nordlink.Vehicle/` | Single communication scheduler and immutable, expiring, single-use operation reviews |
| `src/Nordlink.Storage/` | Separate SQLite stores, audit history, recordings and gap-preserving CSV |
| `src/Nordlink.Providers/` | Local hash/provenance intake, provider interfaces, DPAPI credential store |
| `tools/Nordlink.ContractGenerator/` | Generate `frontend/src/contracts.ts` from C# contracts |
| `tests/` | Foundation tests plus a test-only crashing/hanging/malformed IPC worker |
| `docs/` | Decisions, capability traceability, implementation plan and hardware evidence queue |

No accounts, billing, licence purchases, subscriptions or online support handoffs are enabled.
