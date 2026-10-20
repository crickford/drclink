# Requires Windows PowerShell 5.1 or PowerShell 7 on Windows, as Administrator.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProbeRoot,
    [ValidateRange(30, 180)][int]$Seconds = 90
)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This collector requires Windows.'
}
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open PowerShell as Administrator and run this command again.'
}
$ProbeRoot = (Resolve-Path -LiteralPath $ProbeRoot).ProviderPath
$dotnet = Join-Path $ProbeRoot 'runtime\dotnet.exe'
$probe = Join-Path $ProbeRoot 'app\DrcLink.Probe.dll'
foreach ($file in @($dotnet, $probe)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing probe file: $file" }
}
$run = Join-Path $PSScriptRoot ('results\boundary-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$etl = Join-Path $run 'boundary.etl'
$summary = [ordered]@{
    collectorVersion = '0.1.0'
    startedUtc = [DateTime]::UtcNow.ToString('o')
    traceStarted = $false
    traceStartExitCode = $null
    traceStopExitCode = $null
    probeExitCode = $null
    traceBytes = 0
    failure = $null
    scope = 'Observation of a normal protected GO and phone association; no GamePad pairing or control modification.'
}
$owned = $false
$fatalError = $null
try {
    Get-CimInstance Win32_OperatingSystem |
        Select-Object Caption, Version, BuildNumber |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'windows.json') -Encoding UTF8
    Get-CimInstance Win32_PnPSignedDriver |
        Where-Object { $_.DeviceClass -eq 'NET' } |
        Select-Object DeviceName, DriverVersion, DriverDate, InfName, DriverProviderName, IsSigned |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'network-drivers.json') -Encoding UTF8
    & netsh.exe wlan show drivers 2>&1 | Out-File -LiteralPath (Join-Path $run 'wlan-drivers.txt') -Encoding utf8
    & netsh.exe trace show scenario name=wireless_dbg 2>&1 | Out-File -LiteralPath (Join-Path $run 'wireless-dbg-scenario.txt') -Encoding utf8

    # Record the exact CodeView PDB identity, without copying the Windows binary.
    try {
        if (-not ('DrcLink.SymbolIdentity' -as [type])) {
            Add-Type -Path (Join-Path $PSScriptRoot 'SymbolIdentity.cs')
        }
        $wdi = Join-Path $env:SystemRoot 'System32\drivers\wdiwifi.sys'
        if (Test-Path -LiteralPath $wdi) {
            $v = [Diagnostics.FileVersionInfo]::GetVersionInfo($wdi)
            [ordered]@{
                fileVersion = $v.FileVersion
                sha256 = (Get-FileHash -LiteralPath $wdi -Algorithm SHA256).Hash
                codeView = @([DrcLink.SymbolIdentity]::Read($wdi))
            } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'wdi-symbol-identity.json') -Encoding UTF8
        }
    }
    catch {
        $_.Exception.Message | Set-Content -LiteralPath (Join-Path $run 'wdi-symbol-error.txt') -Encoding UTF8
        Write-Warning 'Could not record symbol identity; continuing with trace collection.'
    }

    Write-Host 'Turn Mobile Hotspot OFF and leave Wi-Fi ON.'
    Write-Host 'Have your phone ready to join DrcLink-Probe using the password the probe prints.'
    $null = Read-Host 'Press Enter to start recording and launch the protected probe'

    # Both GUIDs and the WDI flags are from Microsoft driver tracing documentation.
    # WDI WPP supplies the driver-side trace; NDIS supplies request-routing context.
    # Do not stop a pre-existing netsh trace: ownership is set only after start succeeds.
    $traceArgs = @(
        'trace', 'start', 'wireless_dbg',
        'provider={21ba7b61-05f8-41f1-9048-c09493dcfe38}', 'level=0xff', 'keywords=0xff',
        'provider={dd7a21e6-a651-46d4-b7c2-66543067b869}', 'level=5', 'keywords=0xffffffffffffffff',
        'capture=no', 'report=no', 'persistent=no', 'maxsize=128', 'filemode=circular',
        "tracefile=$etl"
    )
    & netsh.exe @traceArgs 2>&1 | Tee-Object -FilePath (Join-Path $run 'trace-start.txt') | Out-Host
    $summary.traceStartExitCode = $LASTEXITCODE
    if ($LASTEXITCODE -ne 0) {
        throw 'Trace did not start. See trace-start.txt. An existing trace is left running.'
    }
    $owned = $true
    $summary.traceStarted = $true
    Write-Host "Join DrcLink-Probe from your phone during the next $Seconds seconds. The GamePad is not needed."
    & $dotnet $probe probe --mode protected --seconds $Seconds --out (Join-Path $run 'probe')
    $summary.probeExitCode = $LASTEXITCODE
}
catch {
    $fatalError = $_
    $summary.failure = $_.Exception.Message
}
finally {
    if ($owned) {
        try {
            & netsh.exe trace stop 2>&1 | Tee-Object -FilePath (Join-Path $run 'trace-stop.txt') | Out-Host
            $summary.traceStopExitCode = $LASTEXITCODE
        }
        catch {
            $summary.traceStopExitCode = -1
            if (-not $summary.failure) { $summary.failure = $_.Exception.Message }
        }
        if ($summary.traceStopExitCode -ne 0) {
            Write-Warning 'Trace stop failed. Run netsh trace stop in Administrator PowerShell.'
        }
    }
    if (Test-Path -LiteralPath $etl) { $summary.traceBytes = (Get-Item -LiteralPath $etl).Length }
    $summary.endedUtc = [DateTime]::UtcNow.ToString('o')
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'collector-summary.json') -Encoding UTF8
    Write-Host "Results: $run"
}
if ($fatalError) { throw $fatalError }
if ($summary.traceStopExitCode -ne 0) { throw 'The trace did not stop cleanly; see results.' }
Write-Host 'Recording finished. A nonempty ETL is not yet proof that the required events were captured or decoded.'
Write-Host 'Share collector-summary.json, wdi-symbol-identity.json, and probe\summary.json first.'
Write-Host 'Keep boundary.etl for decoding. WPP text requires matching PDB/TMF data; plain ETL-to-text conversion is insufficient.'
