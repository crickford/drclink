#!/usr/bin/env python3
"""Bounded nl80211 TSF sampler and offline analysis. Python standard library only."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import socket
import struct
import sys
import time

CMD, SIZE, ABI = 430, 80, 1
BASE_HASH = '7fe369f7fdfded9eeceb8e438f7346e58634741b326d0715ad1b15275e6fa3a4'
STATUS = {0: 'ok', 1: 'bad_request', 2: 'unverified_backend',
          3: 'clock_down', 4: 'read_failed'}


def request(op, cookie):
    b = bytearray(SIZE)
    struct.pack_into('<4sIII', b, 0, b'DTSF', ABI, SIZE, op)
    struct.pack_into('<Q', b, 72, cookie)
    return bytes(b)


def decode(b, op, cookie):
    if len(b) != SIZE:
        raise ValueError('TSF response length is not 80')
    magic, version, size, actual_op, status, caps, lo, hi, target, domain = struct.unpack_from('<4s9I', b)
    if (magic, version, size, actual_op) != (b'DTSF', ABI, SIZE, op):
        raise ValueError('TSF magic/version/size/operation mismatch')
    if struct.unpack_from('<Q', b, 72)[0] != cookie:
        raise ValueError('TSF cookie mismatch (stale or malformed response)')
    if target != 0x043455C0 or b[40:72].hex() != BASE_HASH:
        raise ValueError('unexpected research target/base hash')
    if status not in STATUS or caps & ~1 or domain != 0:
        raise ValueError('unknown status/capability/domain for ABI 1')
    if (status != 0 or op == 0) and (lo or hi):
        raise ValueError('timestamp present on failure or query')
    if status == 0 and op == 1 and not caps & 1:
        raise ValueError('read succeeded without READ64 capability')
    return dict(status=STATUS[status], status_code=status, capabilities=caps,
                tsf_us=((hi << 32) | lo) if status == 0 and op == 1 else None,
                target=hex(target), clock_domain='unverified', base_sha256=b[40:72].hex())


def align4(n):
    return (n + 3) & ~3


def attr(kind, payload):
    n = 4 + len(payload)
    return struct.pack('=HH', n, kind) + payload + bytes(align4(n) - n)


def attrs(data):
    out = {}
    while data:
        if len(data) < 4:
            raise ValueError('truncated netlink attribute')
        n, kind = struct.unpack_from('=HH', data)
        kind &= 0x3FFF
        if n < 4 or align4(n) > len(data) or kind in out:
            raise ValueError('invalid/duplicate netlink attribute')
        out[kind] = data[4:n]
        data = data[align4(n):]
    return out


def vendor_payload(data):
    outer = attrs(data)
    # nl80211: VENDOR_ID=195, VENDOR_SUBCMD=196, VENDOR_DATA=197.
    if outer.get(195) != struct.pack('=I', 0x001018) or outer.get(196) != struct.pack('=I', 1):
        raise ValueError('wrong vendor response')
    nested = attrs(outer.get(197, b''))
    # brcmfmac vendor.h: BRCMF_NLATTR_LEN=1 (u16), DATA=2.
    if nested.get(1) != struct.pack('=H', SIZE) or len(nested.get(2, b'')) != SIZE:
        raise ValueError('wrong brcmfmac reply length')
    return nested[2]


class VendorTransport:
    """One request in flight; replies and ACK/errors share a bounded deadline."""
    def __init__(self, interface, timeout):
        if sys.byteorder != 'little':
            raise ValueError('transport currently requires a little-endian Linux host')
        self.timeout = timeout
        self.seq = 0
        self.sock = socket.socket(socket.AF_NETLINK, socket.SOCK_RAW, 16)
        self.sock.bind((0, 0))
        self.port = self.sock.getsockname()[0]
        try:
            self.ifindex = socket.if_nametoindex(interface)
            result = self.exchange(16, 3, attr(2, b'nl80211\0'), reply_command=1)
            family = attrs(result)
            if len(family.get(1, b'')) != 2:
                raise ValueError('missing nl80211 family ID')
            self.family = struct.unpack('=H', family[1])[0]
        except BaseException:
            self.sock.close()
            raise

    def close(self):
        self.sock.close()

    def exchange(self, family, command, data, timeout=None, reply_command=None):
        self.seq += 1
        msg = struct.pack('=BBH', command, 1, 0) + data
        header = struct.pack('=IHHII', 16 + len(msg), family, 1 | 4, self.seq, self.port)
        deadline = time.monotonic() + (self.timeout if timeout is None else timeout)
        self.sock.settimeout(max(.001, deadline - time.monotonic()))
        self.sock.sendto(header + msg, (0, 0))
        reply, ack = None, False
        while reply is None or not ack:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError('netlink deadline exceeded')
            self.sock.settimeout(remaining)
            packet, _, flags, sender = self.sock.recvmsg(65536)
            if flags & socket.MSG_TRUNC:
                raise ValueError('truncated netlink datagram')
            if sender[0] != 0:
                continue
            while packet:
                if len(packet) < 16:
                    raise ValueError('truncated netlink header')
                n, kind, _, seq, _ = struct.unpack_from('=IHHII', packet)
                if n < 16 or align4(n) > len(packet):
                    raise ValueError('invalid netlink message length')
                body = packet[16:n]
                packet = packet[align4(n):]
                if seq != self.seq:
                    continue
                if kind == 2:
                    if len(body) < 4:
                        raise ValueError('short netlink error')
                    error = struct.unpack_from('=i', body)[0]
                    if error:
                        raise OSError(-error, os.strerror(-error))
                    ack = True
                elif kind == family:
                    expected = command if reply_command is None else reply_command
                    if len(body) < 4 or body[0] != expected or reply is not None:
                        raise ValueError('unexpected generic-netlink response')
                    reply = body[4:]
                else:
                    raise ValueError('unexpected netlink message type')
        return reply

    def read(self, payload, timeout=None):
        # brcmf_vndr_dcmd_hdr: native-endian on little-endian Pi, set=0.
        dcmd = struct.pack('=IiIII', CMD, SIZE, 20, 0, 0x14E46C77) + payload
        data = (attr(3, struct.pack('=I', self.ifindex)) +
                attr(195, struct.pack('=I', 0x001018)) +
                attr(196, struct.pack('=I', 1)) + attr(197, dcmd))
        return vendor_payload(self.exchange(self.family, 103, data, timeout))


def percentile(values, p):
    if not values:
        return None
    a = sorted(values)
    x = (len(a) - 1) * p
    low = int(x)
    return a[low] + (a[min(low + 1, len(a) - 1)] - a[low]) * (x - low)


def summarize(rows, jump_us=1_000_000):
    durations, segments, events = [], [], []
    current, previous = [], None
    for r in rows:
        if r.get('kind') != 'sample':
            continue
        before, after = r['before_raw_ns'], r['after_raw_ns']
        if after < before:
            raise ValueError('host raw clock moved backward')
        durations.append((after - before) / 1000)
        if r.get('status') != 'ok' or r.get('tsf_us') is None:
            if current:
                segments.append(current)
            current, previous = [], None
            events.append(dict(seq=r['seq'], event='failed_read', status=r.get('status')))
            continue
        point = ((before + after) // 2, r['tsf_us'])
        event = None
        if previous:
            host_delta = (point[0] - previous[0]) / 1000
            delta = point[1] - previous[1]
            if host_delta <= 0:
                raise ValueError('non-increasing sample host clock')
            if delta < 0:
                event = 'backward_reset_or_clock_change'
                if previous[1] > (1 << 64) - (1 << 32) and point[1] < (1 << 32):
                    event = 'possible_64bit_wrap_or_reset'
            elif delta == 0:
                event = 'repeated_tsf'
            elif abs(delta - host_delta) > jump_us:
                event = 'forward_discontinuity'
        if event:
            events.append(dict(seq=r['seq'], event=event))
            if current:
                segments.append(current)
            current = []
        current.append(point)
        previous = point
    if current:
        segments.append(current)
    fits = []
    for seg in segments:
        x = [(p[0] - seg[0][0]) / 1000 for p in seg]
        y = [p[1] - seg[0][1] for p in seg]
        xm, ym = sum(x) / len(x), sum(y) / len(y)
        denom = sum((a - xm) ** 2 for a in x)
        slope = sum((a - xm) * (b - ym) for a, b in zip(x, y)) / denom if denom else None
        fits.append(dict(count=len(seg), span_host_s=x[-1] / 1e6,
                         midpoint_offset_start_us=seg[0][1] - seg[0][0] / 1000,
                         fitted_rate_ppm=(slope - 1) * 1e6 if slope is not None else None))
    return dict(attempts=len(durations), duration_us={
        k: percentile(durations, p) for k, p in [('p50', .5), ('p95', .95), ('p99', .99), ('max', 1)]},
        segments=fits, events=events, forward_jump_flag_us=jump_us,
        model='midpoint anchors; transport symmetry and AP clock domain unproven; no sample filtering')


def finite_range(low, high):
    def parse(s):
        n = float(s)
        if not math.isfinite(n) or not low <= n <= high:
            raise argparse.ArgumentTypeError(f'must be finite and in [{low}, {high}]')
        return n
    return parse


def sample(args):
    if args.duration * args.rate > 250_000:
        raise ValueError('limit is 250000 samples per run; reduce duration/rate')
    inventory = args.inventory.read_bytes()
    firmware = args.firmware_image.read_bytes()
    # Exclusive creation preserves existing raw experiments.
    with args.output.open('x') as out:
        def emit(row):
            out.write(json.dumps(row, allow_nan=False) + '\n')
            out.flush()
        emit(dict(kind='metadata', utc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
                  host=platform.platform(), python=platform.python_version(), interface=args.interface,
                  transport='nl80211 Broadcom DCMD get (set=0)', command=CMD,
                  timeout_s=args.timeout, duration_s=args.duration, rate_hz=args.rate,
                  inventory_sha256=hashlib.sha256(inventory).hexdigest(),
                  firmware_file=str(args.firmware_image),
                  firmware_file_sha256=hashlib.sha256(firmware).hexdigest(),
                  loaded_identity='must be reconciled with kernel logs/inventory; file hash alone is insufficient'))
        transport = VendorTransport(args.interface, args.timeout)
        try:
            def attempt(op, seq, limit=None):
                cookie = seq
                row = dict(kind='query' if op == 0 else 'sample', seq=seq)
                row['before_raw_ns'] = time.clock_gettime_ns(time.CLOCK_MONOTONIC_RAW)
                response = None
                try:
                    response = transport.read(request(op, cookie), limit)
                    row['after_raw_ns'] = time.clock_gettime_ns(time.CLOCK_MONOTONIC_RAW)
                    row.update(decode(response, op, cookie))
                except (OSError, ValueError) as exc:
                    row.setdefault('after_raw_ns', time.clock_gettime_ns(time.CLOCK_MONOTONIC_RAW))
                    row.update(status='error', tsf_us=None, error=str(exc))
                if response is not None:
                    row['response_hex'] = response.hex()
                emit(row)
                return row
            q = attempt(0, 0)
            if q['status'] != 'ok':
                raise ValueError('capability query failed; raw result retained')
            if args.query_only:
                print(json.dumps(q, indent=2))
                return
            if not q['capabilities'] & 1:
                raise ValueError('reader unavailable: backend verification is incomplete; no samples attempted')
            end = time.monotonic() + args.duration
            seq, failures = 0, 0
            rows = []
            while time.monotonic() < end:
                seq += 1
                started = time.monotonic()
                row = attempt(1, seq, min(args.timeout, max(.001, end - started)))
                rows.append(row)
                failures = failures + 1 if row['status'] != 'ok' else 0
                if failures >= 3:
                    break
                time.sleep(max(0, min(started + 1 / args.rate, end) - time.monotonic()))
            emit(dict(kind='summary', **summarize(rows)))
            if failures >= 3:
                raise ValueError('stopped after three consecutive failed reads')
        finally:
            transport.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    run = commands.add_parser('sample')
    run.add_argument('--interface', default='wlan0')
    run.add_argument('--duration', type=finite_range(.01, 86400), default=10.)
    run.add_argument('--rate', type=finite_range(.01, 100), default=10.)
    run.add_argument('--timeout', type=finite_range(.01, 10), default=1.)
    run.add_argument('--inventory', type=Path, required=True)
    run.add_argument('--firmware-image', type=Path, required=True)
    run.add_argument('--output', type=Path, required=True)
    run.add_argument('--query-only', action='store_true')
    analyze = commands.add_parser('analyze')
    analyze.add_argument('input', type=Path)
    analyze.add_argument('--jump-us', type=finite_range(1, 1e12), default=1e6)
    args = parser.parse_args()
    try:
        if args.command == 'sample':
            sample(args)
        else:
            with args.input.open() as f:
                print(json.dumps(summarize((json.loads(s) for s in f if s.strip()), args.jump_us), indent=2))
    except (OSError, ValueError) as exc:
        parser.exit(1, f'{exc}\n')


if __name__ == '__main__':
    main()
