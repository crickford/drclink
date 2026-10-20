# Sources and reproducibility

These references were used during the project. They are evidence and code starting points, not guarantees of current hardware compatibility. The full Pi report contains file-specific links and distinguishes demonstrated behavior from proposals. URLs may change after this snapshot.

## Pinned Pi research sources

| Project | Revision / use |
| --- | --- |
| [Nexmon](https://github.com/seemoo-lab/nexmon/tree/68720406f54ff16cad28fd3c51f2392aefad9fb7) | `68720406f54ff16cad28fd3c51f2392aefad9fb7`; firmware wrappers, ioctl hook, monitor timestamp construction and utility transport |
| [Barista](https://github.com/BetaZay/Barista/tree/081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf) | `081ca2fe0a4caa11fb8e120319fc5aa1bd5380cf`; Nintendo hostap patches, registrar, runtime transport and clock code |
| [Hostap 2.11 snapshot](https://chromium.googlesource.com/external/w1.fi/cgit/hostap/+/d945ddd368085f255e68328f2d3b020ceea359af/) | `d945ddd368085f255e68328f2d3b020ceea359af`; AP handshake offload capability/PMK handling |
| [Linux brcmfmac](https://github.com/torvalds/linux/tree/v6.12/drivers/net/wireless/broadcom/brcm80211/brcmfmac) | `v6.12`; AP RSN parsing, standard key installation, vendor IE filtering and association events |
| [libdrc-streaming](https://github.com/ywh233/libdrc-streaming/tree/c4b9e0adff520d0f282232959ae41463aa3573f7) | `c4b9e0adff520d0f282232959ae41463aa3573f7`; partial Windows-encoder/Pi-proxy prior art with external radio |
| [Modified libdrc](https://github.com/ywh233/libdrc/tree/853c1659211ccabe7f0ff30ddda6d9283e70e111) | `853c1659211ccabe7f0ff30ddda6d9283e70e111`; TSF reads and media scheduling |

Research firmware target: `bcm43455c0/7_45_206`, SHA-256 `7fe369f7fdfded9eeceb8e438f7346e58634741b326d0715ad1b15275e6fa3a4`. This is not a claim about what is installed on the user's Pi. No firmware binary or upstream repository checkout is redistributed in this package.

## Protocol, platform and issue evidence

- [libdrc Wi-Fi peculiarities](https://libdrc.org/docs/re/wifi.html)
- [libdrc network setup](https://libdrc.org/docs/network.html)
- [libdrc video protocol](https://garyodernichts.github.io/libdrc/docs/re/sc-vstrm.html)
- [libdrc input protocol](https://garyodernichts.github.io/libdrc/docs/re/sc-input.html)
- [Linux Broadcom driver architecture](https://wireless.docs.kernel.org/en/latest/en/users/drivers/brcm80211.html)
- [Infineon Pi 4 TSF discussion](https://community.infineon.com/t5/Wi-Fi-Bluetooth-for-Linux/Request-wl-tool-amp-mfg-firmware-for-Raspberry-Pi-4B-running-64-bit-Linux/td-p/322601): production/debug command restriction on the releases discussed, not universal proof about every current image
- [Nexmon CSI command transport discussion](https://github.com/seemoo-lab/nexmon_csi/discussions/395): distinguish the demonstrated platform from Pi 4 validation
- [Barista Broadcom issue #2](https://github.com/BetaZay/Barista/issues/2): read the reopened investigation and later logs, not only the early FullMAC comment
- [Raspberry Pi USB gadget project](https://github.com/raspberrypi/rpi-usb-gadget)
- [Raspberry Pi gadget documentation/article](https://www.raspberrypi.com/news/usb-gadget-mode-in-raspberry-pi-os-ssh-over-usb/)

## Windows sources

See [WINDOWS_HISTORY.md](WINDOWS_HISTORY.md) for the exact WinRT, C#/WinRT, Embedded Mode, NDIS/WDI and VM references. The original diagnostic READMEs preserve their implementation sources.

## Original generated tool packages

The tools in this handoff were extracted from these verified earlier deliverables, without source changes:

| Artifact | SHA-256 |
| --- | --- |
| DrcLink-Probe-0.1.1-win-x64.zip | `2184c415c8dfae7281394fb61cef6c357808edbe31c7b16c432ae5e5db0e889f` |
| DrcLink-DriverBoundary-0.1.0.zip | `ed090af8c5571dfd33b57157bf9ece174d299e86de3966f13351496e3175072a` |

The packed historical runtime has its own license/notices. Research references are not a license to redistribute all upstream firmware or incorporate code without checking its license. Keep hashes and build recipes alongside future patches.
