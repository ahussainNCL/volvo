# Nordlink online website + Windows helper

The online service gives your browser access to the existing workspace through an explicitly paired helper PC. It is a separate ASP.NET Core executable; the existing gateway still binds only to 127.0.0.1. Real vehicle functions remain subject to the existing hardware gates. This is a personal, single-owner service, not a production multi-tenant workshop SaaS.

```text
Browser — HTTPS + private session cookie — Nordlink.Cloud on Heroku
                                              |
                              authenticated outbound WSS connection
                                              |
                           Nordlink.Helper on your Windows PC
                                              |
                          authenticated exact-origin loopback HTTP/SignalR
                                              |
                      Nordlink.Gateway → x86/x64 worker → official TOPDON
```

The Windows helper opens an outbound connection. Do not forward your PC's ports, expose its local gateway, place drivers on Heroku, or send your CEM PIN to the website. SQLite, imported files and DPAPI credential material remain on the helper PC; permitted commands/results transit the cloud service to your browser. The host can therefore observe permitted diagnostic data, even though it does not persist it.

## Hosting contract

The cloud service listens on Heroku's `PORT`. It requires these configuration values at runtime:

| Name | Value to configure securely |
| --- | --- |
| `NORDLINK_PUBLIC_ORIGIN` | Exact public HTTPS origin, e.g. `https://YOUR-APP-UNIQUE-ID.herokuapp.com`, with no trailing slash/path. Copy the actual Heroku app URL, not an assumed hostname |
| `NORDLINK_ADMIN_PASSWORD` | A unique random workspace password of at least 20 characters; enter in Heroku Settings → Config Vars, never in Git or chat |

Cloud sign-in uses rate-limited password verification, a secure HttpOnly/SameSite-strict eight-hour cookie, exact Host/Origin checking and no-store/CSP headers. Pairing codes are 64-bit random, single-use and expire after five minutes. Helper credentials are separate 256-bit random values, expire with their browser session, remain in memory and are sent only in an Authorization header over WSS. A browser may operate only its own paired helper. Logout/unpair terminates that binding. Re-pairing invalidates prior codes/tokens. Old connection cleanup cannot disconnect a newly paired helper.

Run **one web dyno / one cloud process**. Sessions, pairing and relay state are ephemeral and are invalidated on dyno restart/deploy. Re-sign in and re-pair explicitly. No durable cloud database or paid database add-on is required. Multiple dynos, worker processes, durable accounts, automatic reconnect and multi-tenant scaling are not implemented. Browser/hub traffic and helper connections must reach this same process.

The cloud/helper share a fixed route allowlist. Remote credential metadata/reveal, artifact upload, arbitrary URLs/paths and arbitrary capability execution are blocked at both ends. Unknown commands are rejected. The only executable reviewed operation remains the labelled simulation fixture. Hardware open/inspection is forwarded only to the Windows gateway; there are no new vehicle write procedures. Requests time out without automatic replay; an interrupted command may have an unknown outcome and must be checked in local job history.

## Prepare and test locally

```bash
cd /workspace/volvo
source scripts/env.sh
bash scripts/build-cloud.sh
NORDLINK_CHROMIUM=/usr/bin/chromium bash scripts/validate-cloud.sh
```

The cloud browser test generates its own private password/code, starts the actual cloud executable and a separate Linux development helper/gateway, and exercises authentication, pairing, session isolation, reviewed fixture operation, SignalR progress/live data, recording/CSV and unpairing. It does not validate Windows J2534. Plain HTTP development requires the explicit `--development-loopback` switch and an exact 127.0.0.1 origin; normal operation requires HTTPS/WSS.

`Dockerfile` and `heroku.yml` build only the cloud executable and its React production UI. `.dockerignore` excludes local databases, private launch files, credential material, dependency outputs and logs. The image runs as the supplied non-root .NET user. In this managed proxy environment the optional BuildKit `proxy_ca` and `ca_bundle` secrets provide trust for package installation; they are not copied into the image. Ordinary builds use normal system trust. Keep TLS and package verification enabled.

## Deploy to your Heroku account

No app has been created or hosting purchased by the agent. There is no Heroku login/API binding in this workspace. Create an app in your own Heroku dashboard when ready and choose its hosting plan yourself. Do not enter passwords/API keys in chat.

1. Create the app, note its exact **app name** and full public URL, and configure the two runtime values above securely in its settings.
2. Install the official Heroku CLI and Docker Desktop on your computer and sign in locally. From the repository, use Heroku's container workflow:

   ```powershell
   heroku login
   heroku container:login
   heroku container:push web --app YOUR-APP-NAME
   heroku container:release web --app YOUR-APP-NAME
   heroku open --app YOUR-APP-NAME
   ```

   BuildKit support is required by the Dockerfile. A web dyno must be provisioned/running under the plan you select. Do not scale above one dyno. Public deployment and Heroku routing must be checked after release; a local image/test does not prove remote deployment.
3. On the Windows PC with TOPDON attached, clone/update this repository, install the pinned Node/.NET SDK and official matching TOPDON driver, then run:

   ```powershell
   .\scripts\publish-windows.ps1
   .\scripts\start-helper-windows.ps1 -CloudOrigin https://YOUR-EXACT-PUBLIC-HOST
   ```

4. Sign in on your website, choose **Create pairing code**, and type that one-use code into the helper's private terminal prompt. The online workspace appears after pairing. Close any standalone Nordlink local gateway before starting the helper: both use the same protected data directory and one scheduler.
5. Use **Guided → Hardware → Inspect driver health**. First validate the exact official TOPDON installation on Windows. Real VIN/CEM/ECM, bus/addressing definitions and all hardware feature acceptance remain unresolved. You can explicitly select Simulation to verify the cloud connection; that does not validate your vehicle.

The Windows bundle includes the existing gateway/workers and `helper/Nordlink.Helper.exe`. Cross-publication is not Windows execution or a signed installer. `publish-windows.ps1` and Windows DPAPI/driver execution require Windows validation.
