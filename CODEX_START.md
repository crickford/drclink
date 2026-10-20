# Starter request for Codex

Paste the following into Codex after opening the extracted project folder:

```text
Continue DrcLink using the documentation and existing code in this folder.

Read AGENTS.md, docs/PROJECT_CONTEXT.md, docs/STATUS_AND_DECISIONS.md,
docs/NEXT_PI_MILESTONE.md, and docs/PI_FIRMWARE_RESEARCH.md first.

The goal is a real Wii U GamePad connected without any Wii U console,
including for pairing. Windows 11 remains the game host. The active branch
uses a Raspberry Pi 4 Model B's onboard Wi-Fi and USB Ethernet to Windows.
Avoid introducing an additional Wi-Fi radio as an assumed dependency.

Start with the bounded Pi TSF milestone. Inspect the pinned Nexmon source,
verify the proposed wrapper/hook and firmware layout, design a versioned
read-only 64-bit TSF command, and prepare a minimal patch, host-side sampler,
offline checks, and reproducible build instructions. Do not load or install
firmware as part of the build. If the device information prevents a sound
target selection, state precisely what is missing and continue independent
source/design/build work instead of guessing addresses or image versions.

No Pi inventory has been provided yet. Ask me for the necessary read-only
inventory when needed. Do not repeat the completed Windows hotspot/service
experiments. No GamePad pairing or streaming has been demonstrated on either
the AX210 Windows route or the Pi onboard-radio route.

Distinguish a built patch from a hardware-tested patch, and clock access from
the still-unproven RSN advertisement, association, WPS and streaming stages.
Update the status, decisions and validation notes with concrete results.
```

This request starts source/build work. It does not imply that the previously researched firmware version matches the user's Pi or that a source patch is safe to execute on an unknown image.
