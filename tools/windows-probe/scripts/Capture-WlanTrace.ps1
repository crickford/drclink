# Optional event trace. Requires an Administrator terminal. No packet capture.
[CmdletBinding()]
param([ValidateRange(10, 300)][int]$Seconds = 60)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open an Administrator PowerShell terminal to collect the optional ETW trace.'
}
$root = Join-Path (Split-Path -Parent $PSScriptRoot) 'logs'
$run = Join-Path $root ('trace-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$etl = Join-Path $run 'wlan.etl'
$owned = $false
try {
    # If another netsh trace is already active, start fails. Do not stop that session.
    & netsh.exe trace start scenario=WLAN capture=no report=no persistent=no maxsize=128 filemode=circular "tracefile=$etl"
    if ($LASTEXITCODE -ne 0) { throw 'Trace did not start. An existing trace, unsupported scenario, or permission issue may be responsible.' }
    $owned = $true
    Write-Host "Recording Windows WLAN events for $Seconds seconds. Run the probe in a second terminal now."
    Write-Host 'This is not an over-the-air capture and may not expose unsuccessful GamePad requests.'
    for ($i = 0; $i -lt $Seconds; $i++) { Start-Sleep -Seconds 1 }
}
finally {
    if ($owned) {
        & netsh.exe trace stop
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Trace stop failed. Run netsh trace stop in an Administrator terminal.' }
    }
    Write-Host "Trace output: $run"
}
