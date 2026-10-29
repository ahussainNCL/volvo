$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
function Checked { param([scriptblock]$Command) & $Command; if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" } }
if ((node --version) -ne ('v' + (Get-Content .nvmrc).Trim())) { throw 'Activate the Node version in .nvmrc.' }
Checked { dotnet restore Nordlink.sln --locked-mode --configfile NuGet.Config }
Checked { dotnet run --project tools/Nordlink.ContractGenerator --no-restore -- frontend/src/contracts.ts }
Checked { npm ci --prefix frontend --no-fund }
Checked { npm run build --prefix frontend }
# Platform-specific restore locks remain in generated artifacts, separate from source locks.
foreach ($arch in @('x86','x64')) {
  Checked { dotnet publish src/Nordlink.HardwareWorker -c Release -r "win-$arch" --self-contained true -p:NordlinkPublishRoot="$PWD/artifacts/locks" -o "artifacts/workers/win-$arch" }
}
Checked { dotnet publish src/Nordlink.Gateway -c Release -r win-x64 --self-contained true -p:NordlinkPublishRoot="$PWD/artifacts/locks" -o artifacts/windows }
New-Item -ItemType Directory -Force artifacts/windows/workers | Out-Null
foreach ($arch in @('x86','x64')) { Copy-Item "artifacts/workers/win-$arch" artifacts/windows/workers/ -Recurse -Force }
Write-Host 'Portable Windows build prepared. Installer/clean-machine and real TOPDON validation remain in docs/hardware-validation.md.'
