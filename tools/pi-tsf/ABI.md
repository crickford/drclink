# DrcLink TSF ABI 1

Local experimental firmware command **430**, GET only in the host tool. No
named Nexmon definition or explicit ioctl switch case for 430 was found in
the pinned patches/utilities audit. This is not a globally reserved allocation
and does not prove the stock ROM has no command 430. The variant intercepts
that number deliberately; do not probe this number on unrelated stock images.
There is no arbitrary memory/register interface, clock write or state change.

All integers are unsigned little-endian, serialized as bytes; no C struct is
sent. Request and response are exactly **80 bytes**, usable unaligned. Wrong
buffer size or null pointer returns firmware error `-2`, touching no bytes.

| Offset | Size | Request | Response |
| --- | --- | --- | --- |
| 0 | 4 | ASCII `DTSF` | ASCII `DTSF` |
| 4 | 4 | ABI `1` | ABI `1` |
| 8 | 4 | Size `80` | Size `80` |
| 12 | 4 | Operation: query `0`, read `1` | Echo operation |
| 16 | 4 | Zero | Status |
| 20 | 4 | Zero | Capabilities: bit 0 `READ64`; other bits zero |
| 24 | 4 | Zero | TSF low word, microseconds |
| 28 | 4 | Zero | TSF high word |
| 32 | 4 | Zero | Research-target ID `0x043455c0` |
| 36 | 4 | Zero | Domain `0`: unverified hardware/AP domain |
| 40 | 32 | All zero | Raw 32-byte SHA-256 of pinned **base** image |
| 72 | 8 | Caller cookie | Echo cookie |

Status: `0=OK`, `1=BAD_REQUEST`, `2=UNVERIFIED_BACKEND`, `3=CLOCK_DOWN`,
`4=READ_FAILED`. Correct-size packets with wrong magic/version/size/op or any
nonzero reserved request byte return a zero-initialized response with
`BAD_REQUEST` and do not invoke the reader. Firmware transport returns success
for such protocol-level responses; the host must inspect status. Query never
calls the reader. Timestamp words are zero on every query/failure. A zero TSF
on a successful future read is a legitimate integer, not a failure sentinel.

The checked-in backend advertises `caps=0` and READ returns status `2`, without
dereferencing firmware structures or calling a ROM function. The base hash and
chip ID identify the **build target**, not a runtime measurement of the board
or an attestation of the loaded image. Record the exact patched-file hash and
independently reconcile it with firmware-loading evidence. No reboot/BSS epoch
is provided by ABI 1; a decreasing clock can only be flagged as a discontinuity.
Do not invent epoch data from host timestamps. Any future change to clock-domain
values or wire semantics needs matching host support and an ABI decision.
