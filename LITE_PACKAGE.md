# Lightweight handoff

All project documentation, the full Pi firmware research, original evidence, Windows diagnostic source/tests and driver-boundary collector are retained.

Removed: `tools/windows-probe/app/` and `tools/windows-probe/runtime/` (compiled application and portable .NET runtime). No Pi firmware or upstream repository checkout was included in the original either.

To continue the Pi research, start with `CODEX_START.md`; no Windows rebuild is needed.

To run the Windows probe again, install the .NET 8 SDK on Windows and run `tools/windows-probe/scripts/Build.ps1`. The launcher now uses its `rebuilt/DrcLink.Probe.exe`. Older diagnostic READMEs and the collector describe the original portable distribution; the collector requires that original package layout. Do not rerun completed experiments just because binaries were removed.

`HANDOFF_VALIDATION.md` records historical checks on the original package. The lightweight packaging preserves retained source/evidence bytes, except README and launcher updates, and regenerates `MANIFEST.json`. No hardware checks or code tests were rerun.
