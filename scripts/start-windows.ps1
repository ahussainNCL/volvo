$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot '../artifacts/windows/Nordlink.Gateway.exe'
if (-not (Test-Path $app)) { throw 'Run scripts/publish-windows.ps1 first.' }
$data = Join-Path $env:LOCALAPPDATA 'Nordlink'
$launch = Join-Path $data ('launch-' + [Guid]::NewGuid().ToString('N') + '.json')
$process = Start-Process -FilePath $app -ArgumentList @('--launch-file', ('"' + $launch + '"')) -PassThru
try {
  $deadline = (Get-Date).AddSeconds(30)
  while (-not (Test-Path $launch)) {
    if ($process.HasExited -or (Get-Date) -gt $deadline) { throw 'Gateway failed to start; inspect local prerequisites.' }
    Start-Sleep -Milliseconds 100
  }
  # The fragment is used once; the browser removes it immediately after opening.
  $url = (Get-Content $launch -Raw | ConvertFrom-Json).url
  Start-Process $url
  $process.WaitForExit()
} finally {
  if (-not $process.HasExited) { Stop-Process -Id $process.Id }
  if (Test-Path $launch) { Remove-Item -LiteralPath $launch }
}
