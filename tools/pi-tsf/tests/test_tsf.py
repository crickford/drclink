from pathlib import Path
import argparse
import json
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import tsf


def response(op=1, value=0x0123456789ABCDEF, status=0, caps=1, cookie=7):
    b = bytearray(tsf.request(op, cookie))
    struct.pack_into('<6I', b, 16, status, caps, value & 0xFFFFFFFF, value >> 32, 0x043455C0, 0)
    b[40:72] = bytes.fromhex(tsf.BASE_HASH)
    return bytes(b)


def row(seq, value, before=None, status='ok'):
    t = seq * 1_000_000_000 if before is None else before
    return dict(kind='sample', seq=seq, before_raw_ns=t, after_raw_ns=t + 100_000,
                status=status, tsf_us=value)


class Protocol(unittest.TestCase):
    def test_golden_word_order(self):
        self.assertEqual(tsf.decode(response(), 1, 7)['tsf_us'], 0x0123456789ABCDEF)

    def test_query_and_backend_failure(self):
        self.assertEqual(tsf.decode(response(op=0, value=0, caps=0), 0, 7)['capabilities'], 0)
        self.assertIsNone(tsf.decode(response(value=0, status=2, caps=0), 1, 7)['tsf_us'])

    def test_malformed_and_stale(self):
        for offset in [0, 4, 8, 12, 16, 20, 32, 36, 40, 71, 72]:
            b = bytearray(response()); b[offset] ^= 255
            with self.subTest(offset=offset), self.assertRaises(ValueError):
                tsf.decode(bytes(b), 1, 7)
        for n in [0, 79, 81]:
            with self.assertRaises(ValueError):
                tsf.decode(bytes(n), 1, 7)
        with self.assertRaises(ValueError):
            tsf.decode(response(status=3), 1, 7)
        with self.assertRaises(ValueError):
            tsf.decode(response(caps=0), 1, 7)


class Analysis(unittest.TestCase):
    def test_low_word_rollover_is_monotonic_64bit(self):
        result = tsf.summarize([row(0, 0xFFFFFFF0), row(1, 0x1000F4230)])
        self.assertEqual(result['events'], [])

    def test_zero_extended_32bit_wrap_is_not_silently_unwrapped(self):
        result = tsf.summarize([row(0, 0xFFFFFFF0), row(1, 100)])
        self.assertEqual(result['events'][0]['event'], 'backward_reset_or_clock_change')

    def test_reset_repeat_forward_jump_and_failure(self):
        rows = [row(0, 900), row(1, 100), row(2, 100), row(3, 10_000_000), row(4, None, status='error')]
        result = tsf.summarize(rows)
        self.assertEqual([e['event'] for e in result['events']], [
            'backward_reset_or_clock_change', 'repeated_tsf', 'forward_discontinuity', 'failed_read'])
        self.assertEqual(result['duration_us']['p99'], 100)

    def test_wrap_64_is_flagged_without_assuming_a_reset_cause(self):
        result = tsf.summarize([row(0, (1 << 64) - 10), row(1, 10)])
        self.assertEqual(result['events'][0]['event'], 'possible_64bit_wrap_or_reset')

    def test_fit_known_rate_and_separate_reset_segment(self):
        rows = [row(i, 7_000_000 + i * 1_000_010) for i in range(10)]
        rows += [row(10, 100), row(11, 1_000_110)]
        result = tsf.summarize(rows)
        self.assertEqual(len(result['segments']), 2)
        self.assertAlmostEqual(result['segments'][0]['fitted_rate_ppm'], 10, places=5)

    def test_host_clock_validation_and_empty(self):
        self.assertIsNone(tsf.summarize([])['duration_us']['p50'])
        bad = row(0, 1); bad['after_raw_ns'] = -1
        with self.assertRaises(ValueError):
            tsf.summarize([bad])


class Netlink(unittest.TestCase):
    def fixture(self, payload):
        return (tsf.attr(195, struct.pack('=I', 0x001018)) + tsf.attr(196, struct.pack('=I', 1)) +
                tsf.attr(197, tsf.attr(2, payload) + tsf.attr(1, struct.pack('=H', len(payload)))))

    def test_nested_vendor_fixture(self):
        self.assertEqual(tsf.vendor_payload(self.fixture(response())), response())
        for b in [self.fixture(response()[:-1]), self.fixture(response())[:-1], b'']:
            with self.assertRaises(ValueError):
                tsf.vendor_payload(b)

    def test_attribute_bounds_padding_and_duplicates(self):
        self.assertEqual(tsf.attrs(tsf.attr(4, b'abc')), {4: b'abc'})
        for b in [b'x', b'\x03\x00\x01\x00', tsf.attr(1, b'a') * 2]:
            with self.assertRaises(ValueError):
                tsf.attrs(b)

    def test_ack_and_response_in_either_order(self):
        class FakeSocket:
            def __init__(self, packets): self.packets = iter(packets)
            def sendto(self, *a): pass
            def settimeout(self, timeout): self.timeout = timeout
            def recvmsg(self, size): return next(self.packets), [], 0, (0, 0)
        def packet(kind, payload):
            return struct.pack('=IHHII', 16 + len(payload), kind, 0, 1, 0) + payload
        ack = packet(2, struct.pack('=i', 0))
        data = packet(42, struct.pack('=BBH', 103, 1, 0) + b'data')
        for packets in [(ack, data), (data, ack), (ack + data,)]:
            t = object.__new__(tsf.VendorTransport)
            t.seq, t.port, t.timeout = 0, 123, .1
            t.sock = FakeSocket(packets)
            self.assertEqual(t.exchange(42, 103, b''), b'data')
        t.seq = 0
        t.sock = FakeSocket([packet(2, struct.pack('=i', -95))])
        with self.assertRaises(OSError):
            t.exchange(42, 103, b'')

    def test_timeout_is_propagated(self):
        class FakeSocket:
            def sendto(self, *a): pass
            def settimeout(self, timeout): pass
            def recvmsg(self, size): raise TimeoutError('test timeout')
        t = object.__new__(tsf.VendorTransport)
        t.seq, t.port, t.timeout, t.sock = 0, 123, .01, FakeSocket()
        with self.assertRaises(TimeoutError):
            t.exchange(42, 103, b'')


class Sampling(unittest.TestCase):
    def args(self, directory):
        root = Path(directory)
        for name in ['inventory', 'firmware']:
            (root / name).write_bytes(b'fixture, not hardware')
        return argparse.Namespace(inventory=root / 'inventory', firmware_image=root / 'firmware',
                                  output=root / 'run.jsonl', interface='test0', timeout=.1,
                                  duration=1., rate=100., query_only=False)

    def test_fail_closed_and_preserve_existing_log(self):
        class FakeTransport:
            calls = []
            closed = False
            def __init__(self, *a): pass
            def read(self, data, timeout=None):
                self.calls.append(data)
                return response(op=0, value=0, caps=0, cookie=0)
            def close(self): FakeTransport.closed = True
        with tempfile.TemporaryDirectory() as d, patch.object(tsf, 'VendorTransport', FakeTransport):
            args = self.args(d)
            with self.assertRaisesRegex(ValueError, 'reader unavailable'):
                tsf.sample(args)
            self.assertEqual(len(FakeTransport.calls), 1)
            self.assertTrue(FakeTransport.closed)
            rows = [json.loads(s) for s in args.output.read_text().splitlines()]
            self.assertEqual([r['kind'] for r in rows], ['metadata', 'query'])
            original = args.output.read_bytes()
            with self.assertRaises(FileExistsError):
                tsf.sample(args)
            self.assertEqual(args.output.read_bytes(), original)

    def test_three_malformed_reads_stop_and_retain_raw(self):
        class FakeTransport:
            def __init__(self, *a): pass
            def read(self, data, timeout=None):
                op = struct.unpack_from('<I', data, 12)[0]
                if op == 0:
                    return response(op=0, value=0, cookie=0)
                return b'broken'
            def close(self): pass
        with tempfile.TemporaryDirectory() as d, patch.object(tsf, 'VendorTransport', FakeTransport), patch.object(tsf.time, 'sleep'):
            args = self.args(d)
            with self.assertRaisesRegex(ValueError, 'three consecutive'):
                tsf.sample(args)
            rows = [json.loads(s) for s in args.output.read_text().splitlines()]
            samples = [r for r in rows if r['kind'] == 'sample']
            self.assertEqual(len(samples), 3)
            self.assertTrue(all(r['status'] == 'error' and r['response_hex'] == b'broken'.hex() for r in samples))
            self.assertEqual(rows[-1]['attempts'], 3)

    def test_bounds(self):
        for value in ['nan', 'inf', '-1', '0', '101']:
            with self.assertRaises(argparse.ArgumentTypeError):
                tsf.finite_range(.01, 100)(value)


if __name__ == '__main__':
    unittest.main()
