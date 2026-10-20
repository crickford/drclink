#!/bin/bash
# Build only. No sudo, firmware installation, module loading, or radio changes.
set -eo pipefail
task_dir=$(cd -- "$(dirname -- "$0")" && pwd)
test "$#" = 1 || { echo "usage: $0 /path/to/pinned/nexmon" >&2; exit 2; }
nexmon_dir=$(cd -- "$1" && pwd)
python3 "$task_dir/prepare.py" "$nexmon_dir"
cd "$nexmon_dir"
# Do not send upstream build statistics.
touch DISABLE_STATISTICS
source setup_env.sh
# Optional toolchain prefix supports isolated/emulated local builds.
if test -n "${DRCLINK_CC_PREFIX:-}"; then export CC="$DRCLINK_CC_PREFIX"; fi
make -C buildtools/flash_patch_extractor
make -C firmwares/bcm43455c0/7_45_206
# Serial build: upstream init/generator files are shared and not parallel-safe.
make -j1 -C patches/bcm43455c0/7_45_206/drclink_tsf firmware
sha256sum patches/bcm43455c0/7_45_206/drclink_tsf/brcmfmac43455-sdio.bin
