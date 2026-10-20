# Validation

Prepared on Linux; Windows/AX210 execution is pending.

- `Collect-WdiBoundary.ps1` parsed with the official PowerShell 7.4.6 parser: **zero syntax errors**. Windows PowerShell 5.1 compatibility was reviewed, but not executed.
- `SymbolIdentity.cs` compiled with Roslyn against .NET 8 with warnings treated as errors: **zero warnings/errors**.
- PE symbol extraction passed fixtures for PE32+, PE32, GUID/age/symbol-key formatting, and a PE with no debug information.
- Truncated data, invalid signatures, and a debug RVA outside file-backed sections were rejected.
- Extraction on Microsoft's Windows `dotnet.exe` and `hostfxr.dll` matched .NET's independent `PEReader` CodeView GUID/age results (one entry each).
- Collector control flow was reviewed: trace ownership is established only after successful startup; cleanup is in `finally`; the probe is not launched if trace startup fails; symbol-metadata failure does not prevent tracing; stop failure is reported. These paths have not been exercised against Windows services.

No custom kernel driver was built, loaded, signed, or tested. No Windows policy, radio settings, driver configuration, or firmware was changed in the preparation environment. Source review and syntax checks do not establish runtime behavior. The next hardware run must verify trace startup/stop and phone attachment before the ETL is interpreted.
