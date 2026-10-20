# Windows/AX210 investigation history

This is background for a retained research branch. The latest continuation is the Pi firmware milestone.

## Baseline supplied by the user

The laptop reports Intel Wi-Fi 6E AX210 160MHz, driver 22.80.1.1 dated 2021-09-12. `netsh` reports Hosted Network, Soft AP, monitor and promiscuous modes unsupported, but Wi-Fi Direct device/GO/client and GO on 5 GHz supported. The user successfully created a 5 GHz Windows Mobile Hotspot and joined it from another device. Those capability labels do not contradict this observation: legacy Hosted Network/SoftAP and Wi-Fi Direct hosting are different paths.

`IntelIHVRouter08.dll` is configured. Infrastructure vendor-defined cipher/auth entries and an IHV extension do not prove a custom GO security implementation or a documented usable Intel command. Reported zero radio/concurrent-channel fields were not interpreted as physical absence of a radio.

## API probe sequence

### Protected control, 0.1.0

The probe created a normal protected legacy-client Wi-Fi Direct GO. User uploaded a result with `apiReportedStarted=true`, `connectionRequestCount=1`, `attachedPeerCount=1`, exit code 0 and clean terminal cleanup. The probe did not measure the band or verify over-the-air authentication. This is ordinary API publication/phone-attachment success, not GamePad pairing.

### Open request, 0.1.0

Setting the projected C# passphrase property to null produced `System.NullReferenceException`, HRESULT `0x80004003`. Calling Stop on a publisher never started also produced `0x8000000E`. Because C#/WinRT translates native `E_POINTER` into `NullReferenceException`, the initial exception alone did not reveal whether the wrapper or the native method rejected null.

### Native ABI check, 0.1.1

The revised probe obtains `IWinRTObject.NativeObject`, queries IID `A64FDBBA-F2FD-4567-A91B-F5C2F5321057`, and invokes the interface setter with a literal null pointer. SDK metadata/IL checks established getter slot 10 and setter slot 11. The supplied result is:

```text
QueryInterfaceHresult = 0x00000000
SetterWasInvoked      = true
SetterHresult         = 0x80004003
startWasAttempted     = false
lastPublisherStatus   = Created
cleanupStatus         = not-needed-never-started
result                = open-native-setter-rejected
```

This confirms native method rejection in this environment. It does not identify the underlying policy/driver/hardware cause. The 0.1.1 cleanup change avoided reporting an attempted stop on an unstarted publisher.

## Embedded Mode detour

Microsoft's passphrase documentation conditions null/open authentication on Embedded Mode configuration and adapter support. The Embedded Mode overview names IoT Enterprise; the `AllowEmbeddedMode` policy applicability table includes Pro/Enterprise/Education/IoT, not Home. Treat those as documented support statements, not proof of this HRESULT's cause and not evidence that buying an edition upgrade solves Nintendo interoperability.

The user confirmed Windows 11 Home build 26200 and service `embeddedmode`, stopped/manual. A suggested service-start test failed with a generic message. Subsequent `sc.exe query embeddedmode` reported stopped, both stored exit codes zero. Starting a service alone is not configuring Embedded Mode policy. **The detour did not establish a cause and should not be resumed by default.** No registry/provisioning policy change was performed.

## Driver-boundary route

Microsoft documents these candidate mappings:

| Native WLAN side | WDI side |
| --- | --- |
| `OID_DOT11_WFD_START_GO_REQUEST` | `OID_WDI_TASK_START_AP` |
| `OID_DOT11_CIPHER_KEY_MAPPING_KEY` | `OID_WDI_SET_ADD_CIPHER_KEYS` |
| `OID_DOT11_CIPHER_DEFAULT_KEY` | `OID_WDI_SET_ADD_CIPHER_KEYS` |
| `OID_DOT11_WFD_ADDITIONAL_IE` | `OID_WDI_SET_ADVERTISEMENT_INFORMATION` |

NDIS filter callbacks can observe requests from overlying drivers, suggesting a Native WLAN observation point. This does not prove a standard filter sees internal WDI calls; the Microsoft WDI component substitutes miniport handlers and invokes the vendor handler. No custom driver, filter attachment or security control was implemented.

The packaged WDI/NDIS collector uses Windows' existing trace providers around a protected probe/phone connection. It collects matching CodeView symbol identity because WPP decoding may need PDB-derived TMFs. An ETL containing bytes does not prove the required events are present or decodable. No output from this collector has been received. It does not pair the GamePad or change the Intel driver.

## Linux and VM observations from earlier research

Normal Windows VM/WSL networking does not hand the built-in AX210 PCIe radio to Linux. A supported passthrough route on this laptop/Windows edition has not been demonstrated. The Bluetooth USB function is not the Wi-Fi radio. Supported Hyper-V DDA documentation is server-oriented; unofficial workarounds are not an established project solution.

Ordinary Intel Linux 5 GHz AP restrictions were identified in earlier iwlwifi documentation. Some P2P-GO workarounds exist in other projects, but they were not tested on this AX210 or with a real GamePad. Do not turn the old restriction into a universal impossibility claim. WSL2 with a suitable USB radio/custom kernel is a different path; it is not categorically impossible, but introduces hardware/setup outside the current onboard-radio Pi target.

## References

- [WinRT passphrase and open authentication](https://learn.microsoft.com/en-us/uwp/api/windows.devices.wifidirect.wifidirectlegacysettings.passphrase)
- [Embedded Mode overview](https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/os-features/embedded-mode)
- [AllowEmbeddedMode policy](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-system#allowembeddedmode)
- [C#/WinRT exception mapping](https://github.com/microsoft/CsWinRT/blob/master/src/WinRT.Runtime/ExceptionHelpers.cs)
- [C#/WinRT native interop](https://github.com/microsoft/CsWinRT/blob/master/docs/interop.md)
- [Native WLAN-to-WDI mapping](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/wdi-oid-to-wdi-message-mapping)
- [WDI driver interfaces](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/wdi-ihv-driver-interfaces)
- [NDIS filtering of OID requests](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/filtering-oid-requests-in-an-ndis-filter-driver)
- [WDI tracing](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/wdi-tracing-with-wdilib)
- [iwlwifi documentation](https://wireless.docs.kernel.org/en/latest/en/users/drivers/iwlwifi.html)
- [Hyper-V DDA](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/deploy/deploying-graphics-devices-using-dda)

The statements above record the earlier investigation. Recheck live platform support only if reviving that branch; it is not a reason to repeat completed Windows measurements during the Pi work.
