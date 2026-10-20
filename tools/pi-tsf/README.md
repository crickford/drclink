# Pi TSF prototype

**Locally built ABI prototype, not hardware tested. The hardware reader is
disabled.** This is a separate, small Nexmon variant for the pinned
BCM43455c0 `7_45_206` research image. Query works in offline checks; reads return
`unverified_backend`. It is not yet a working hardware TSF accessor. See
[source verification](SOURCE_REVIEW.md), [ABI](ABI.md), and [validation](VALIDATION.md).

The missing device inventory, reader ROM instructions, live struct/clock-state
validation and AP clock-domain evidence prevent enabling a sound reader. There
is no compiler switch to bypass this. A later reviewed backend must check
pointers and verified clock state, call the verified reader, and advertise
`READ64` only after those source requirements are met. Capability advertisement
still would not imply a hardware-tested AP clock.

## Prepare and check

From the DrcLink repository root, on Linux with Git, Python 3 and GCC:

```bash
mkdir -p external
git clone --no-checkout https://github.com/seemoo-lab/nexmon.git external/nexmon
git -C external/nexmon checkout --detach 68720406f54ff16cad28fd3c51f2392aefad9fb7
python3 tools/pi-tsf/prepare.py external/nexmon
bash tools/pi-tsf/check.sh
```

`prepare.py` rejects another commit, modified tracked Nexmon inputs, a different
base-image hash, or an unexpected hook pointer. It stages only the new variant
under `patches/bcm43455c0/7_45_206/drclink_tsf`. It does not alter stock sources.
Existing differing variant files are preserved with an error: use a fresh
checkout or preserve/remove your previous variant explicitly before updating.

`nexmon-tsf.patch` contains the same new variant for review/application with
`git apply`; use either it or the preparer. Regenerate the patch with
`prepare.py external/nexmon --write-patch` after changing canonical `firmware/`
sources. The baseline `MANIFEST.json` inventories the original handoff only.

`check.sh` exercises the real firmware packet handler under AddressSanitizer and
UndefinedBehaviorSanitizer, plus Python parser/transport/analysis fixtures. In
this managed executor LeakSanitizer cannot operate under its process tracing;
run `ASAN_OPTIONS=detect_leaks=0 bash tools/pi-tsf/check.sh` here. Bounds and
undefined-behavior checks remain enabled. No allocations occur in the handler.
These tests do not emulate a radio.

## Build without deployment

Use the pinned bundled GCC 5.4.1 toolchain and Nexmon plugin. On a conventional
Linux x86_64 host able to execute 32-bit x86 programs, install its compatible
i386 runtime libraries and `gcc`, `make`, `gawk`, `qpdf`, `xxd`, and Python 3.
The pinned upstream README describes its legacy dependency requirements. A
native modern ARM compiler alone cannot replace the matching GCC plugin.

For Linux x86_64 environments without 32-bit executable support, the included
rootless runtime uses QEMU user emulation. Requires `dpkg-deb`, Python 3, native
GCC/make, and a recent glibc environment compatible with Debian 13 tools. It
downloads about 67 MB of fixed, checksum-verified Debian packages to an ignored
build directory; it installs nothing system-wide and preserves inherited proxies.

```bash
python3 tools/pi-tsf/bootstrap_toolchain.py build/tsf-toolchain external/nexmon
source build/tsf-toolchain/env.sh
bash tools/pi-tsf/build.sh external/nexmon
```

Alternatively, once dependencies are already available, simply run the final
`build.sh` command. This invokes only the flashpatch extractor, extraction for
one firmware target, and the variant's `firmware` target. It disables upstream
build-statistics submission. It does not invoke top-level builds for every
device, kernel-driver builds, install targets, module operations or services.
The variant also overrides the inherited install/backup targets to fail.

Outputs stay in the ignored checkout:

```text
external/nexmon/patches/bcm43455c0/7_45_206/drclink_tsf/
  brcmfmac43455-sdio.bin     built prototype; do not deploy yet
  gen/patch.elf             linked code/symbols
  gen/*.mk, gen/*.ld        generated patch placement
  log/                     compile/link/patch records
```

The variant uses Nexmon's existing ucode compression/reclaim and original
flashpatch relocation to provide patch space. It adds one ioctl hook and no
monitor-mode, frame-injection, console or UDP hooks. Thus even a command whose
semantics are read-only still requires a changed firmware image; this is not
a deployment-free clock access technique. Upstream sources and derived firmware
components retain Nexmon's GPL notices. Proprietary base/output firmware and
downloaded dependencies are never committed.

For clean rebuilds, source Nexmon's `setup_env.sh` first, then use
`make -C patches/bcm43455c0/7_45_206/drclink_tsf clean-firmware` from inside its
checkout. Reinvoke `build.sh` from DrcLink. Do not use `make install-firmware`.
Record compiler, Python/zlib and tool versions plus output hashes. The variant
does not embed wall-clock time or build counters; exact reproduction still
requires identical source/toolchain/compression inputs.

## Sampler (for a later approved hardware stage)

`tsf.py` uses only Python's standard library, generic netlink and the Broadcom
DCMD vendor command (`set=0`). It neither depends on Nexmon's legacy private
ioctl driver nor installs a driver. Its brcmfmac reply parsing follows the
pinned kernel vendor handler; live compatibility on the actual stock kernel
remains untested. Root/CAP_NET_ADMIN will normally be needed for vendor commands.

After deployment has been separately planned and verified, a bounded example
from the Pi repository root would be:

```bash
mkdir -p pairing-private/tsf
sudo python3 tools/pi-tsf/tsf.py sample --query-only \
  --inventory pairing-private/pi-inventory.txt \
  --firmware-image /path/to/exact-uncompressed-deployed-image.bin \
  --output pairing-private/tsf/query-001.jsonl
```

With this prototype, an ABI query would advertise zero capabilities. Sampling
without `--query-only` would refuse to start. After a verified backend exists,
omit that flag and use `--duration 30 --rate 10 --timeout 1`. All output files
are created exclusively; old runs are never overwritten. Three consecutive
read failures stop a run, retaining raw failed rows. Limits: duration 0.01–86400
seconds, rate 0.01–100 Hz, timeout 0.01–10 seconds, at most 250,000 samples.
One response deadline bounds netlink wait time; kernel request processing is
not cancellable merely because the userspace deadline expires.

Reanalyze raw logs without accessing hardware:

```bash
python3 tools/pi-tsf/tsf.py analyze pairing-private/tsf/run-001.jsonl
```

The sampler records CLOCK_MONOTONIC_RAW brackets, cookies, returned raw bytes,
status, provenance hashes and transport settings. Offset/rate fits use midpoint
anchors within segments. It flags backward values, repeats, possible 64-bit
wraps, failed reads and forward jumps. Backward values are not automatically
called resets or unwrapped as a 32-bit clock. Correlate with kernel/BSS event logs.
The default one-second forward-jump flag is an analysis heuristic, adjustable
with `analyze --jump-us`; it is not a GamePad tolerance. Short-run fitted drift
is especially sensitive to variable transport delay. No samples are filtered,
and the read-duration percentiles include userspace transport overhead.

For hardware readiness and recovery, start with [Pi setup](../../docs/PI_SETUP.md).
Clock access, authoritative Nintendo RSN, association, WPS, media and Windows
integration remain separate gates; none has been demonstrated here.
