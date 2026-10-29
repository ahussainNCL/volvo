# Local API

All endpoints are on the gateway's dynamically allocated exact loopback origin. JSON uses generated C# contracts with camelCase properties/enums; missing required or null non-nullable constructor fields are rejected. All API endpoints except the session exchange and all SignalR requests require the private cookie. Mutation/SignalR requests also require exact Origin. No raw PIN in logs, ordinary responses or events.

| Endpoint | Behavior |
| --- | --- |
| `POST /api/session` | One-time `BootstrapRequest` fragment-token exchange; five-minute validity; HttpOnly/SameSite-strict cookie |
| `GET /api/status` | Measured ConnectionSnapshot; unknown VIN/module stays null; actual interface versions carry provenance |
| `GET /api/features` | Full operation register with implementation and hardware acceptance states |
| `POST /api/environment` | Hardware/simulation; requires disconnected state |
| `POST /api/drivers/inspect` | Both Windows registry views through an isolated worker; Linux reports Windows prerequisite |
| `POST /api/connect`, `POST /api/disconnect` | Explicit interface lifecycle; bus/vehicle communication not fabricated |
| `GET /api/vehicles` | Environment-scoped saved VehicleProfile results |
| `POST /api/operations/review` | Simulation-only fixture operation; immutable identity/input/summary/expiry-bound review |
| `POST /api/operations/{id}/execute` | Explicit single-use ExecuteRequest after review; persists completion/interruption |
| `GET /api/operations` | Environment-scoped OperationJob history; no approval tokens |
| `POST /api/live/simulation/start`, `/stop` | Explicit fixture SignalR feed; stops when connection/environment changes |
| `POST /api/recordings/simulation-fixture` | Explicit timestamped fixture with two gaps, not hardware capture |
| `GET /api/recordings`, `/{id}`, `/{id}/csv` | Persisted Recording, replay values and formula-safe CSV; authenticated downloads may omit Origin |
| `POST /api/artifacts`, `GET /api/artifacts` | ArtifactImport validation/provenance and metadata list; max 4 MiB; no semantic pack enablement |
| `GET /api/credentials` | SecurityCredential metadata only |
| `POST /api/credentials/{id}/reveal` | Explicit authenticated same-origin non-cacheable raw reveal; Windows DPAPI only; zero temporary buffer |
| `POST /api/capabilities/{id}/run` | Structured ApiError with exact missing real-procedure dependencies; no hardware command dispatched |
| `/hubs/progress` | Cookie-authenticated exact-Origin SignalR WebSocket; `progress: ProgressEvent`, `live: LiveValueEvent`; both carry evidence context |

Structured errors carry capability, reason and missingPrerequisites. 400 means invalid input, 401 missing/expired session, 403 Host/Origin rejected, 404 missing local record/route, 409 missing operation prerequisites or stale approval, 503 storage/worker dependency failure. No retry automatically replays a vehicle operation.

The frontend uses native WebSockets with cookies, no bearer query-string credentials. Browsers send the required WebSocket Origin. Arbitrary hub connections without Origin fail even if a cookie is present. Only the harmless bootstrap/static shell is available before authentication; it exposes no operational result.
