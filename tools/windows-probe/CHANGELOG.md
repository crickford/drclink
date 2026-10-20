# Changes

## 0.1.1

- Replace the projected C# null-passphrase assignment with a direct call to the
  documented WinRT interface ABI. Query the exact interface IID, pass a literal
  null pointer, and preserve the native setter/getter HRESULTs.
- Include the native results, start-attempt flag, and main exception details in
  summary.json; include stack traces in events.jsonl.
- Skip Stop when configuration failed before any Start attempt. Treat already
  terminal publishers separately, avoiding the misleading cleanup failure seen
  in 0.1.0.
- Add unmanaged test doubles that verify null arguments, native HRESULT retention,
  readback, and cleanup decisions.

The original 0.1.0 result (System.NullReferenceException, 0x80004003) is compatible
with either a managed failure or a native E_POINTER mapped by C#/WinRT. It does
not, by itself, identify which layer rejected the argument. The new test narrows
that question. If the native setter also returns E_POINTER, it confirms native
API rejection in this environment; it still does not establish the underlying
policy, driver, or hardware cause.
