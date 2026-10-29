param([Parameter(Mandatory=$true)][string]$CloudOrigin)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../artifacts/windows')).Path
$helper = Join-Path $root 'helper/Nordlink.Helper.exe'
if (-not (Test-Path $helper)) { throw 'Run scripts/publish-windows.ps1 first.' }
# The one-use code is read privately by the executable, never placed in process arguments.
& $helper --cloud $CloudOrigin --gateway-root $root
if ($LASTEXITCODE -ne 0) { throw 'Helper stopped. Inspect prerequisites and create a fresh pairing code.' }
