# Existing tools and validation

## Package layout

`tools/windows-probe` preserves DrcLink.Probe 0.1.1 as shipped, including source, tests, framework-dependent application and a portable Windows x64 .NET 8.0.32 runtime. `tools/windows-driver-boundary` preserves the later collector and PE symbol reader. No source changes were made to either tool while preparing this handoff.

The runtime is bundled to reproduce the historical diagnostic; it is not a statement that .NET 8.0.32 is the newest release or the version to choose for the future application. Preserve the historical behavior when comparing results and record any dependency changes in new work.

## Windows probe

Relevant source files under `tools/windows-probe/src/DrcLink.Probe`:

| File | Role |
| --- | --- |
| `RadioProbe.cs` | WinRT publication, listener/peer lifetime and cleanup |
| `NativeOpenProbe.cs` / `LegacySettingsAbi.cs` | Direct native null-passphrase setter/readback and raw HRESULTs |
| `PublicationTracker.cs` | Publisher lifecycle state and cleanup decisions |
| `Options.cs` | Validated command/mode/SSID/duration parsing |
| `Inventory.cs` | OS/runtime and netsh capability inventory |
| `ProbeLog.cs` | Structured logs |

No SDK is needed to use the packaged diagnostic on Windows:

```powershell
cd .\tools\windows-probe
.\Run-Probe.cmd inspect
```

`inspect` is the read-only inventory command. Historical `protected` and `open` runs are already documented; do not launch them automatically just because their commands appear in the original README. No Nintendo handshake, GamePad emulation, media or controller bridge is implemented.

To build/check the existing source using an installed .NET 8 SDK, from the handoff root:

```powershell
dotnet build .\tools\windows-probe\src\DrcLink.Probe\DrcLink.Probe.csproj -c Release
dotnet run --project .\tools\windows-probe\tests\DrcLink.Probe.Checks -c Release
```

The main target is `net8.0-windows10.0.19041.0`, with unsafe calls enabled and Windows SDK reference package `10.0.19041.56`. The tests target ordinary `net8.0`. `scripts/Build.ps1` publishes a self-contained win-x64 executable into `rebuilt`; it does not replace the bundled application automatically. The included `Run-Probe.cmd` still uses the bundled `app` and `runtime`, so explicitly choose a rebuilt executable when testing source changes.

Historical preparation validation: Roslyn compile, zero warnings, 27 checks passed, and CLI help/invalid-argument/unsupported-platform exit behavior checked. Tests use unmanaged ABI doubles and do not exercise real Windows WinRT or the radio. The actual user-supplied results are separately recorded under `evidence`.

The old Linux scratch environment had an MSBuild/.NET process-discovery issue, so direct Roslyn compilation was used there. That environment-specific workaround is not a portable project requirement. Prefer normal SDK builds in Codex and diagnose the actual environment if they fail. No `/tmp/drc-build-tools` directory is required by this handoff.

## WDI collector

`Collect-WdiBoundary.ps1` uses built-in WDILib WPP and NDIS providers, starts the protected probe, then stops its trace. It enables no packet-capture provider through `capture=yes`, changes no Embedded Mode policy and installs no driver. Debug traces may still contain network identifiers/security diagnostics.

`SymbolIdentity.cs` extracts PE CodeView PDB identity; matching symbols/TMFs may be needed to decode WPP messages. Public symbol availability and useful event coverage are not guaranteed.

Historical validation: official PowerShell 7.4.6 parser reported zero syntax errors; the C# helper compiled without warnings; PE32/PE32+ and malformed-data cases were checked; results on Windows `dotnet.exe` and `hostfxr.dll` matched .NET PEReader. The collector's Windows/AX210 runtime behavior has not been reported. The original `VALIDATION.md` states those limits.

If intentionally resuming the Windows branch, use its own README and pass the moved probe location:

```powershell
# Administrator PowerShell; from the handoff root.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\windows-driver-boundary\Collect-WdiBoundary.ps1 -ProbeRoot .\tools\windows-probe
```

This starts an active radio/trace experiment and is not a required setup step for Pi development. It needs a phone connection during the observation window. Keep raw ETLs local until needed for decoding.

## Firmware work

[tools/pi-tsf](../tools/pi-tsf/README.md) now contains a versioned command ABI,
minimal Nexmon variant/patch, source audit, host sampler, read-only inventory
collector, offline checks and build instructions. The prototype was compiled
and linked locally with pinned GCC 5.4.1, but its actual hardware TSF backend
is disabled pending ROM/layout/clock-state verification. Built firmware stays
in the ignored upstream checkout and is not packaged for installation.

See [source review](../tools/pi-tsf/SOURCE_REVIEW.md),
[validation](../tools/pi-tsf/VALIDATION.md) and
[Pi provisioning/remote access](PI_SETUP.md). No Pi firmware has been installed,
no kernel vendor transport or hardware clock has been tested, and no GamePad
stage has passed. The original firmware research remains a historical
source-supported assessment; its string addresses are not callable entry points.

## Handoff validation

`HANDOFF_VALIDATION.md` records integrity, relative-link and evidence checks for this documentation package. The package preparation does not rerun historical hardware experiments or change their status.
