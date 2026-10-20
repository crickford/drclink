/* SPDX-License-Identifier: GPL-3.0-or-later */
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include "../firmware/tsf_protocol.h"

static int calls;
static drc_u32 result;
static drc_u32 reader(void *ctx, drc_u32 *lo, drc_u32 *hi)
{
    assert(ctx == &calls);
    calls++;
    *lo = 0x89abcdef; *hi = 0x01234567;
    return result;
}

static void req(unsigned char *b, drc_u32 op)
{
    memset(b, 0, DRC_TSF_SIZE);
    drc_put32(b, 0x46535444); drc_put32(b + 4, 1);
    drc_put32(b + 8, 80); drc_put32(b + 12, op);
    drc_put32(b + 72, 0xdeadbeef); drc_put32(b + 76, 0x01234567);
}

int main(void)
{
    unsigned char storage[100], snapshot[100], *b = storage + 1;
    int n, i;
    memset(storage, 0xa5, sizeof(storage));
    for (n = -1; n <= 98; n++) {
        if (n == 80) continue;
        memcpy(snapshot, storage, sizeof(storage));
        assert(drc_tsf_handle(b, n, &calls, 1, reader) == -2);
        assert(memcmp(snapshot, storage, sizeof(storage)) == 0);
    }
    assert(drc_tsf_handle(0, 80, &calls, 1, reader) == -2);
    req(b, 0);
    assert(drc_tsf_handle(b, 80, &calls, 0, reader) == 0);
    assert(calls == 0 && drc_get32(b + 16) == 0);
    assert(drc_get32(b + 24) == 0 && drc_get32(b + 28) == 0);
    assert(drc_get32(b + 72) == 0xdeadbeef && drc_get32(b + 76) == 0x01234567);
    req(b, 1);
    drc_tsf_handle(b, 80, &calls, 0, reader);
    assert(calls == 0 && drc_get32(b + 16) == DRC_TSF_UNVERIFIED);
    req(b, 1);
    drc_tsf_handle(b, 80, &calls, 1, 0);
    assert(calls == 0 && drc_get32(b + 16) == DRC_TSF_UNVERIFIED);
    req(b, 1);
    drc_tsf_handle(b, 80, &calls, 1, reader);
    assert(calls == 1 && drc_get32(b + 24) == 0x89abcdef);
    assert(drc_get32(b + 28) == 0x01234567);
    assert(memcmp(b + 24, "\xef\xcd\xab\x89\x67\x45\x23\x01", 8) == 0);
    /* A backend writing partial data and failing must never leak a timestamp. */
    for (result = DRC_TSF_CLOCK_DOWN; result <= DRC_TSF_READ_FAILED; result++) {
        req(b, 1);
        drc_tsf_handle(b, 80, &calls, 1, reader);
        assert(drc_get32(b + 16) == result);
        assert(drc_get32(b + 24) == 0 && drc_get32(b + 28) == 0);
    }
    for (i = 0; i < 72; i++) {
        /* Every header/reserved byte except the operation byte is validated. */
        req(b, 1); b[i] ^= 0xff;
        n = calls;
        drc_tsf_handle(b, 80, &calls, 1, reader);
        assert(calls == n && drc_get32(b + 16) == DRC_TSF_BAD_REQUEST);
    }
    assert(storage[0] == 0xa5 && storage[81] == 0xa5);
    puts("protocol checks passed: lengths, null/unaligned buffers, reserved bytes, cookies, word order, errors, fail-closed backend");
    return 0;
}
