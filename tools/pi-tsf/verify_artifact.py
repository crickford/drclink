#!/usr/bin/env python3
"""Offline sanity checks of the built ABI-only prototype, never deployment."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess
from prepare import audit, VARIANT


def verify(root, nm):
    source = audit(root)
    directory = root / VARIANT
    image = (directory / 'brcmfmac43455-sdio.bin').read_bytes()
    symbols_text = subprocess.check_output([nm, '-n', str(directory / 'gen/patch.elf')], text=True)
    symbols = {}
    for line in symbols_text.splitlines():
        fields = line.split()
        if len(fields) == 3:
            symbols[fields[2]] = int(fields[0], 16)
    pointer = struct.unpack_from('<I', image, 0x20E264 - 0x198000)[0]
    if pointer != symbols['drclink_ioctl'] + 1 or not 0x22C7F0 <= pointer < 0x2307F0:
        raise ValueError('ioctl pointer does not resolve to the patch-region handler')
    if len(image) != source['base_size']:
        raise ValueError('unexpected output image size')
    if 'wlc_bmac_read_tsf' in symbols or 'wlc_bmac_read_tsf_ram' in symbols:
        raise ValueError('unexpected hardware TSF reader linked into gated prototype')
    for name in ('wlc_monitor_hook', 'wlc_recv_hook', 'sendframe', 'udpvnprintf'):
        if name in symbols:
            raise ValueError(f'unexpected non-TSF hook/helper: {name}')
    return dict(source=source, output_sha256=hashlib.sha256(image).hexdigest(),
                output_size=len(image), ioctl_pointer=hex(pointer),
                handler_address=hex(symbols['drclink_ioctl']),
                reader_symbol_present=False, hardware_tested=False,
                patch_sha256=hashlib.sha256((Path(__file__).parent / 'nexmon-tsf.patch').read_bytes()).hexdigest())


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('nexmon', type=Path)
    p.add_argument('--nm', default='arm-none-eabi-nm')
    a = p.parse_args()
    print(json.dumps(verify(a.nexmon.resolve(), a.nm), indent=2))
