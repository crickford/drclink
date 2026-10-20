# Project context

## Outcome

Use a physical Wii U GamePad to play PC games: video to its screen, audio to its speakers, and buttons/sticks/touch input back to Windows. A Wii U console must not be an intermediary or an initial-pairing prerequisite.

The work so far establishes feasibility constraints and provides diagnostic tools. It does not implement the finished link. The user is an experienced software engineer and can supply terminal output and run bounded hardware experiments.

## Hardware and preferences

| Item | Known state |
| --- | --- |
| Game host | 2021 Razer Blade 14 |
| OS | Microsoft Windows 11 Home, version `10.0.26200`, build `26200` |
| Wi-Fi | Intel Wi-Fi 6E AX210 160MHz; driver `22.80.1.1`, dated 2021-09-12 |
| Existing Windows result | User successfully created a 5 GHz Mobile Hotspot and joined it from another device |
| New bridge candidate | Raspberry Pi 4 Model B; exact board revision, OS, kernel and installed firmware have not been supplied |
| Pi onboard radio target | CYW43455 / BCM43455c0 family, brcmfmac FullMAC driver; verify actual hardware identity before implementation/deployment |
| Extra adapter preference | Avoid another Wi-Fi adapter if possible; active Pi research targets the onboard radio |
| VM preference | Linux VM acceptable if latency stays low, but radio ownership/passthrough is unresolved |
| Latency target | Suitable for interactive gaming; no measured or numerical budget agreed |

No inventory of a user's M1 Mac was collected for this branch. A separate macOS idea appeared in conversation history, but Windows plus Pi is the latest direction here.

## Proposed Pi architecture

Windows captures the game and produces a GamePad-compatible encoded video stream plus audio. A local service sends it over USB Ethernet to the Pi. The Pi performs radio-specific timing, packetization, pairing/security and transmission, and sends GamePad input reports back to Windows. Windows then exposes those inputs to games. The input-device integration mechanism has not been selected.

| Component | Intended responsibilities | Implemented here? |
| --- | --- | --- |
| Windows media/input service | Capture, compatible encoding, USB transport, game-controller integration | No |
| USB Ethernet gadget | Local PC–Pi transport via the Pi 4 USB-C device-capable port | Supported by existing Raspberry Pi tooling; not configured/tested in this project |
| Pi link service | Nintendo WPS/WPA2, DHCP/control, packet transport, TSF clock adapter and pacing | No; upstream implementations provide reusable research/code |
| Pi Wi-Fi driver/firmware adaptations | Clock access and any required security advertisement/association changes | Research only; no project patch built or loaded |
| Existing Windows tools | API publication probe and WDI tracing collector | Included; see validation/status |

Keep encoding on Windows initially and avoid an added decode/re-encode stage on the Pi. This is a design direction, not a measured performance result. Windows must still produce the specialized H.264 representation the GamePad accepts; ordinary GPU-generated H.264 is not assumed compatible. The GamePad display is 854×480 at approximately 60 Hz; verify exact transport/encoder constraints against the protocol sources and selected implementation.

The Pi owns the wireless link. Windows would see a USB networking device, not automatically obtain raw control of the Pi's radio. Raspberry Pi's gadget tooling supports Windows RNDIS and the Pi 4 USB-C port. Power and driver setup need checking on the actual laptop. USB networking does not solve the GamePad-facing wireless protocol by itself.

## Protocol facts that guide the design

The [libdrc Wi-Fi documentation](https://libdrc.org/docs/re/wifi.html) describes:

- A 5 GHz 802.11n link.
- An open pairing AP with a Wii U-style SSID and a modified WPS exchange.
- WPS KDK rotated left three bytes before the KDF.
- A four-symbol user PIN prefix mapped from spade/heart/diamond/club to 0/1/2/3, followed by `5678`; a PC/Pi registrar can choose a matching code.
- Runtime WPA2 with Nintendo cipher/AKM OUI `a4:c0:e1`; CCMP selector ends in `04`, PSK selector in `02`.
- Derived PTK rotated left three bytes before use. Hardware can still perform ordinary CCMP with the correct installed temporal key.
- DHCP and the traditional link addresses `192.168.1.10` (host) / `192.168.1.11` (GamePad).

The Nintendo OUI is inside the standard RSN element, ID 48. It is not equivalent to appending a vendor-specific element, ID 221. The active firmware must advertise the intended security and accept the peer's association; changing userspace key derivation alone is insufficient.

TSF is the radio Time Synchronization Function clock. Exposing it does not by itself guarantee accurate scheduling: clock-domain identity, SDIO read latency/jitter, resets, wrap behavior and drift relative to host time all matter. Keep final packet timing on the Pi.

## Projects and directionality

- **libdrc / drc-hostap:** original direct host-to-real-GamePad prior art. Old Linux/mac80211 setup assumptions need adaptation.
- **Barista:** modern source for Nintendo hostap patches, registrar setup, runtime transport and clock approaches. Its real-GamePad path is Linux-oriented; this handoff does not claim a working Windows/Pi port.
- **Nexmon:** firmware-patching framework with BCM43455c0 targets. Monitor mode/injection support is not proof of GamePad hosting.
- **ywh233/libdrc-streaming:** close Windows-encoder/Pi-proxy experiment, but it used an external PAU09 radio and reported video freezing; it does not meet the onboard-radio objective.
- **Moonlight Wii U:** console-dependent approach excluded by the user.
- **Vanilla:** primarily a software GamePad connecting to a Wii U console, the reverse direction. Its compatibility reports cannot establish the needed host/AP behavior.

No additional adapter has been chosen or purchased as part of this project. No timing, end-to-end latency, encoder compatibility, successful console-free pairing or real-GamePad media session has been demonstrated on the selected Pi route.
