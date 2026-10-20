# User-reported hardware and service observations

These are selected values transcribed from the user's pasted terminal output and statements, not fresh measurements made for this handoff.

## Windows wireless baseline

```text
Interface name: Wi-Fi
Driver: Intel(R) Wi-Fi 6E AX210 160MHz
Vendor/Provider: Intel Corporation / Intel
Driver date: 9/12/2021
Driver version: 22.80.1.1
INF: oem29.inf
Type: Native Wi-Fi Driver
Hosted network supported: No
Bands: 2.4 GHz, 5 GHz, 6 GHz
IHV adapter OUI: [00 00 00], type [00]
IHV extensibility DLL: C:\WINDOWS\system32\IntelIHVRouter08.dll

WDI Version (Windows): 0.1.1.13
WDI Version (IHV): 0.1.1.12
WiFiCx: Not supported
Firmware Version: 34.0
Station: Supported
Soft AP: Not supported
Network monitor mode: Not supported
Promiscuous mode: Not supported
Wi-Fi Direct Device / GO / Client: Supported
P2P GO on 5 GHz: Supported
P2P GO on 6 GHz: Not supported
Tx/Rx spatial streams: 2 / 2
Number of Concurrent Channels Supported: 0
Number of Radios: 0
```

The user reported: “Was able to create and connect to 5ghz hotspot.” This was a normal Windows Mobile Hotspot observation. It is not a measurement of the separate WinRT probe's chosen band. The zero-valued radio/capability fields above were not treated as absence of physical radio hardware.

## OS and Embedded Mode

```text
Caption                   Version    BuildNumber
Microsoft Windows 11 Home 10.0.26200 26200

Name          Status  StartType
embeddedmode  Stopped Manual
```

Attempted `Start-Service` returned:

```text
Start-Service: Failed to start service 'Embedded Mode (embeddedmode)'.
```

Subsequent service query returned:

```text
SERVICE_NAME: embeddedmode
        TYPE               : 30  WIN32
        STATE              : 1  STOPPED
        WIN32_EXIT_CODE    : 0  (0x0)
        SERVICE_EXIT_CODE  : 0  (0x0)
        CHECKPOINT         : 0x0
        WAIT_HINT          : 0x0
```

A standalone `2` preceded the pasted service query in the conversation. Its meaning was not established and it is not interpreted as the service's error code. Both reported stored exit-code fields were zero. No policy/registry change was made, and these observations did not identify why the native open-network setter rejected null.

## Pi information not yet supplied

The user proposed a Raspberry Pi 4 Model B as a USB-connected radio bridge. No output identifying its actual chip revision, Raspberry Pi OS version, kernel, brcmfmac/firmware revision, firmware hash, country/channel configuration or AP behavior has been supplied. Do not substitute the research firmware target for this missing inventory.
