# DrcLink: Windows driver boundary investigation

Prepared 2026-10-15 for Windows 11 Home build 26200, Intel AX210, driver 22.80.1.1.

**Finding:** There is a documented candidate observation path at the Native WLAN request layer. There is not yet a demonstrated method to take over GamePad pairing on this Windows/Intel stack. The next experiment records an ordinary protected hotspot and a phone joining it, using Windows' existing WDI and NDIS trace providers. No custom driver is included or installed.

## Run the next experiment

1. Extract this folder. Keep the already-extracted **DrcLink-Probe-0.1.1** folder; this collector uses its bundled runtime and executable. No SDK installation is required.
2. Turn Windows Mobile Hotspot **off**, leave Wi-Fi **on**, and have a phone ready.
3. Open **PowerShell as Administrator**, change to this folder, and run the following, replacing the probe path with your actual extracted folder. Point to the folder containing `app` and `runtime`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Collect-WdiBoundary.ps1 -ProbeRoot "C:\path\to\DrcLink-Probe-0.1.1-win-x64"
```

`-ExecutionPolicy Bypass` applies to this launched process. The collector prompts before recording, then launches the normal protected probe for 90 seconds. Join **DrcLink-Probe** from your phone using the password printed by the probe. No GamePad interaction is needed. Leave the terminal open until recording stops.

Results go into `results\boundary-<timestamp>-<id>`. Send these small files first:

- `collector-summary.json`
- `wdi-symbol-identity.json`, if present
- `probe\summary.json`

If startup fails, send `trace-start.txt` too. Keep `boundary.etl` for the next decoding step. The collector does not upload anything. Packet capture is disabled; debug trace contents can still include network identifiers and security-related diagnostic data.

The collector starts only its own trace and attempts to stop it in `finally`. If another `netsh trace` session is running, startup should fail and that session is left alone. A forced process kill or closing the terminal can prevent cleanup; in that case stop the collector's session with `netsh trace stop`. If trace startup or stop fails, the collector records the exit code and reports failure. AP band is still selected by Windows; the collector makes no 5 GHz claim.

## What the source review establishes

Microsoft's [Native WLAN-to-WDI mapping](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/wdi-oid-to-wdi-message-mapping) documents these translations. Microsoft warns that the mapping is state-dependent, so this is a candidate path rather than proof that every operation crosses a filter on this machine.

| Operation | Native WLAN request | WDI counterpart |
| --- | --- | --- |
| Start a Group Owner | `OID_DOT11_WFD_START_GO_REQUEST` | `OID_WDI_TASK_START_AP` |
| Install a peer key | `OID_DOT11_CIPHER_KEY_MAPPING_KEY` | `OID_WDI_SET_ADD_CIPHER_KEYS` |
| Install a default/group key | `OID_DOT11_CIPHER_DEFAULT_KEY` | `OID_WDI_SET_ADD_CIPHER_KEYS` |
| Add Wi-Fi Direct advertisement IEs | `OID_DOT11_WFD_ADDITIONAL_IE` | `OID_WDI_SET_ADVERTISEMENT_INFORMATION` |
| Enable WPS | `OID_DOT11_WPS_ENABLED` | Handled by Microsoft |

An [NDIS lightweight filter can observe and forward requests from overlying drivers](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/filtering-oid-requests-in-an-ndis-filter-driver). This makes the Native WLAN side a plausible place for an observation prototype. It does **not** imply that an ordinary filter receives the internal WDI commands. The [WDI initialization documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/wdi-ihv-driver-interfaces) explains that the Microsoft component substitutes miniport handlers during registration and calls the vendor handler for WDI messages.

The existing `IntelIHVRouter08.dll` entry is also not proof of usable controls. [WlanIhvControl](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanihvcontrol) accepts a vendor-defined payload. No documented Intel payload for the required operations was established in this review. Guessing payloads is not the next experiment.

## The difficult requirements remain separate

The [libdrc protocol documentation](https://libdrc.org/docs/re/wifi.html) describes open-network discovery, a modified WPS key derivation for pairing, Nintendo cipher/AKM selectors in normal WPA2 operation, and a three-byte PTK rotation. A usable implementation must satisfy the entire sequence.

| Requirement | Evidence available | What remains unproven |
| --- | --- | --- |
| Open AP on a permitted 5 GHz channel | WDI AP-start parameters include auth/cipher lists and band/channel information | Whether a supported attachment point can request this on AX210 despite the WinRT setter rejection |
| Nintendo WPS implementation | Protocol is reverse engineered | Whether EAPOL/WPS can be delivered to our implementation while Windows' WPS engine is excluded |
| Nintendo RSN selectors in beacons/probe responses | WDI supports additional advertisement IEs | Whether the authoritative RSN element can be replaced or suppressed; appending an IE is not equivalent |
| Correct peer key installation | Documented Native WLAN and WDI key commands | Whether our chosen attachment point sees/controls the AP peer's key installation and can provide the correct key |
| Stable streaming and timing | Existing direct Linux implementations provide prior art | No transport, latency, timing or encoder tests have run on this Windows route |

The [advertisement command](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/oid-wdi-set-advertisement-information) and [additional IE TLV](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/wdi-tlv-additional-ies) describe adding elements, not arbitrary replacement of firmware-generated security fields. Two RSN elements or a cosmetic Nintendo vendor IE do not establish compatibility. Likewise, a temporal key alone is not the complete PTK; rotating whatever bytes appear in an installation request is not a correct implementation of the Nintendo derivation.

The official [Realtek WDI sample](https://github.com/microsoft/Windows-driver-samples/tree/main/network/wlan/WDI) was also inspected: `WDI_OID_TASK_START_AP` in `PLATFORM/NDIS6/WDI_Cmds.c` translates auth/cipher settings into its own AP setup; `COMMON/MgntConstructPacket.c` builds security elements and appends additional IEs separately. This illustrates why the vendor implementation matters. It is **not** AX210 source or evidence of Intel behavior. The generic NDIS filter sample is not an immediately usable wireless-control driver either.

## Why trace before writing a driver

Microsoft documents [WDILib WPP tracing](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/wdi-tracing-with-wdilib), including provider `{21ba7b61-05f8-41f1-9048-c09493dcfe38}`. The collector also enables the NDIS provider identified in Microsoft's [NDIS tracing example](https://learn.microsoft.com/en-us/windows-hardware/test/hlk/testref/qosremotesettings), with a broader keyword mask to look for request-routing context. The [Windows wireless troubleshooting guide](https://learn.microsoft.com/en-us/troubleshoot/windows-client/networking/wireless-network-connectivity-issues-troubleshooting) confirms that `wireless_dbg` remains available even when not listed by `show scenarios`.

WPP records require matching formatting data. Plain `Get-WinEvent` or ETL-to-text conversion may leave the crucial messages undecoded. The collector records the installed `wdiwifi.sys` CodeView PDB name, GUID and age so that matching Microsoft symbols can be sought. Public PDB availability and sufficient WPP formatting information are not guaranteed. It records identity metadata only; it does not download symbols or copy the Windows binary.

After collecting, decode with matching symbols/TMFs and look for AP creation/start, association, and key-install operations correlated by timestamps and port identifiers. Distinguish the phone-facing GO port from the PC's existing infrastructure connection. General station key events are not evidence of control over the GO peer.

| Observation | Decision |
| --- | --- |
| Trace starts, probe starts, phone attaches, and relevant events decode | Use the command sequence to design a narrowly scoped observation-only filter experiment |
| WDI events exist but Native WLAN routing is not visible | Do not infer filter access; test that attachment point separately |
| ETL has data but events cannot be decoded | Resolve symbols/provider coverage before interpreting missing events |
| Events are missing after decode | Check trace loss, keywords and port selection; absence does not prove hardware incapability |
| Filter later sees key requests but cannot control security IEs or WPS | Key visibility alone is insufficient; reassess the Windows-only route |

A future filter should initially log only request type, OID, port, length and completion status, without copying key buffers or modifying requests. Scope binding to the target adapter and its relevant virtual interfaces, preserve cancellation/completion semantics, and prove clean unbinding. Developing and loading that kernel driver would require a Windows WDK build and an appropriate signing/test setup. This package deliberately stops at built-in tracing because that is the next unresolved observation.

## Validation and limits

This package contains a PowerShell collector and a small PE symbol-identity reader. Validation performed in the preparation environment is recorded in `VALIDATION.md`. Windows tracing and AX210 operation cannot be exercised in this Linux environment. A successful collection would establish observability only, not GamePad compatibility, open hosting, filter interception, modified WPA2 support or acceptable latency.
