/* SPDX-License-Identifier: GPL-3.0-or-later */
#pragma NEXMON targetregion "patch"
#include <firmware_version.h>
#include <wrapper.h>
#include <structs.h>
#include <patcher.h>
#include "tsf_protocol.h"

/* Compile-time checks of UPSTREAM DECLARATIONS, not live layout verification. */
typedef char check_wlc_hw_offset[__builtin_offsetof(struct wlc_info, hw) == 0x10 ? 1 : -1];
typedef char check_hw_regs_offset[__builtin_offsetof(struct wlc_hw_info, regs) == 0x88 ? 1 : -1];
typedef char check_hw_up_offset[__builtin_offsetof(struct wlc_hw_info, up) == 0xac ? 1 : -1];
typedef char check_pointer_width[sizeof(void *) == 4 ? 1 : -1];

int drclink_ioctl(struct wlc_info *wlc, int cmd, char *arg, int len, void *wlc_if)
{
    if (cmd == DRC_TSF_CMD) {
        /* Fail closed: no ROM, clock-state or live-layout verification yet.
         * There is deliberately no build flag that enables an unverified call.
         * After auditing the ROM/flashpatch path and clock state, implement a
         * backend for wlc_bmac_read_tsf(wlc->hw, &lo, &hi) and set READ64.
         * Do not equate hw->up with a verified clock-enabled predicate.
         */
        return drc_tsf_handle((unsigned char *)arg, len, wlc, 0, 0);
    }
    return wlc_ioctl(wlc, cmd, arg, len, wlc_if);
}

__attribute__((at(0x20E264, "", CHIP_VER_BCM43455c0, FW_VER_7_45_206)))
GenericPatch4(drclink_ioctl_hook, drclink_ioctl + 1);
