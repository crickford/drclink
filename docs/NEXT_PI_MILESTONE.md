# First Pi milestone: a measured, read-only 64-bit TSF interface

## Deliverable

Prepare one version-pinned Nexmon patch that exposes a read of the Wi-Fi hardware clock, plus a Linux host-side sampler and reproducible build/validation documentation. Do source/build work first. Do not add GamePad pairing, video streaming or a generic raw-injection AP to this milestone.

The first goal is to establish that the selected firmware's actual AP clock can be read through a controlled interface with measurable latency. A returned integer alone is insufficient. Loading changed firmware is separate from the fact that the new command itself is read-only.

## Obtain the actual device baseline

No Pi output is available yet. Request this inventory from the user's Pi; commands are read-only and may be unavailable on a minimal image. Record missing commands rather than treating their absence as radio incompatibility.

```bash
uname -a
cat /etc/os-release
tr -d '\0' < /proc/device-tree/model
printf '\n'
ethtool -i wlan0
iw dev
iw phy
iw reg get
```

If the wireless interface is not `wlan0`, substitute the interface found by `iw dev`. Also identify the firmware actually loaded from kernel logs and the active firmware/NVRAM/CLM files, their hashes and any symlink/compression layout. `ethtool` version output is useful but not a substitute for identifying the exact binary. Do not guess a filesystem path solely from the package name. A standard 5 GHz AP control test and USB Ethernet setup are later explicit hardware operations, not part of this inventory block.

## Source target for independent preparation

The Astra review inspected Nexmon commit `68720406f54ff16cad28fd3c51f2392aefad9fb7` and BCM43455c0 firmware target `7_45_206`. This is a reproducible research target, **not the known installed image**. Its inspected binary SHA-256 was:

```text
7fe369f7fdfded9eeceb8e438f7346e58634741b326d0715ad1b15275e6fa3a4
```

Use the exact source links and ROM/RAM cautions in [PI_FIRMWARE_RESEARCH.md](PI_FIRMWARE_RESEARCH.md). Relevant paths:

- `patches/common/wrapper.c`: `wlc_bmac_read_tsf()` wrapper for BCM43455c0, recorded ROM address `0x9C76C`.
- `patches/bcm43455c0/7_45_206/nexmon/src/ioctl.c`: `wlc_ioctl_hook()`, with a recorded image-specific hook pointer at `0x20E264`.
- `utilities/libnexio/libnexio.c`: potential nl80211 vendor-command transport, including `USE_VENDOR_CMD=1`.
- A different `wlc_bmac_read_tsf_ram()` wrapper exists for `7_45_265_28bca26_CY`; do not conflate its RAM path/address with the older target.

These records identify where to verify a design. They are not an instruction to call raw addresses on the current device. Check the firmware struct layout, pointer derivation, clock-enabled state, wrapper signature, applicable flash patches and AP clock domain. Do not export a host-derived clock and label it hardware TSF.

## Suggested interface design

Audit existing command IDs before assigning one. Define an explicit ABI with a version/capability query and an unambiguous TSF representation (for example, separately defined little-endian low/high 32-bit words). Specify request/response sizes and failure behavior. Keep the API limited to clock reading; do not include arbitrary memory access, clock writes, channel changes or unsolicited radio state changes.

The firmware handler should reject short/invalid buffers, check required pointers/state before reading, call the verified reader, and return an explicit error rather than stale or uninitialized bytes. Use documented byte layout instead of relying on host C struct padding. A full 64-bit read is preferable to treating a zero-extended 32-bit monitor timestamp as permanently monotonic.

The host sampler should:

1. Query the ABI/version and record the firmware identity and transport configuration.
2. Bracket each read with `CLOCK_MONOTONIC_RAW` timestamps and retain both brackets, returned TSF, status and sequence number.
3. Write machine-readable samples (CSV or JSONL) and summarize duration percentiles, monotonicity/discontinuities, offset and measured drift. Keep measurement and filtering separate so raw outliers remain inspectable.
4. Run with bounded duration/rate and explicit timeouts. Reject malformed responses rather than silently falling back to host time.
5. Distinguish hardware-clock reset/BSS restart from counter wrap and from a failed/late read. If only a 32-bit path is tested, handle its roughly 71.6-minute wrap explicitly.

Use a midpoint anchor as an initial measurement model, not proof of symmetric SDIO transport. Evaluate fitting clock rate only when measurements support it. Barista's slope-one extrapolation and 5 ms sample rejection are implementation references, not established GamePad tolerances.

## Acceptance stages

| Stage | Required evidence |
| --- | --- |
| Source/design | Pinned sources, exact wrapper/struct/hook validation notes, command-ID audit, ABI definition |
| Offline build | Reproducible commands and compiler versions; build-only output; patch hash and matching base-image checks |
| Offline checks | Buffer lengths, ABI version/errors, word ordering/endian conversion, sample parsing, reset/wrap and failure handling |
| Hardware readiness | Actual board/firmware identity matches the intended target; original files and recovery method recorded; loading is a separate explicit action |
| Hardware observation | Values advance plausibly in the intended AP clock domain; measured request durations and discontinuities under idle/load; no unexplained firmware crashes/resets |
| Long-run validation | Sustained samples through resets/restarts and enough time to expose relevant wrap behavior; raw logs retained |

Record a blocker if wrapper/layout/clock semantics or latency cannot be established. Do not claim success from a clean compilation or a dummy responder. No universally valid error threshold was established by research; choose and justify measurement targets, then validate against the GamePad in later phases.

## Later milestones, in order of dependency

After baseline/clock work, isolate the Nintendo pairing AP and userspace registrar. Independently prove authoritative Nintendo RSN in beacon/probe output, protected association acceptance, userspace EAPOL derivation with firmware handshake offload excluded, standard CCMP key installation, DHCP and bidirectional traffic. Then establish stable control/input, known-compatible local media, Windows USB transport, and endurance/latency.

For the RSN stage, first test a bounded existing AP override only if its behavior is supported by the selected image; otherwise trace the firmware builder/parser. The RAM-image string addresses in the report are not code entry points. A same-radio monitor trace or internal buffer dump may not prove transmitted bytes; preserve that limitation if no independent capture is available.
