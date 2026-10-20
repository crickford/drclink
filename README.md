# DrcLink: Codex handoff

**Current state: feasibility research and working Windows diagnostic tools. There is no completed GamePad link.**

Build software that lets a real Wii U GamePad display PC games, play audio, and send controller/touch input to a Windows PC, without a Wii U console, including during pairing. The latest direction is a Raspberry Pi 4 Model B using its **onboard Wi-Fi** for the GamePad and USB Ethernet for the Windows connection.

This repository retains documentation, research, evidence, diagnostic source and tests. Compiled Windows binaries and the bundled .NET runtime are omitted. The Pi work now includes a build-only Nexmon ABI prototype and a host sampler; its hardware reader remains disabled pending verification. Nexmon/Barista checkouts, proprietary Pi firmware and build artifacts stay outside tracked source. There is no finished streaming application.

## Start in Codex

1. Extract the entire ZIP to a writable project folder.
2. Open **this folder** in Codex so it can read the root `AGENTS.md`.
3. Paste the starter request from [CODEX_START.md](CODEX_START.md).

Read in this order:

| File | Purpose |
| --- | --- |
| [AGENTS.md](AGENTS.md) | Project constraints and development guidance |
| [docs/PROJECT_CONTEXT.md](docs/PROJECT_CONTEXT.md) | Goals, hardware, architecture and protocol requirements |
| [docs/STATUS_AND_DECISIONS.md](docs/STATUS_AND_DECISIONS.md) | What actually happened, what remains untested, and branch decisions |
| [docs/NEXT_PI_MILESTONE.md](docs/NEXT_PI_MILESTONE.md) | First bounded implementation task and acceptance criteria |
| [docs/PI_FIRMWARE_RESEARCH.md](docs/PI_FIRMWARE_RESEARCH.md) | GPT-6 Astra's detailed source review, pinned commits and patch candidates |
| [docs/WINDOWS_HISTORY.md](docs/WINDOWS_HISTORY.md) | Windows experiments, native ABI finding and service detour |
| [docs/TOOLS_AND_VALIDATION.md](docs/TOOLS_AND_VALIDATION.md) | Included code, build/run instructions and validation limits |
| [docs/SOURCES.md](docs/SOURCES.md) | Primary references and prior projects |
| [evidence/README.md](evidence/README.md) | Original experiment results and their interpretation |

## Immediate continuation

Start with [Pi setup and remote access](docs/PI_SETUP.md), then collect the actual board/kernel/firmware inventory. [The TSF prototype](tools/pi-tsf/README.md) includes the versioned command, sampler, offline checks and build instructions. It has been compiled locally, but its hardware reader is disabled because ROM/layout/clock-state verification remains incomplete. No firmware has been loaded and no Pi hardware result has been received.

This clock prototype will not establish Nintendo-compatible security advertisements, association, pairing or streaming. Those are distinct milestones. “Read-only” describes what the proposed command does; loading a firmware patch is still a hardware-changing deployment step.

## Included existing tools

- [tools/windows-probe](tools/windows-probe/README.md): DrcLink.Probe 0.1.1 source and tests. Build with the .NET 8 SDK on Windows using `scripts/Build.ps1`; run the executable in `rebuilt`. The portable application/runtime are omitted. It is a Wi-Fi API feasibility probe, not GamePad software.
- [tools/windows-driver-boundary](tools/windows-driver-boundary/README.md): WDI/NDIS observation collector and symbol reader. Its Windows hardware run is still pending.
- [tools/pi-tsf](tools/pi-tsf/README.md): minimal Nexmon ABI prototype, source audit, bounded sampler, inventory collector and offline checks. See its explicit source/build/hardware limits.
- [evidence](evidence/README.md): copies of the uploaded Windows results plus user-reported hardware/service observations.

The older tool READMEs describe experiments as they were designed. For which ones have already run, use [the status document](docs/STATUS_AND_DECISIONS.md). Do not automatically repeat the protected/open tests or restart Embedded Mode troubleshooting.

## Snapshot and portability

Handoff prepared 19 October 2026, America/Los_Angeles. Raw log timestamps are UTC. The repository remote is now `https://github.com/crickford/drclink.git`; the original handoff was imported on branch `work`. `MANIFEST.json` preserves the original package inventory/hashes and is not an inventory of later development. Runtime/third-party notices remain alongside the Windows tools. TSF source/build validation is recorded separately under `tools/pi-tsf` and `evidence/pi-tsf-offline-2026-10-19`.
