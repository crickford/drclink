/* SPDX-License-Identifier: GPL-3.0-or-later
 * Reuses Nexmon's pinned ucode-compression/reclaim hooks; no monitor/injection. */
#pragma NEXMON targetregion "patch"
#include <firmware_version.h>
#include <wrapper.h>
#include <patcher.h>

__attribute__((at(WLC_UCODE_WRITE_BL_HOOK_ADDR, "", CHIP_VER_ALL, FW_VER_ALL)))
BLPatch(wlc_ucode_write_compressed, wlc_ucode_write_compressed);

__attribute__((at(HNDRTE_RECLAIM_0_END_PTR, "", CHIP_VER_ALL, FW_VER_ALL)))
GenericPatch4(hndrte_reclaim_0_end, PATCHSTART);
