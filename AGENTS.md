# DrcLink project guidance

## Read first

Read `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/STATUS_AND_DECISIONS.md`, and `docs/NEXT_PI_MILESTONE.md`. Use `docs/PI_FIRMWARE_RESEARCH.md` for precise upstream paths and pinned sources. Keep the status and validation records current as work progresses.

## User intent and scope

- Target a physical Wii U **GamePad**, not a Wii Remote, Wii U Pro Controller, or a software-emulated GamePad connecting to a Wii U.
- A Wii U console must not be required for streaming **or initial pairing**.
- Windows 11 is the game host. User hardware: 2021 Razer Blade 14, Intel AX210, Windows 11 Home build 26200.
- Avoid an additional Wi-Fi adapter where possible. The active Pi branch specifically investigates the Pi 4 Model B's onboard radio. An external-adapter demonstration does not validate this branch.
- The user explicitly proposed the Pi as a USB-connected bridge. Preserve that option; “direct” means no Wii U console dependency, not a prohibition on the Pi.
- A Linux VM was acceptable if latency stayed low, but normal virtual networking does not give a VM control over the laptop's PCIe Wi-Fi radio.
- The latest authorized work was research and this Codex handoff. No Pi firmware deployment or system policy change has occurred.

## Engineering rules specific to this project

- Label every result as hardware-observed, locally tested, source-supported, proposed, or unresolved. A successful build, API return, ordinary hotspot, or input-only session is not complete GamePad compatibility.
- Keep protocol logic, clock access, driver adaptations, firmware patches and Windows media transport separate. Retain userspace control of Nintendo key derivation and explicitly account for AP handshake offload.
- Prefer small, version-pinned patches. Check exact chip revision, firmware hash, wrapper/layout definitions and applicable ROM/RAM/flash-patch paths. Do not reuse an address merely because its number appears in a report.
- Firmware string addresses are reverse-engineering leads, not callable function addresses.
- Nintendo's OUI inside RSN element 48 is not a generic vendor IE (221). Appending an IE or permitting the OUI in Linux does not prove the radio's authoritative RSN is correct.
- Reuse existing CCMP/key transport where it meets the protocol. Do not start by inventing firmware crypto or a raw-injection replacement AP.
- Keep timing/packet pacing on the Pi. Do not assume a Windows timestamp assigned before USB transit equals the GamePad-facing radio time.
- Source/build preparation and offline checks can proceed without hardware. Keep firmware loading, service changes and hardware tests explicit, separate operations; a normal build must not auto-install or flash anything. Prepare version checks and recovery instructions before any later deployment.
- Do not restart Embedded Mode troubleshooting or assert that Windows Home is the proven cause of the native `E_POINTER`. That cause remains unresolved.
- Do not describe Vanilla as a solution to this direction: its main purpose is the reverse connection, a software GamePad talking to a Wii U console.
- Do not assume arbitrary NVENC or other stock H.264 output satisfies the GamePad's special video constraints. Validate the encoded representation separately before optimizing it.

## Validation and reporting

Run checks appropriate to the changed component. For a TSF prototype, buffer validation, high/low ordering, reset/wrap behavior, firmware identity and measured read latency matter more than tests that mirror implementation. Record commands, versions, results and untested hardware assumptions. Preserve raw experiment results; add new runs rather than overwrite the historical evidence.

No universal clock-error or end-to-end latency threshold has been established. Do not present Barista's 5 ms read-rejection threshold as a GamePad specification. A source comment or a maintainer's early speculation is weaker evidence than an actual test; read the complete issue discussion.

Do not include downloaded firmware images or large upstream source trees in commits by default. Record hashes and pinned fetch/build instructions. Keep existing tool notices intact. Do not commit temporary pairing credentials or captured key material.
