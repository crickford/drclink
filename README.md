# DrcLink: Codex handoff

**Current state: feasibility research and working Windows diagnostic tools. There is no completed GamePad link.**

Build software that lets a real Wii U GamePad display PC games, play audio, and send controller/touch input to a Windows PC, without a Wii U console, including during pairing. The latest direction is a Raspberry Pi 4 Model B using its **onboard Wi-Fi** for the GamePad and USB Ethernet for the Windows connection.

This lightweight package retains documentation, research, evidence, diagnostic source and tests. Compiled Windows binaries and the bundled .NET runtime are omitted. It does not include Nexmon/Barista checkouts, proprietary Pi firmware, a Pi firmware patch, or a finished streaming application. Fetch the pinned upstream sources when needed. You do not need access to the original chat or its filesystem paths.

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

The next task is to prepare a **read-only 64-bit radio TSF accessor** for one pinned Pi firmware target, with a host-side measurement tool and reproducible build instructions. First obtain the actual Pi board/kernel/firmware inventory; none has been supplied yet. Independent source inspection, ABI design and build preparation can continue while that information is missing.

This clock prototype will not establish Nintendo-compatible security advertisements, association, pairing or streaming. Those are distinct milestones. “Read-only” describes what the proposed command does; loading a firmware patch is still a hardware-changing deployment step.

## Included existing tools

- [tools/windows-probe](tools/windows-probe/README.md): DrcLink.Probe 0.1.1 source and tests. Build with the .NET 8 SDK on Windows using `scripts/Build.ps1`; run the executable in `rebuilt`. The portable application/runtime are omitted. It is a Wi-Fi API feasibility probe, not GamePad software.
- [tools/windows-driver-boundary](tools/windows-driver-boundary/README.md): WDI/NDIS observation collector and symbol reader. Its Windows hardware run is still pending.
- [evidence](evidence/README.md): copies of the uploaded Windows results plus user-reported hardware/service observations.

The older tool READMEs describe experiments as they were designed. For which ones have already run, use [the status document](docs/STATUS_AND_DECISIONS.md). Do not automatically repeat the protected/open tests or restart Embedded Mode troubleshooting.

## Snapshot and portability

Handoff prepared 19 October 2026, America/Los_Angeles. Raw log timestamps are UTC. The project has no configured remote Git repository and no application build/deployment pipeline beyond the included diagnostics. You may initialize a repository in the extracted folder. `MANIFEST.json` inventories included files and SHA-256 hashes. Runtime/third-party notices remain alongside the Windows tools.
