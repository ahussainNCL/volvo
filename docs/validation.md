# Executed foundation validation

Validated on Linux x64 in this managed cloud instance on **2026-10-29**, with Node **24.19.0**, .NET SDK **10.0.403**, runtime **10.0.14**, and system Chromium. This is software-foundation evidence only. No actual vehicle/driver/decoder/programming behavior has been validated.

| Check | Executed outcome | Scope |
| --- | --- | --- |
| `bash scripts/setup.sh` | Passed, including repeat execution | Frozen NuGet/npm restoration, generated contracts, TypeScript/Vite, full solution build, production asset publication |
| .NET solution build | Passed, zero warnings / errors | All eleven projects, warnings as errors |
| `dotnet test tests/Nordlink.Tests` | **53 passed, 1 skipped, 0 failed** | 54 discovered test cases; skipped case is actual Windows DPAPI CurrentUser round-trip |
| Generated TypeScript contract comparison | Passed | Source-of-truth C# enums, records, nullability/dictionaries match saved generated TS |
| TypeScript + Vite production build | Passed | UI compiled and served inside the gateway; no public Vite listener |
| `python3 scripts/check-host.py` | **52 checks passed** | Real published gateway via actual start.sh, bootstrap, cookie/Host/Origin controls, SignalR auth, production assets, environment isolation, operation lifecycle, CSV/hash checks and restart interruption |
| `npm run test:e2e --prefix frontend` | **1 browser workflow passed** | Real Chromium, Guided environment/connection flow, review/execute, SignalR progress/live samples, recording graph/table/gauge, replay/CSV, responsive 390px/1440px and environment history isolation |
| Windows cross-publication | Passed | Self-contained win-x86/win-x64 worker builds and win-x64 gateway; actual PE machine values checked; source dependency locks remain neutral |
| `npm audit` and production-only audit | 0 reported vulnerabilities | Installed locked dependency versions, at validation time |
| NuGet audit during locked restoration | Passed without warnings | Vulnerable initial SQLite transitive version was replaced; no audit suppression |

`NORDLINK_CHROMIUM=/usr/bin/chromium bash scripts/validate.sh` is the combined software check. Results belong to this run: ignored `test-results/foundation.trx`, `test-results/browser.json` and desktop/mobile screenshots. CI configuration was added but has **not** been run on GitHub; Windows CI results are not claimed.

## What the tests establish

- Bounded current-user pipe framing, UUID/version correlation, malformed/oversized/truncated responses, worker process crash/hang/timeout isolation and explicit failure without automatic writes.
- Synthetic x86/x64 PE inspection and mismatch rejection; missing DLL/non-PE handling. This does not establish the actual TOPDON registration/path or official native ABI behavior.
- One scheduler serializes exclusive work and suppresses polling; cancellation releases ownership. A second gateway cannot create another scheduler for the same data root.
- Operation review copies exact inputs and binds the saved summary/identity/connection/expiry. Replay, stale connection and changed/expired review are rejected. Reviewed/running jobs are persisted as interrupted across restart.
- Hardware/simulation database and artifact roots are separate; cross-environment evidence saves are rejected. UI filters contexts and clears simulation history, samples and selections on environment changes.
- Session bootstrap is single use and expires; forged/missing/expired/restarted credentials fail. Host, foreign/opaque/missing mutation Origin, CORS preflight and network-wide binding overrides fail. Authenticated same-origin CSV downloads may omit Origin.
- Routine SecurityCredential serialization contains metadata, not PIN material. Explicit reveal requires authenticated same-origin POST and no-store; actual decryption is unrun on Linux.
- Local artifact intake checks SHA-256, nonempty source/licence/applicability and environment. It does not validate a proprietary package schema or prove a licence.
- Recordings round-trip timestamps/units/values/gaps. Invalid ordering/unmarked gaps are rejected. CSV neutralizes formula-like text, and real browser replay/download preserves fixture gaps.

## Unrun or blocked acceptance

Windows registry discovery and official installer repair, actual TOPDON open/version/close, real architecture selection/USB removal, DPAPI current-user retrieval, Windows executable/PowerShell/clean-machine installer validation, actual VIN/CEM/ECM identification and supported voltage queries are **unrun**.

Real scans/clears, DTC interpretation, parameter scaling, reusable CEM PIN recovery/independent verification, configuration restoration/edits, retrofits, service routines, firmware programming/recovery, replacement/adaptation, key/mileage/security procedures, licensed repair search and provider handoff remain **blocked/unimplemented** as recorded in the register. There are no equipment-non-applicability findings and no hardware-verified feature claims.

The reference website returned proxy CONNECT 403. The editable environment draft and running instance have different network settings; the observed running policy remains restricted. No website content or advertised implementation details were inferred. Review/apply the required destination access in environment settings before a later website claim audit.

## Online service extension validation

After the user selected online website + Windows helper, the updated eleven-project solution and frozen setup passed. The full .NET suite now has **53 passed, 1 Windows DPAPI skipped, 0 failed**. Both the original local Chromium workflow and the separate cloud Chromium workflow passed. The existing loopback host retained **52 passing HTTP/lifecycle/isolation checks**.

The cloud browser workflow starts the actual cloud executable plus a separate outbound helper process and local gateway. It verifies missing-auth/foreign-Origin rejection, sign-in, one-use pairing, browser-session isolation, blocked remote PIN reveal, simulation connection, reviewed operation, forwarded SignalR progress/live values, recording/CSV download, unpair and sign-out. This validates the architecture in Linux development mode; it does not validate actual Windows/TOPDON operations.

The Heroku-target Dockerfile built successfully with TLS verification preserved. The production image ran as a non-root user and passed **15 container checks**, including production UI/feature-file access, exact Host, secure cookie flags, sign-in/out, pairing, blocked credential reveal and explicit missing-helper state. A read-permission problem for the capability manifest was fixed in the image and the affected checks reran successfully. The image does not include local vehicle data or DPAPI material. Public Heroku deployment/router/TLS behavior is not yet validated because the user has not created an app and this workspace has no Heroku authentication binding.

The Windows x64 helper also cross-published successfully and its PE architecture was verified, alongside the existing x86/x64 worker and x64 gateway outputs. Executing the helper, installer/PowerShell launcher, DPAPI and real hardware remains unrun. No new hardware acceptance evidence or non-applicability findings were added. Public VCTS pages are now accessible; their advertised claims are recorded in reference-audit.md and remain distinct from measured V40 behavior.
