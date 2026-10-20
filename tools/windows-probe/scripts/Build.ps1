[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\DrcLink.Probe\DrcLink.Probe.csproj'
$destination = Join-Path (Split-Path -Parent $PSScriptRoot) 'rebuilt'
& dotnet publish $project -c Release -r win-x64 --self-contained true -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built executable: $destination\DrcLink.Probe.exe"
