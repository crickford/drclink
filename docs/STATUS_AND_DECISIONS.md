# Status and decisions

Snapshot: 19 October 2026 local time. The original uploaded logs use 16 October UTC for tests performed on 15 October in Los Angeles.

## Evidence status

| Item | Status | Evidence / limit |
| --- | --- | --- |
| Windows 5 GHz Mobile Hotspot | User-observed success | User reports another device connected; separate from our probe's band selection |
| DrcLink.Probe 0.1.0 protected control | Hardware-observed success | `evidence/windows-protected-v0.1.0-summary.json`: exit 0, Started, one request, one attached peer |
| Probe 0.1.0 open attempt | Hardware-observed failure | Null-passphrase assignment produced `0x80004003`; cleanup also failed before a publisher had started |
| Probe 0.1.1 native open attempt | Hardware-observed failure | Native QueryInterface succeeded; actual setter invoked and returned `0x80004003`; no Start attempted; correct cleanup skip |
| Embedded Mode service start | User-observed failure | Generic Start-Service failure; subsequent service status stopped with both stored exit codes zero |
| Actual open 5 GHz network | Not established | Neither WinRT open probe reached network startup |
| Windows WDI boundary collector | Packaged, locally checked | No user output or ETL has been received; hardware execution is unreported |
| Windows lower-level driver control | Candidate based on documentation | Native WLAN-to-WDI mappings exist; actual filter visibility/control on AX210 unproven |
| Pi firmware feasibility review | Completed source research | GPT-6 Astra report in `docs/PI_FIRMWARE_RESEARCH.md`; no hardware testing |
| Pi installed hardware/software inventory | Missing | Do not assume a particular OS, kernel or firmware version is installed |
| Pi TSF accessor | Locally built ABI prototype; actual reader blocked | Minimal pinned Nexmon variant and sampler in `tools/pi-tsf`; reader disabled pending ROM/layout/clock verification. No hardware run |
| Pi Nintendo RSN and association handling | Unresolved | Source-backed rejection points and reverse-engineering leads, no verified working adaptation |
| GamePad pairing / media / input integration | Not implemented in this project | No successful GamePad connection on either route |

## Branch decisions

### Original Windows/AX210 investigation

Keep it as a documented fallback/research branch. Stop repeating application-level open-hotspot and Embedded Mode service experiments: their useful result is already recorded. Native `E_POINTER` identifies the rejecting API boundary but does not establish whether the root cause is policy, implementation validation, driver or hardware. Windows Home's absence from the documented policy editions is evidence about support, not a proved diagnosis.

Microsoft documents Native WLAN requests mapping to WDI AP-start, key-install and advertisement operations. A normal NDIS filter may observe the Native WLAN side, but no custom driver was built or installed and no interception was demonstrated. The pending WDI collector is an observation experiment, not a prerequisite for the Pi branch.

### Latest direction: Pi 4 onboard-radio bridge

The user proposed a Pi as the USB Wi-Fi bridge and requested an Astra subagent investigation of patching its firmware. The resulting assessment is **credible R&D, no demonstrated end-to-end solution**. A broad assertion that FullMAC makes the project impossible is not supported.

The recommended first implementation milestone is a read-only 64-bit TSF command on a pinned image. Nintendo WPS/key derivation should remain in userspace where possible. Reuse ordinary CCMP/key transport, and account explicitly for automatic AP four-way-handshake offload. Authoritative RSN transmission and incoming association acceptance remain independent radio/firmware problems.

The user has not supplied Pi inventory or run a project Pi test. Firmware loading must not be bundled into the first local build. No new radio is an assumed dependency of the Pi experiment.

## Corrections and conclusions to preserve

1. The original managed `NullReferenceException` was ambiguous. The later direct native setter result confirmed native rejection. It did not prove a hardware limitation.
2. Starting the Embedded Mode service is not equivalent to configuring its policy; the service-start detour was inconclusive and is closed for now.
3. A protected phone connection is not Nintendo WPS, a GamePad association, or an open-network result. The probe reports band as unmeasured.
4. The full Barista issue #2 is not a definitive FullMAC failure: after an early negative comment, the maintainer reopened it; the supplied logs indicated no selected interface/engine start. It is also a different Broadcom chip.
5. A Nexmon function address or a firmware string is not automatically safe to call on another image. The report separates confirmed wrapper metadata from unverified parser/builder code locations.
6. The old Pi streaming project's attribution to TSF drift was the author's hypothesis, not a controlled root-cause proof.
7. Observed inputs continuing after video freezes do not validate a complete or stable link.

## Updating this file

Append dated results with exact hardware/software versions, commands and evidence paths. Promote a hypothesis to observed only when a corresponding result exists. Keep failed experiments and their narrow interpretations; do not erase them after a later change succeeds.

## 19 October 2026 — bounded TSF source/build continuation

- **User-reported:** A brand-new Pi 4B is available, and wired Ethernet can be
  connected to the router. No board revision, chip revision, OS/kernel, loaded
  firmware or SSH endpoint has been received. Provisioning and private remote
  access instructions are in [PI_SETUP.md](PI_SETUP.md); no extra radio is assumed.
- **Source-supported:** Re-fetched Nexmon commit
  `68720406f54ff16cad28fd3c51f2392aefad9fb7`, verified the exact research-image
  hash, original ioctl pointer `0x203b9` at `0x20e264`, wrapper metadata and 255
  original flashpatch entries. The shared structs explicitly warn about copied,
  potentially wrong layouts; no verified hardware-clock predicate or clean
  BCM43455c0 ROM is present. See `tools/pi-tsf/SOURCE_REVIEW.md` for exact limits.
- **Decision:** Keep `7_45_206` as an offline research target, not a selected
  deployment image. The new command's ABI/capabilities work in offline tests,
  but READ returns `UNVERIFIED_BACKEND` without calling the ROM reader. Do not
  treat `hw->up` as a clock-enabled guarantee or guess a `clk` offset. There is
  deliberately no build switch to enable an unverified reader.
- **Locally tested:** The small standalone Nexmon variant compiles, links and
  produces a patched image with the pinned GCC 5.4.1/plugin. It excludes the
  normal monitor/injection/console/UDP hooks and builds no driver. The build
  uses the existing ucode compression/reclaim layout and relocates original
  flashpatches. Artifact hashes, offline checks and exact commands are recorded
  in `tools/pi-tsf/VALIDATION.md` and `evidence/pi-tsf-offline-2026-10-19`.
- **Locally tested / proposed hardware use:** The standard-library sampler
  parses the Broadcom nl80211 DCMD reply, retains raw brackets/responses/errors,
  stops on unavailable capabilities and bounds request deadlines/duration/rate.
  Fixtures cover malformed buffers, word ordering, stale responses, resets,
  wraps and rate fits. Actual stock-kernel vendor transport is untested.
- **Unresolved:** This cloud environment has no configured VPN or TCP grant to
  the Pi. SSH setup requires a local Codex session on the LAN or the supported
  cloud Tailscale integration plus an exact TCP destination grant and SSH key.
  No firmware installation, AP/service changes or hardware experiments occurred.

RSN advertisement, association acceptance, WPS/rotated keys, GamePad pairing,
streaming and end-to-end latency remain unproven and outside this implementation.
Completed Windows experiments were not repeated.
