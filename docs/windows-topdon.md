# Windows installation and TOPDON diagnostics

Linux tests do not validate TOPDON, DPAPI or Windows publishing. Use a Windows x64 PC as the local gateway host. Keep the gateway exclusively on 127.0.0.1. No remote browser, port forwarding or wildcard binding.

1. Install the pinned .NET 10 SDK (global.json) and Node 24 (.nvmrc) from official Microsoft/Node installers. Preserve signature/TLS verification. Run `dotnet --version` and `node --version`. No licence purchase is needed for this foundation.
2. Obtain the installer for the **exact TOPDON model** from TOPDON's official support channel. Record model, installer version and signer. Follow the vendor's USB/power/install order. Do not use an unrelated model's DLL or unofficial download.
3. In PowerShell from the checkout run `scripts/publish-windows.ps1` (internet/package access required), then `scripts/start-windows.ps1`. The portable layout has gateway plus `workers/win-x86` and `workers/win-x64`. The launcher opens the per-launch private URL. Windows clean-machine publishing remains a required validation step; no installer is claimed.
4. Guided → Hardware → Inspect driver health. The discovery worker inspects both Registry32/Registry64 views of `HKLM\SOFTWARE\PassThruSupport.04.04`, including Name, Vendor and FunctionLibrary. It reports the **registered** absolute path, existence, PE machine, version and publisher metadata. Missing path/version/publisher remain unknown. Check signature trust separately; a publisher string alone does not establish trust.
5. Inspect, without changing, the registered path. Independent checks in PowerShell:

   ```powershell
   # Replace only with the actual inspected FunctionLibrary path.
   $driverPath = Read-Host 'Actual registered FunctionLibrary path'
   Test-Path -LiteralPath $driverPath
   if (Test-Path -LiteralPath $driverPath) {
     Get-Item -LiteralPath $driverPath | Select-Object FullName, Length, VersionInfo
     Get-AuthenticodeSignature -LiteralPath $driverPath | Select-Object Status, StatusMessage, SignerCertificate
     Get-FileHash -LiteralPath $driverPath -Algorithm SHA256
   }
   ```

6. If the actual registered file is missing, close Nordlink and run the **official matching installer** repair, or uninstall/reinstall following TOPDON instructions. Reboot only if the installer requires it. Reinspect both views. Do not point the registry at a guessed library or manually copy DLLs between versions. The user's missing-library report remains unverified until this inspection.
7. Choose an existing healthy registration. Nordlink selects the separately published worker matching PE architecture. Only that process loads the native DLL, with DLL-local/System32 dependency resolution. A mismatch or missing worker is a blocker. Open interface reads official API/driver/firmware versions. PassThruOpen success proves interface availability only. Driver status/voltage support is not assumed.
8. Disconnect, unplug USB, reconnect and record behavior. Unexpected worker exit/hang faults the connection and terminates affected work; disconnect/reconnect is explicit. Do not retry a vehicle write automatically. Check permissions, USB/power and official installer before another attempt.
9. Real VIN/module identification remains disabled until established applicable bus/addressing/session definitions are available. Do not probe arbitrary protocol IDs or addresses. Proceed to the hardware queue once that prerequisite is met.

Run `dotnet test tests/Nordlink.Tests` on Windows to exercise DPAPI CurrentUser. Encrypted material stays local under `%LOCALAPPDATA%\Nordlink\hardware\credentials`. Keep default user-only profile permissions. Backups/support bundles must exclude these files and launch*.json. Explicit reveal is a POST requiring the local session/Origin and no-store; no routine event/export contains PIN material. No real recovered credential exists until gate 4.
