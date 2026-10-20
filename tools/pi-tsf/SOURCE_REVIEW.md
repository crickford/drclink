# Pinned source verification

Source-supported findings from Nexmon commit
`68720406f54ff16cad28fd3c51f2392aefad9fb7`, inspected on 19 October 2026
(America/Los_Angeles). These are independent of hardware compatibility.

| Item | Verified in source/binary | Limit |
| --- | --- | --- |
| Base image | 624,943 bytes, SHA-256 `7fe369f7fdfded9eeceb8e438f7346e58634741b326d0715ad1b15275e6fa3a4` | Not the known image on the user's Pi |
| RAM map | `RAMSTART=0x198000`, declared size `0xc8000` | The file is smaller than the declared RAM address space |
| Reader wrapper | `patches/common/wrapper.c`: `wlc_bmac_read_tsf(void *, unsigned int *, unsigned int *)`, BCM43455c0, `FW_VER_ALL`, ROM `0x9c76c` | Metadata only; ROM body not present/verified |
| Original dispatch hook | `nexmon/src/ioctl.c` patches four bytes at `0x20e264`; base bytes there are `b9 03 02 00` = Thumb pointer `0x203b9` | Matches `wlc_ioctl` wrapper at ROM `0x203b8`; live calling convention still untested |
| Layout include | Target `structs.h` includes `../structs.common.h` | Shared file warns structures copied from BCM4339 may be wrong |
| Layout declarations | `wlc_info.hw=0x10`, `wlc_hw_info.regs=0x88`, `wlc_hw_info.up=0xac` | Compiled offset assertions verify declarations only, not the real firmware layout |
| Clock state | No verified clock-enabled field found for this `wlc_hw_info` | A `clk` elsewhere in the shared file belongs to a different structure; `up` is insufficient proof |
| Original flashpatch table | `0x199000..0x199bf4`, 255 12-byte entries in target/size/data order | No entry overlaps reader entry address `0x9c76c`; unknown function extent/callees and clean ROM remain unresolved |
| Patch layout | Reclaim end `0x2307f0`, patch start `0x22c7f0`, size `0x4000`; relocated FP config `0x22bbf0`, size `0xc00`; compressed ucode region `0x222ed8`, size `0x9918` | Existing Nexmon layout, not a newly guessed free range |

`prepare.py` reproduces hash, hook, flashpatch and explicit command-case checks.
It scans NEX definitions and ioctl-named C/header files for decimal 430/hex 0x1ae
within pinned patches/utilities. It excludes the new variant. This audit is
deliberately scoped: unrelated numeric cases (e.g. Wireshark flow fields) are not
firmware command assignments, and unseen ROM command handling is not audited.
Stock Nexmon defines commands 400–428 and this image's ioctl hook adds 510/511.

The separate `drclink_tsf` variant reuses only the required common wrapper,
object-memory helpers and ucode decompressor plus original flashpatches. It
replaces the same dispatch pointer with a handler that forwards all other
commands to the original `wlc_ioctl`. It does not retain the stock Nexmon
monitor/injection/ioctl-debug hooks. The version string is not modified; use ABI
and explicit artifact provenance to identify this prototype.

## nl80211 transport review

`utilities/libnexio/libnexio.c` supplies a Broadcom OUI `0x001018`, subcommand 1
DCMD transport. Its pinned vendor path strips a fixed number of bytes from
the nested reply, does not expose exact reply length, and has an unbounded
receive loop. The new sampler implements the small GET transport directly,
parses nested attributes and waits for both ACK and reply under one deadline.

The pinned `patches/driver/brcmfmac_6.6.y-nexmon/vendor.c`/`vendor.h` define:
20-byte native-endian DCMD header, command 430, length 80, offset 20, set 0,
magic `0x14e46c77`; replies carry nested DATA type 2 and LEN type 1 (u16).
`NL80211_CMD_VENDOR=103`, IFINDEX=3 and vendor attributes 195–197 are checked
against the local Linux header during validation. This is source support and
synthetic parser testing; the actual Pi/kernel vendor path has not run.

## Enabling an actual reader is still blocked

Obtain the exact Pi revision, SDIO chip/revision evidence, OS/kernel/brcmfmac,
loaded firmware identity and file selection, decompressed hash if applicable,
board NVRAM/CLM hashes and a recovery baseline. Decide whether this research
image is appropriate; do not downgrade merely to match this patch.

Then verify the reader implementation/calling convention in the clean chip ROM
with the selected image's relevant flashpatches, high/low consistency under
rollover, struct pointer chain and a correct clock-available predicate. Do not
wake/change the clock in a read handler. Prove with source/disassembly that
calling it in the selected ioctl context is sound. Only then implement the
backend, retain bounded buffer/state checks, and proceed to a separately planned
deployment. Hardware must still establish AP TSF identity, idle/load latency,
reset/restart behavior and sustained operation. No RSN/WPS/association/media
claim follows from any of these source/build checks.
