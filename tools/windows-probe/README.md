# DrcLink.Probe 0.1.1

A Windows 11 x64 feasibility probe for connecting a physical Wii U GamePad
directly to the PC's built-in Intel AX210. This first prototype tests the Windows
application API's ability to host an **open** network and report connection events.

## What this experiment establishes

The laptop has already demonstrated a working 5 GHz Windows Mobile hotspot.
That confirms ordinary hosting. Console-free GamePad pairing also needs an open
pairing AP, Nintendo's modified WPS registrar, specific network advertisements,
and later modified WPA2 key derivation. This build tests only the first API gate.

The protected run is a control: it checks that our own process can publish a
legacy-client network and attach a phone through the WinRT API. The open run sets
the native `IWiFiDirectLegacySettings::put_Passphrase(NULL)` method directly and
records its raw HRESULT and the native getter result. Only when these confirm a
null setting does it attempt to start the network. Microsoft documents a dependence on embedded mode
and adapter support. **The program does not enable embedded mode or modify policy.**

Even if the publisher starts, check the actual band and authentication from
another device. A Started event or null readback is not over-the-air verification.

## Run the packaged build

1. Extract the entire ZIP to a writable folder on the Blade. Keep `app`, `runtime`,
   and `Run-Probe.cmd` together. No SDK or separate .NET installation is needed.
2. Turn **OFF Windows Mobile hotspot**. Leave Wi-Fi enabled. Mobile hotspot can
   preempt this API. Start with a normal terminal; elevation is only needed for the
   optional ETW script. If the API denies access, retain the exact logged error.
3. In PowerShell, change to the extracted directory and run:

   ```powershell
   .\Run-Probe.cmd protected
   ```

   Join `DrcLink-Probe` from a phone using the temporary password printed in the
   terminal. The run lasts 60 seconds; Ctrl+C requests cleanup. Expect
   `publisher-status` with `Started` and, if exposed by Windows, `connection-request`
   and `peer-attached`. The temporary password is regenerated each run and is not
   written to the log files.

4. After the first run stops, run:

   ```powershell
   .\Run-Probe.cmd open
   ```

   Look for **DrcLink-Open** on the phone. Record whether it exists, whether a lock
   icon/password prompt appears, whether the phone can join without a password,
   and the actual band/channel if the phone exposes them. If Windows rejects the
   null setting or aborts startup, the exact stage and error are the result we need.
   There is no silent fallback to a protected network.

   If you already ran 0.1.0 and the protected control worked, only repeat the open
   test with this version. Version 0.1.1 adds `nativeOpenProbe` and `failure` to
   summary.json; that single summary should now contain the needed diagnosis.

5. Each run creates a unique `logs\<timestamp>-<id>` directory. Bring back the two
   `summary.json` files and any `exception` or `publisher-status` lines from
   `events.jsonl`, plus the phone's observations. The read-only command
   `.\Run-Probe.cmd inspect` collects capabilities without publishing a network.

**Leave the GamePad out of these first tests.** There is no Nintendo WPS registrar
yet, and the probe uses diagnostic SSIDs rather than emulating a Wii U. No input
bridge, video/audio streaming, or actual GamePad pairing is implemented.

## Interpret the result

| Observation | Conclusion / next experiment |
| --- | --- |
| Protected control fails | Investigate WinRT activation, driver, competing hotspot, or permissions before interpreting an open-mode failure. |
| Native setter returns a failing HRESULT | The native Windows method rejected the literal null argument in this environment. This identifies the layer, not the underlying policy/driver/hardware cause. |
| Result is `native-interop-probe-failed` | The diagnostic bridge itself failed. Inspect the included exception and stack trace before drawing conclusions about Windows. |
| Null readback succeeds; publisher aborts | Configuration was accepted but startup failed. Preserve the enum/error and compare with the control. |
| Open publisher starts but phone sees a locked network | The requested security setting was not established over the air. Do not treat it as an open-network success. |
| Phone confirms an open network on 2.4 GHz | Open hosting works on that band. We still need to establish 5 GHz open hosting. |
| Phone confirms an open 5 GHz network | Next gates: Wii U pairing advertisement/BSSID details, Nintendo WPS registrar and packet access, then normal-mode RSN identifiers and rotated PTK. |
| No connection callback | The API did not expose a request. It does not prove the GamePad or another client transmitted no frames. |

This public WinRT API does not expose a documented band selector, raw RSN
replacement, or Nintendo key-derivation callback. The probe does not pretend to
exercise those controls. The Windows-selected band may differ from the band chosen
in Mobile hotspot settings. The tool also cannot select a particular adapter if
several Wi-Fi radios are installed; use it with the laptop's existing single card.

## Logging and lifecycle

- `events.jsonl`: UTC and elapsed timestamps, exact exception HRESULTs, publisher
  states/errors, and hashed peer identifiers. These are API events, not raw radio
  packets and not a latency benchmark.
- `summary.json`: requested mode, native query/set/get HRESULTs, exception detail,
  start-attempt flag, last stage/status/error, whether Started occurred,
  and connection counts. Authentication/band remain unverified until checked by a
  second device. `gamePadPaired` is always false in this prototype.
- `environment.json`, `drivers.txt`, `wirelesscapabilities.txt`: runtime and driver
  baseline. No Wi-Fi profile export, saved-password extraction, or nearby-network
  scan is performed. netsh output is retained in the local system's text encoding.
- Only this process's publisher and peer objects are stopped/disposed. No driver,
  service, firewall, registry, or embedded-mode settings are changed. Stop is skipped
  when configuration fails before any Start attempt. Normal exit
  and Ctrl+C request cleanup; forcibly closing/killing the process prevents a
  cleanup report. Windows owns the underlying adapter lifecycle.

Exit codes: 0 observation completed (not pairing success), 2 invalid arguments,
3 unsupported OS, 10 API operation failed, 11 native open configuration rejected/not
confirmed, 12 publisher ended before starting, 13 startup timed out, 14 publisher
ended during observation, 16 cleanup could not be confirmed, 20 fatal error,
130 cancelled.

The process accepts legacy client requests reported by Windows during the short
test. Requests are handled sequentially, with a bounded queue, per-request timeout,
and retained peer lifetimes. No application service is exposed to peers.

## Optional Windows event trace

If the summary is ambiguous, run `scripts\Capture-WlanTrace.ps1` in an
Administrator PowerShell terminal, then run the probe in a second terminal during
its 60-second window. It records the WLAN ETW scenario with `capture=no`, a 128 MB
circular limit, and no persistent tracing. It stops the trace only if its own
start succeeded. ETW still may not reveal every unassociated or failed-radio
request. Logs stay local; nothing is uploaded automatically. If the trace window
is forcibly closed, use `netsh trace stop` in an Administrator terminal.

## Build from source

Install the .NET 8 SDK on Windows (the SDK restores the Windows reference package),
then from this directory:

```powershell
dotnet build .\src\DrcLink.Probe\DrcLink.Probe.csproj -c Release
dotnet run --project .\tests\DrcLink.Probe.Checks -c Release
dotnet run --project .\src\DrcLink.Probe -c Release -- probe --mode protected
dotnet run --project .\src\DrcLink.Probe -c Release -- probe --mode open
```

`scripts\Build.ps1` publishes a conventional self-contained Windows x64 executable
to `rebuilt`. It does not replace the bundled portable-runtime build automatically.
All options are documented by `runtime\dotnet.exe app\DrcLink.Probe.dll --help`.

## Sources and remaining work

- [libdrc: Wi-Fi peculiarities](https://libdrc.org/docs/re/wifi.html): open pairing
  AP, modified WPS, Nintendo RSN identifiers and modified WPA2 key derivation.
- [Microsoft: legacy passphrase](https://learn.microsoft.com/en-us/uwp/api/windows.devices.wifidirect.wifidirectlegacysettings.passphrase):
  null passphrase/open authentication and its documented restrictions.
- [Microsoft: desktop Wi-Fi Direct](https://learn.microsoft.com/en-us/windows-hardware/drivers/partnerapps/wi-fi-direct)
  and [legacy settings](https://learn.microsoft.com/en-us/uwp/api/windows.devices.wifidirect.wifidirectlegacysettings):
  desktop hosting API and Mobile hotspot interaction.
- [Microsoft's desktop sample](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/WiFiDirectLegacyAP)
  and [C# example](https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/WiFiDirect/cs/Scenario1_Advertiser.xaml.cs):
  publisher/listener/peer object lifecycle.
- [WDI AP startup](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/oid-wdi-task-start-ap)
  and [cipher-key installation](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/oid-wdi-set-add-cipher-keys):
  potential driver research, not application calls implemented by this tool.

Further work depends on observed results. A generic vendor IE is not the same as
rewriting the RSN security element. A generic packet filter is not automatically
able to alter the Intel/WDI boundary. A Linux VM does not by itself gain ownership
of the built-in PCIe radio. None of those capabilities is established by this build.

## Why 0.1.1 calls the ABI directly

The original open run produced System.NullReferenceException with HRESULT
0x80004003. C#/WinRT maps a native E_POINTER to that exception type. Without a
stack trace or a raw HRESULT at the native boundary, the previous report could
not distinguish a projection error from native rejection. Version 0.1.1 obtains
the underlying native object using C#/WinRT's documented IWinRTObject interface,
queries IID A64FDBBA-F2FD-4567-A91B-F5C2F5321057, and calls the Passphrase setter
with a literal null pointer. It performs no exception translation on that HRESULT.

The interface's setter/getter slots (11 and 10) and IID were checked against
Microsoft.Windows.SDK.NET.Ref 10.0.19041.56. Unmanaged test doubles exercise the
exact function-pointer calls and null argument on Linux; they do not establish
Windows behaviour. A native success still needs readback, publication, and phone
verification. A native failure does not tell us by itself whether embedded-mode
policy, implementation validation, or adapter support caused it.

[Microsoft C#/WinRT exception mapping](https://github.com/microsoft/CsWinRT/blob/master/src/WinRT.Runtime/ExceptionHelpers.cs)
and [native-object interop](https://github.com/microsoft/CsWinRT/blob/master/docs/interop.md).
