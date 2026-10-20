/* SPDX-License-Identifier: GPL-3.0-or-later */
#ifndef DRCLINK_TSF_PROTOCOL_H
#define DRCLINK_TSF_PROTOCOL_H

/* Byte-oriented ABI: no alignment, padding or host-endian assumptions. */
#define DRC_TSF_CMD 430
#define DRC_TSF_SIZE 80
#define DRC_TSF_ABI 1
#define DRC_TSF_QUERY 0
#define DRC_TSF_READ 1
#define DRC_TSF_OK 0
#define DRC_TSF_BAD_REQUEST 1
#define DRC_TSF_UNVERIFIED 2
#define DRC_TSF_CLOCK_DOWN 3
#define DRC_TSF_READ_FAILED 4
#define DRC_TSF_CAP_READ64 1

typedef unsigned int drc_u32;
typedef char drc_u32_must_be_four_bytes[sizeof(drc_u32) == 4 ? 1 : -1];

static drc_u32 drc_get32(const unsigned char *p)
{
    return (drc_u32)p[0] | ((drc_u32)p[1] << 8) |
        ((drc_u32)p[2] << 16) | ((drc_u32)p[3] << 24);
}

static void drc_put32(unsigned char *p, drc_u32 n)
{
    p[0] = n; p[1] = n >> 8; p[2] = n >> 16; p[3] = n >> 24;
}

/* A future verified backend must check clock/pointers before reading, return a
 * status above, and never wake the clock or change BSS/radio state. */
typedef drc_u32 (*drc_reader)(void *, drc_u32 *, drc_u32 *);

static int drc_tsf_handle(unsigned char *arg, int len, void *ctx,
                          drc_u32 caps, drc_reader read_clock)
{
    static const unsigned char base_hash[32] = {
        0x7f,0xe3,0x69,0xf7,0xfd,0xfd,0xed,0x9e,
        0xec,0xeb,0x8e,0x43,0xf7,0x34,0x6e,0x58,
        0x63,0x47,0x41,0xb3,0x26,0xd0,0x71,0x5a,
        0xd1,0xb1,0x52,0x75,0xe6,0xfa,0x3a,0x4a
    };
    drc_u32 op, cookie_l, cookie_h, status = DRC_TSF_OK, lo = 0, hi = 0;
    int i;
    if (!arg || len != DRC_TSF_SIZE) return -2;
    op = drc_get32(arg + 12);
    cookie_l = drc_get32(arg + 72); cookie_h = drc_get32(arg + 76);
    if (drc_get32(arg) != 0x46535444 || drc_get32(arg + 4) != DRC_TSF_ABI ||
        drc_get32(arg + 8) != DRC_TSF_SIZE || op > DRC_TSF_READ)
        status = DRC_TSF_BAD_REQUEST;
    for (i = 16; i < 72; i++)
        if (arg[i]) status = DRC_TSF_BAD_REQUEST;
    /* Clear all response bytes, including timestamp words on any failure. */
    for (i = 0; i < DRC_TSF_SIZE; i++) arg[i] = 0;
    if (status == DRC_TSF_OK && op == DRC_TSF_READ) {
        if (!(caps & DRC_TSF_CAP_READ64) || !read_clock)
            status = DRC_TSF_UNVERIFIED;
        else
            status = read_clock(ctx, &lo, &hi);
    }
    drc_put32(arg, 0x46535444); drc_put32(arg + 4, DRC_TSF_ABI);
    drc_put32(arg + 8, DRC_TSF_SIZE); drc_put32(arg + 12, op);
    drc_put32(arg + 16, status); drc_put32(arg + 20, caps);
    if (status == DRC_TSF_OK && op == DRC_TSF_READ) {
        drc_put32(arg + 24, lo); drc_put32(arg + 28, hi);
    }
    drc_put32(arg + 32, 0x043455c0); /* Research target, not runtime detection. */
    drc_put32(arg + 36, 0); /* Clock domain unverified. */
    for (i = 0; i < 32; i++) arg[40 + i] = base_hash[i];
    drc_put32(arg + 72, cookie_l); drc_put32(arg + 76, cookie_h);
    return 0;
}
#endif
