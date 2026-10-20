# Raspberry Pi 4 onboard Wi-Fi as a Wii U GamePad host

Research date: 19 October 2026. Scope: Windows 11 supplies compatible encoded media over USB Ethernet; a Raspberry Pi 4 Model B owns its onboard CYW43455/BCM43455c0 radio and directly pairs with and serves a real GamePad. No console, including during pairing, and no additional Wi-Fi adapter. This was source/documentation research only: no firmware was built, patched, installed, or exercised on hardware.

Later implementation note (same local date): a fail-closed ABI prototype has
since been built locally. The actual hardware reader remains disabled. See
[the new source review](../tools/pi-tsf/SOURCE_REVIEW.md) and
[validation record](../tools/pi-tsf/VALIDATION.md); the research findings below
retain their original evidence status. No firmware has been installed or tested
on the Pi.

## Assessment

**A credible firmware research project, but not a demonstrated or ready-to-install solution.** I found no end-to-end demonstration satisfying the onboard-radio constraint. The evidence does not justify declaring it impossible merely because brcmfmac is FullMAC. Existing Linux key-installation paths and patched hostapd can potentially handle the unusual key derivation without reimplementing CCMP inside firmware. The important unverified radio work is authoritative Nintendo RSN advertisement, acceptance of Nintendo association requests, and reliable use of the AP's actual hardware clock.

The best first implementation milestone is a **read-only 64-bit TSF accessor on one pinned firmware image**. Nexmon already identifies a BCM43455c0 hardware-TSF reader and a custom-ioctl insertion point. This offers a bounded experiment before tackling unknown beacon and association paths. Afterward, isolate pairing and security tests before adding Windows capture or streaming.

**Prominent implementation trap:** hostapd 2.11 can automatically choose AP four-way-handshake offload when the driver advertises it. If that happens, its patched userspace PTK-rotation function is bypassed. The Nintendo build must deliberately retain the userspace authenticator and verify that choice; changing the derivation routine alone is insufficient.

## What the evidence establishes

| Requirement | Established evidence | Remaining uncertainty |
|---|---|---|
| 5 GHz AP operation | brcmfmac has an AP configuration path; Pi 4's chip has a supported Nexmon target. | Prove stable AP operation on the chosen board, firmware, kernel, regulatory domain and channel. Ordinary AP success does not imply GamePad compatibility. |
| Console-free pairing | libdrc describes computer-hosted WPS; Barista implements a registrar and generated credentials. | Its exact Nintendo WPS advertisement and EAPOL exchange must survive the Pi FullMAC path. |
| WPS key modification | Existing hostapd patches rotate KDK left three bytes before the WPS KDF. | No Pi firmware cryptography change is needed if hostapd receives/handles the exchange; verify this on hardware. |
| Runtime WPA2 key modification | Existing patches rotate the derived PTK left three bytes; payload cipher remains CCMP. | Keep firmware handshake offload disabled and establish successful key installation/traffic. |
| Kernel-facing cipher | Barista explicitly maps Nintendo CCMP to standard CCMP for nl80211 key installation. | Audit every driver-facing crypto parameter, rather than changing global on-air constants indiscriminately. |
| Beacon/probe RSN | Linux parses the RSN into firmware security settings; its vendor-IE path does not forward RSN element 48. | No verified existing arbitrary-RSN override for this firmware/AP mode was found. |
| Incoming association RSN | Firmware image contains WPA2 association-parser names/diagnostics; brcmfmac reports successful firmware association events to cfg80211. | Firmware may reject Nintendo selectors before hostapd sees a station. Must test separately from EAPOL. |
| TSF | Nexmon maps a hardware reader; Infineon confirms production firmware disables the ordinary `wl tsf` command in the cited Pi 4 case. | Correct AP clock, host/radio offset, read latency, drift and resets must be measured. |
| AP plus monitor | Nexmon's Raspberry Pi instructions explicitly say an AP can coexist with the monitor interface. | Verify the specific combination; this is not proof of Nintendo operation or reliable capture of the AP's own transmissions. |
| Whole system | Windows-to-Pi-to-GamePad streaming was attempted by ywh233 using an external PAU09. | That project does not demonstrate the onboard radio, and its video froze after seconds. |

## Source snapshots

These are reproducible snapshots inspected during this research, not claims that they are the only appropriate versions to deploy.

| Component | Snapshot |
|---|---|
| Nexmon | `68720406f54ff16cad28fd3c51f2392aefad9fb7` |
| Nexmon firmware target inspected | `bcm43455c0/7_45_206` |
| Firmware binary SHA-256 | `7fe369f7fdfded9eeceb8e438f7346e58634741b326d0715ad1b15275e6fa3a4` |
| Barista | `081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf` |
| Hostap source pinned by Barista | hostap 2.11, `d945ddd368085f255e68328f2d3b020ceea359af` |
| Linux brcmfmac source inspected | upstream tag `v6.12` |
| ywh233/libdrc-streaming | `c4b9e0adff520d0f282232959ae41463aa3573f7` |
| ywh233/libdrc | `853c1659211ccabe7f0ff30ddda6d9283e70e111` |

## Concrete patch and investigation points

### 1. Preserve userspace ownership of pairing and key derivation

Reuse the known protocol changes rather than implement new crypto:

- `src/wps/wps_common.c`, `wps_derive_keys()`: rotate KDK three bytes left before `wps_kdf()`.
- `src/common/wpa_common.c`, `wpa_pmk_to_ptk()`: rotate the complete derived PTK buffer three bytes left before splitting its constituent keys.
- `src/common/wpa_common.h`: Nintendo CCMP selector `a4:c0:e1:04` and PSK AKM selector `a4:c0:e1:02` on air.
- `src/drivers/driver_nl80211.c`, `wpa_alg_to_cipher_suite()`: return standard `00:0f:ac:04` when installing the resulting CCMP temporal key.
- Nintendo WPS vendor type and compatible registrar advertisements also matter. Barista's patches 0005–0008 cover WPS/vendor/HT advertisement details; the initial three crypto patches are not its entire pairing implementation.

Pinned patch sources:

- [PTK and selectors](https://github.com/BetaZay/Barista/blob/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf/third_party/hostapd-drc/patches/0002-wpa-add-Tendonin-RSN-selectors-and-PTK-rot3.patch)
- [WPS KDK](https://github.com/BetaZay/Barista/blob/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf/third_party/hostapd-drc/patches/0003-wps-add-Tendonin-KDK-rot3-support.patch)
- [Kernel CCMP mapping](https://github.com/BetaZay/Barista/blob/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf/third_party/hostapd-drc/patches/0004-nl80211-use-standard-ccmp-selector-for-driver-keys.patch)
- [Complete patch directory](https://github.com/BetaZay/Barista/tree/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf/third_party/hostapd-drc/patches)

Barista's `core/drh/server/backend_linux.cpp` contains `WritePairingCredentialsBlob()`, `WritePairingHostapdConfig()` and `WriteRuntimeHostapdConfig()`. Its pairing configuration supplies an internal EAP server, WPS registrar, generated credentials and Nintendo vendor elements; runtime configuration switches to the protected AP. This is a more useful starting point for the no-console requirement than the old libdrc guide's console-assisted credential acquisition instructions.

Sources: [Barista backend](https://github.com/BetaZay/Barista/blob/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf/core/drh/server/backend_linux.cpp), [libdrc Wi-Fi peculiarities](https://libdrc.org/docs/re/wifi.html), [libdrc network guide](https://libdrc.org/docs/network.html).

### 2. Explicitly prevent handshake offload

Hostap 2.11's `src/drivers/driver_nl80211_capa.c` recognizes `NL80211_EXT_FEATURE_4WAY_HANDSHAKE_AP_PSK` and sets `WPA_DRIVER_FLAGS2_4WAY_HANDSHAKE_AP_PSK` (lines 709–710 in the inspected source). `src/ap/beacon.c` then fills `params->psk`/`psk_len` when that capability is present (around lines 2381–2397). `driver_nl80211.c` sends `NL80211_ATTR_PMK` in the AP setup (around 5192–5196).

A narrowly scoped Nintendo-build change should suppress this capability consistently at discovery, so the higher layers and driver backend agree that hostapd owns the handshake. Alternatively use an equivalent verified disable mechanism. Do not assume changing only an outgoing parameter is sufficient to restore hostapd's state machine. Test the actual EAPOL transcript and key-install operations; there must be no firmware-computed standard PTK silently replacing the rotated one.

Sources: [capability parsing](https://chromium.googlesource.com/external/w1.fi/cgit/hostap/+/d945ddd368085f255e68328f2d3b020ceea359af/src/drivers/driver_nl80211_capa.c), [AP parameter construction](https://chromium.googlesource.com/external/w1.fi/cgit/hostap/+/d945ddd368085f255e68328f2d3b020ceea359af/src/ap/beacon.c), [nl80211 AP setup](https://chromium.googlesource.com/external/w1.fi/cgit/hostap/+/d945ddd368085f255e68328f2d3b020ceea359af/src/drivers/driver_nl80211.c).

### 3. Use existing brcmfmac key transport; adapt the RSN configuration boundary

In `drivers/net/wireless/broadcom/brcm80211/brcmfmac/cfg80211.c` at Linux v6.12:

| Function | Source-backed behavior |
|---|---|
| `brcmf_cfg80211_add_key()` (2777 onward) | Copies supplied keys; standard CCMP selects `CRYPTO_ALGO_AES_CCM`. |
| `send_key_to_dongle()` (548 onward) | Sends `wsec_key`, after waiting for pending 802.1X transmissions. |
| `brcmf_cfg80211_start_ap()` (5244 onward) | Selects PSK offload when `crypto->psk` is supplied. |
| `brcmf_valid_wpa_oui()` / `brcmf_configure_wpaie()` (4510 onward) | Reject nonstandard RSN OUIs and derive `auth`, `wsec`, `wpa_auth` settings. |
| `brcmf_parse_vndr_ies()` (4764 onward) | Accepts vendor-specific IEs, skips non-vendor elements; RSN is not forwarded here. |
| `brcmf_config_ap_mgmt_ie()` (5015 onward) | Passes beacon/probe-response/association-response vendor IEs to firmware. |
| `brcmf_notify_connect_status_ap()` (6461 onward) | Reports successful firmware association events and received association IEs. |

Source: [pinned Linux cfg80211.c](https://github.com/torvalds/linux/blob/v6.12/drivers/net/wireless/broadcom/brcm80211/brcmfmac/cfg80211.c). The EAPOL Ethernet transmit path is also visible in [`core.c`](https://github.com/torvalds/linux/blob/v6.12/drivers/net/wireless/broadcom/brcm80211/brcmfmac/core.c).

**Inference and proposed adaptation:** permit Nintendo selectors at the AP configuration boundary while mapping their cipher/auth meaning to ordinary AES-CCMP/PSK internally. This can be a scoped driver parser change, or a deliberately translated driver-facing copy of security configuration. Preserve Nintendo bytes in hostapd's actual protocol state. This addresses one known rejection; it does not prove the transmitted RSN is correct.

### 4. Establish authoritative beacon and probe-response RSN control

The key distinction is **Nintendo OUI inside a standard RSN element (element ID 48)** versus a vendor-specific IE (element ID 221). Merely appending `vendor_elements`, accepting its OUI in the driver, or injecting occasional replacement beacons does not establish the AP's authoritative RSN. Firmware can still transmit its own standard RSN or reject the peer's association.

The `wpaie` iovar exists in brcmfmac's station connection path. I did not establish that it overrides AP beacon/probe-response RSN on BCM43455c0 7.45.206. It is a bounded experiment candidate, not an existing solution. Any claimed override must survive BSS restart, beacon refresh, directed/wildcard probes and association; check for duplicate/conflicting RSN elements.

If the iovar route fails, investigate the firmware IE-manager callbacks that build RSN for beacon/probe output. The pinned RAM image contains registration diagnostics referencing `wlc_iem_add_build_fn` for RSN in beacons. That establishes a useful reverse-engineering lead, not a verified callable build-function address. A targeted builder/serialization patch is preferable to running a second injected management-plane stream alongside the firmware AP.

### 5. Test incoming association separately from EAPOL

The firmware image contains the name `wlc_assoc_parse_wpa2_ie` and association/RSN validation diagnostics. FullMAC association processing is therefore a distinct potential blocker even with userspace WPA authentication. Confirm that a Nintendo association request results in a successful station event before debugging the four-way handshake.

If firmware rejects the selectors, locate its RSN cipher/AKM interpretation path and introduce a Nintendo-specific mapping while preserving enough original peer-IE information for hostapd's consistency checks. Exact implementation requires tracing the selected image. **No verified code address for this parser or the RSN beacon builder was identified in this pass.**

For reproduction, these are string addresses in the inspected RAM image, using its declared load base `0x198000`; they are **not function entry points**:

| String/data lead | Image address |
|---|---:|
| RSN beacon build-registration diagnostic | `0x1f9b5d` |
| RSN association parse-registration diagnostic | `0x1f9c37` |
| `wlc_assoc_parse_wpa2_ie` name | `0x1fa255` |
| `wlc_ap_process_assocreq_done` name | `0x1fa288` |

Sources: [pinned firmware binary](https://github.com/seemoo-lab/nexmon/blob/68720406f54ff16cad28fd3c51f2392aefad9fb7/firmwares/bcm43455c0/7_45_206/brcmfmac43455-sdio.bin), [firmware layout](https://github.com/seemoo-lab/nexmon/blob/68720406f54ff16cad28fd3c51f2392aefad9fb7/firmwares/bcm43455c0/7_45_206/definitions.mk). A complete analysis may also require the chip's ROM and applicable flash patches; the RAM image alone is not the entire firmware.

### 6. Add a small, read-only TSF interface

This is the strongest concrete firmware insertion point found:

- Nexmon `patches/common/wrapper.c` identifies `wlc_bmac_read_tsf(void *wlc_hw, unsigned int *tsf_l_ptr, unsigned int *tsf_h_ptr)` for `CHIP_VER_BCM43455c0`, `FW_VER_ALL`, address **`0x9C76C`** (around lines 745–754).
- `patches/bcm43455c0/7_45_206/nexmon/src/ioctl.c` already defines `wlc_ioctl_hook()`. For this exact image its hook pointer is patched at **`0x20E264`**.
- A new read command can return the reader's low/high values, with strict buffer-length checking, no TSF writes, and a version/capability identifier. The struct layout and wrapper call must be validated against the exact image before execution. `FW_VER_ALL` is Nexmon metadata, not independent verification against every installed firmware. The same wrapper file also defines `wlc_bmac_read_tsf_ram()` at `0x1BFF78` specifically for BCM43455c0 `7_45_265_28bca26_CY` (around line 1254). Resolve the active ROM/RAM/flash-patch path for the actual chosen image; do not blindly transplant the ROM address into newer firmware.

Sources: [wrapper](https://github.com/seemoo-lab/nexmon/blob/68720406f54ff16cad28fd3c51f2392aefad9fb7/patches/common/wrapper.c), [ioctl hook](https://github.com/seemoo-lab/nexmon/blob/68720406f54ff16cad28fd3c51f2392aefad9fb7/patches/bcm43455c0/7_45_206/nexmon/src/ioctl.c).

The ordinary vendor utility is not a proven substitute. A 2019 Infineon reply suggested `wl tsf`, but the more specific 2021 Pi 4 investigation records error -23 on production 7.45.229 and a 7.45.234 WLTEST image. The vendor subsequently confirmed the command was disabled in production firmware and available only in debug variants. That finding concerns those releases; do a read-only capability probe on the selected release, but do not assume its presence from `wl --help`.

Sources: [2019 suggestion](https://community.infineon.com/t5/Wi-Fi-Bluetooth-for-Linux/Software-driver-access-to-TSF-on-CYW43455-Raspberry-PI-3B/td-p/94621), [Pi 4 investigation and vendor explanation](https://community.infineon.com/t5/Wi-Fi-Bluetooth-for-Linux/Request-wl-tool-amp-mfg-firmware-for-Raspberry-Pi-4B-running-64-bit-Linux/td-p/322601).

Recent Nexmon utilities offer another useful simplification: `utilities/libnexio/libnexio.c` has an nl80211 vendor-command transport (`USE_VENDOR_CMD=1`). Nexmon CSI's maintainer documents use without a modified brcmfmac on recent Raspberry Pi OS, and explicitly confirms kernel 6.12. This can carry a custom firmware command; it does not remove the separate RSN configuration problems. The demonstrated tutorial platform is Pi 5, so validate the Pi 4 deployment rather than treating it as an end-to-end Pi 4 proof.

Sources: [libnexio](https://github.com/seemoo-lab/nexmon/blob/68720406f54ff16cad28fd3c51f2392aefad9fb7/utilities/libnexio/libnexio.c), [Nexmon CSI discussion 395](https://github.com/seemoo-lab/nexmon_csi/discussions/395).

## Clock exposure does not solve clock accuracy

The original libdrc design reads TSF through `GetTsf()`/`pread()`; the inspected ywh233 fork timestamps audio packets directly and samples TSF repeatedly in its video scheduling callback. It does not estimate a host/radio frequency ratio. Its video schedule advances by `1000000/59.94` and rearms against a fresh radio timestamp.

Sources: [tsf-linux.cpp](https://github.com/ywh233/libdrc/blob/853c1659211ccabe7f0ff30ddda6d9283e70e111/src/tsf-linux.cpp), [video scheduling](https://github.com/ywh233/libdrc/blob/853c1659211ccabe7f0ff30ddda6d9283e70e111/src/video-streamer.cpp), [audio scheduling](https://github.com/ywh233/libdrc/blob/853c1659211ccabe7f0ff30ddda6d9283e70e111/src/audio-streamer.cpp).

Barista implements three relevant paths:

1. An optional RTL8852BE-specific hardware-register clock samples every 100 ms, anchors at the midpoint of host timestamps surrounding the read, rejects reads lasting over 5 ms, and extrapolates at one radio microsecond per host microsecond. This is not a regression-based drift estimator and its register addresses are not applicable to Broadcom.
2. Its generic TSF-file path performs a direct read per `timestamp_us()` call.
3. Its monitor fallback pairs radiotap TSF with kernel RX timestamps, eliminating userspace socket queue delay, then extrapolates with slope one. It does not eliminate earlier radio-to-host/SDIO delay.

Sources: [ApTsfClock](https://github.com/BetaZay/Barista/blob/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf/core/drh/ap_tsf_clock.h), [runtime transport](https://github.com/BetaZay/Barista/blob/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf/core/drh/runtime_transport.cpp).

Nexmon 7.45.206 radiotap fills its timestamp using `sts->mactime` for the low word and zero for the high word. A consumer treating that as a forever-increasing 64-bit value must handle the roughly 71.6-minute low-word wrap. Also verify that the received-packet timestamp represents the same AP TSF domain used in emitted beacons; a receiver timestamp is not automatically an authoritative AP-clock reading.

Source: [monitormode.c, lines 97–98](https://github.com/seemoo-lab/nexmon/blob/68720406f54ff16cad28fd3c51f2392aefad9fb7/patches/bcm43455c0/7_45_206/nexmon/src/monitormode.c).

**Proposed clock work:** bracket ioctl reads with `CLOCK_MONOTONIC_RAW`, record distributions under idle and stream load, reject slow outliers, fit/track offset and frequency if measurements justify it, and detect BSS resets explicitly. Keep final GamePad timestamps and packet pacing on the Pi. A timestamp assigned on Windows before USB transit includes variable transport/queue age. Read latency and host scheduling should be measured separately from oscillator drift. No universal microsecond acceptance threshold was established by these sources; set an engineering target and validate against sustained real-GamePad playback.

## Firmware/kernel selection and coexistence

At the inspected Nexmon commit, the support matrix lists BCM43455c0 7.45.154 and .189 with monitor/radiotap support, and .206 with frame injection as well. The .234 row lists firmware/ucode patching but does not claim the same monitor/injection features. The repository also contains a .241 directory; directory existence is not a full support claim. Drivers in the source tree reach 6.6.y, while parts of README still list older kernels. Pin firmware, board NVRAM/CLM data, kernel, driver and utility versions as a tested set; copying old firmware offsets into an arbitrary current image is invalid.

The README explicitly allows AP operation alongside its monitor interface. Older issue reports show failed combinations, including an older Pi 4/Kali setup. This supports a version-specific coexistence test, not a categorical impossibility claim. Ordinary encrypted data can use the normal AP network interface; raw injection need not be the streaming architecture.

Sources: [Nexmon README](https://github.com/seemoo-lab/nexmon/tree/68720406f54ff16cad28fd3c51f2392aefad9fb7), [Pi 4 coexistence issue 365](https://github.com/seemoo-lab/nexmon/issues/365), [earlier AP/monitor issue 92](https://github.com/seemoo-lab/nexmon/issues/92).

Nexmon CSI targets BCM43455c0 7.45.189 and establishes sophisticated firmware modification and command transport. Channel-state extraction is not a GamePad host implementation; adding CSI to this project is unnecessary unless a specific measurement needs it.

Source: [Nexmon CSI](https://github.com/seemoo-lab/nexmon_csi).

## Minimal experiments and stop conditions

These are proposed follow-up experiments, not completed tests. Keep one change per test and retain exact versions, logs and reproducible results.

| Stage | Experiment | Proceed only when |
|---|---|---|
| Baseline inventory | Record board/chip revision, kernel, firmware ID/hash, NVRAM/CLM, AP/channel capabilities. Establish the USB Ethernet control path and a plain 5 GHz AP on a legal supported channel. | AP and USB link are stable; management does not compete for the radio. |
| First firmware milestone | Implement only the read-only 64-bit TSF accessor on the pinned image. Compare repeated samples with host time before/after each call, idle and loaded. | Reader advances consistently, returns plausible values, handles restart, and has bounded measured latency. Otherwise stop before beacon work. |
| Pairing AP | Reuse Nintendo WPS registrar logic and advertisement; observe discovery, WPS exchange, generated credential transfer and GamePad persistence. | A never-console-assisted pairing completes, and the same saved credentials support the next runtime stage. |
| RSN transmission | Test an existing AP-specific override if supported; otherwise trace and patch the firmware RSN builder. Inspect both beacon and probe behavior. | Exactly the intended Nintendo RSN is authoritative, including after refresh/restart; appending a second RSN is a failure. |
| Association | Bring up the protected BSS and attempt the saved GamePad association, before interpreting EAPOL results. | Firmware emits success and hostapd sees the correct peer IE. If firmware rejects it, investigate the association parser rather than changing PTK code blindly. |
| EAPOL and keys | Explicitly disable PSK offload; run the rotated handshake and standard CCMP key installation. | Four-way exchange completes, keys install, and DHCP/bidirectional encrypted packets work. |
| Input/control | Run the protocol/control service without video load and forward inputs to Windows. | Stable input/control session and recovery after disconnect. |
| Local media | Send a known-compatible encoded test pattern and audio, with Pi TSF-based packetization/pacing. | Sustained real-GamePad playback; no periodic loss of sync. Input success alone is insufficient. |
| Windows integration | Add capture/encoding over USB Ethernet, bounded queues, then game input mapping. | Added transport preserves stability and acceptable latency. |
| Endurance | Run at least a multi-hour session and repeated pairing/runtime restarts. | No TSF wrap/reset bug, repeated video freeze, or accumulated audio/video drift. |

Within the no-additional-adapter constraint, use firmware instrumentation and the onboard monitor path where supported. A same-radio capture may omit its own transmitted frames; an internal buffer dump proves intended bytes, not necessarily transmitted bytes. If no independent receiver/capture is available, explicitly retain that verification limitation rather than report authoritative over-the-air behavior as proven. Do not make a second adapter a required part of the final architecture.

## Existing projects: what they do and do not demonstrate

**ywh233/libdrc-streaming:** its Windows encoder sends pre-encoded H.264 chunks to a Pi proxy, which calls a modified libdrc. The stated hardware includes an external rt2800usb Panda PAU09 and Ethernet. The author observed video freezing after about 5–10 seconds while button input continued and suspected TSF drift. That attribution is not a controlled root-cause proof. It demonstrates a partial split-media architecture, not onboard Pi hosting.

Source: [pinned streaming project](https://github.com/ywh233/libdrc-streaming/tree/c4b9e0adff520d0f282232959ae41463aa3573f7).

**Barista issue 2:** the maintainer initially expressed strong doubt about a BCM4364 FullMAC adapter. Later, after examining capabilities, they reopened the investigation and requested further logs. The shown logs had `interface=none`, and the maintainer noted that the engine did not appear to start. This is not a completed radio-level rejection, not a BCM43455 test, and not proof that firmware modification cannot work.

Source: [complete issue 2](https://github.com/BetaZay/Barista/issues/2).

**libdrc:** its tested radio list is rt2800usb plus ath9k/carl9170 with caveats. Its old mac80211 TSF export is not a drop-in patch for brcmfmac. The protocol documentation and media implementation remain useful; a new FullMAC clock interface and radio compatibility work are separate responsibilities.

## Effort and decision

The likely smallest useful code contribution is the TSF accessor plus measurements. The next substantial contribution is establishing Nintendo RSN transmission and association acceptance, while keeping hostapd in charge of key derivation. A complete new crypto implementation or a generic monitor/injection bridge is not the best starting point.

Planning judgment, not a measured estimate: an experienced embedded wireless engineer with the actual Pi and GamePad should budget days to a few weeks for the first clock/control proof and potentially several weeks to months for firmware tracing, security interoperability and streaming endurance. A failed or incompatible firmware path could extend that indefinitely. There is no defensible completion promise before the radio/security milestones pass.

**Go** if the objective is a bounded hardware/firmware research project with staged stop points. **No-go as an immediately dependable accessory or a promised software-only weekend setup.** Preserve the onboard-radio constraint throughout; existing external-adapter demonstrations should not be presented as satisfying it.
