# TSF prototype validation — 19 October 2026

All results below are **locally tested** or **source-supported**, not
hardware-observed. No Pi connection/inventory has been received. No firmware,
driver or service was installed, loaded or restarted. The reader is disabled;
this is a built ABI prototype, not a successful hardware TSF accessor.

## Environment and reproducible commands

Managed Linux x86_64, Debian 13, native GCC `14.2.0-19`, GNU Make 4.4.1,
Python 3.12.14, Python zlib compile/runtime 1.3.2. Pinned Nexmon GCC:
`arm-none-eabi-gcc 5.4.1 20160609`, bundled GCC plugin. QEMU i386 7.2.22
(`7.2+dfsg-7+deb12u18+b3` Debian package), GNU Awk 5.3.2, xxd 2024-12-07.
Runtime dependencies and exact SHA-256 pins are in `bootstrap_toolchain.py`.

The executor could not natively execute the 32-bit bundled compiler
(`Exec format error`), so it was run under QEMU, including compiler subprocesses
and binutils. No host library paths or system packages were changed. The optional
rootless bootstrap was exercised using checksum-verified cached Debian archives.
Its generated environment then reproduced the build from clean variant outputs.

From the DrcLink root after the pinned checkout exists:

```bash
python3 tools/pi-tsf/prepare.py external/nexmon --write-patch
python3 tools/pi-tsf/bootstrap_toolchain.py build/tsf-toolchain external/nexmon
source build/tsf-toolchain/env.sh
bash tools/pi-tsf/build.sh external/nexmon
python3 tools/pi-tsf/verify_artifact.py external/nexmon \
  --nm build/tsf-toolchain/bin/arm-none-eabi-nm
ASAN_OPTIONS=detect_leaks=0 bash tools/pi-tsf/check.sh
```

The first sanitizer invocation without that environment setting failed because
LeakSanitizer cannot run under the executor's process tracing. Retried with
leak detection disabled: AddressSanitizer and UndefinedBehaviorSanitizer passed.
The tested firmware packet handler does not allocate memory.

## Results

- Native C handler checks pass under address/undefined-behavior sanitizers:
  invalid/null/short/oversized/unaligned buffers, byte-level reserved-field
  rejection, cookie preservation, no reader invocation on query/invalid/disabled
  paths, explicit high/low golden bytes, zero timestamp words after partial
  backend failure and surrounding canaries.
- 16 Python tests pass: strict packet/header/hash/cookie/capability/error checks;
  malformed/duplicate/padded netlink attributes, nested brcmfmac response fixtures,
  ACK/reply ordering and timeout/error propagation; monotonic full-64-bit low-word
  rollover, zero-extended-32-bit backward values, possible 64-bit wrap, reset/repeat/
  jump/failure flags, host-clock validation and a synthetic 10 ppm rate fit;
  no sampling with disabled capabilities, existing-log preservation and bounded
  failure stop retaining malformed raw responses.
- Firmware C is compiled with the pinned ARM compiler, not only a native mock.
  Struct-offset assertions pass for the upstream declarations, without proving
  live correctness. The source uses no unchecked guessed clock field.
- Firmware build/link/patch succeeds: 624,943-byte output. Two clean builds
  (the second using the checked-in rootless bootstrap's wrappers) produce the
  same image hash. No bit-for-bit promise is made across different zlib/toolchain
  versions. The stock base hash is preserved separately.
- Final hook value `0x22c889` resolves to `drclink_ioctl` at `0x22c888` plus the
  Thumb bit, inside the declared patch region. The disabled TSF reader is absent
  from linked symbols; monitor/injection/UDP helpers checked by the artifact
  verifier are also absent. Linker region checks pass.
- Compiled local Linux header constants match the sampler:
  `NL80211_CMD_VENDOR=103`, `IFINDEX=3`, `VENDOR_ID=195`,
  `VENDOR_SUBCMD=196`, `VENDOR_DATA=197`.
- The generated patch is checked/applied in a fresh temporary Git repository
  and its files compared byte-for-byte with the staged canonical variant.
- Shell syntax and repository whitespace checks pass. No Windows radio/service
  experiment was repeated.

Output image SHA-256:

```text
4853ea4df6356115dd45085349d18ead96d735937df9c43b0b8a5a228f787d03
```

Patch SHA-256:

```text
1b5035ac121ffb90026a8a98fd2427ecc2ee6ce9e29e1140223ebde3ead18d6a
```

Records: [artifact/source audit](../../evidence/pi-tsf-offline-2026-10-19/artifact.json),
[offline checks](../../evidence/pi-tsf-offline-2026-10-19/checks.txt),
[build log](../../evidence/pi-tsf-offline-2026-10-19/build.txt). Build-log ANSI
color codes and trailing whitespace were removed and the absolute checkout path replaced with
`<NEXMON_ROOT>`; commands/results otherwise retained. Firmware, upstream source
trees and downloaded runtime binaries remain ignored and uncommitted.

## What remains untested/unresolved

The actual Pi target is not selected. Need board/chip revision, stock OS/kernel/
driver and loaded firmware/file-selection evidence, NVRAM/CLM hashes, regulatory
capabilities and an independent recovery baseline. Need a clean ROM/source
verification of the reader, layout/pointer chain and clock-enabled predicate.
No AP TSF identity, read latency/jitter, drift, reset behavior, transport/kernel
compatibility or firmware stability has been observed on hardware. The new
backend must remain unavailable until its source assumptions are verified.

Deployment is a separate later step after exact identity, backups and recovery
are reviewed. Subsequent hardware tests should retain raw samples under idle and
load, correlate kernel/BSS restarts, and establish the AP clock domain; ordinary
stock AP testing and USB Ethernet setup also need their own explicit results.
No RSN advertisement, association, WPS, GamePad pairing or streaming is validated
by this prototype.
