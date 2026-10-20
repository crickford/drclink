#!/usr/bin/env python3
"""Verify a pinned source checkout and stage a build-only Nexmon variant."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess

PIN = '68720406f54ff16cad28fd3c51f2392aefad9fb7'
BASE = '7fe369f7fdfded9eeceb8e438f7346e58634741b326d0715ad1b15275e6fa3a4'
VARIANT = Path('patches/bcm43455c0/7_45_206/drclink_tsf')
INPUTS = {
    'patches/common/wrapper.c': '281b8268ccc3284c5884e2de405b75bf636b092c2520197de32d3f857bc75176',
    'firmwares/bcm43455c0/structs.common.h': '57aedee35fc717f02da0bf18a1fad9c4c5fe38682503b8f6907a2af4b9e9a134',
    'patches/bcm43455c0/7_45_206/nexmon/Makefile': '596837e72a4f235f648d93446bf2f37e0d670a8d7e47da071be2a932e2b7848e',
}


def audit(root):
    head = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
    if head != PIN:
        raise ValueError(f'expected Nexmon {PIN}, got {head}')
    # Reject changed tracked inputs, including dependency Makefiles; generated
    # untracked build outputs are allowed for rebuilding the same variant.
    if subprocess.check_output(['git', '-C', str(root), 'status', '--porcelain', '--untracked-files=no']):
        raise ValueError('tracked Nexmon inputs are modified; use a clean pinned checkout')
    for p, expected in INPUTS.items():
        if hashlib.sha256((root / p).read_bytes()).hexdigest() != expected:
            raise ValueError(f'changed input: {p}')
    data = (root / 'firmwares/bcm43455c0/7_45_206/brcmfmac43455-sdio.bin').read_bytes()
    if hashlib.sha256(data).hexdigest() != BASE:
        raise ValueError('base firmware SHA-256 mismatch')
    base = 0x198000
    pointer = struct.unpack_from('<I', data, 0x20E264 - base)[0]
    if pointer != 0x203B9:
        raise ValueError('original hook pointer does not match Thumb wlc_ioctl')
    # fpext.c defines target_addr, size, data_ptr in that order (12 bytes).
    flashpatches = [struct.unpack_from('<III', data, off)
                    for off in range(0x199000 - base, 0x199BF4 - base, 12)]
    overlapping = [dict(address=hex(addr), size=size, data=hex(ptr))
                   for addr, size, ptr in flashpatches if addr <= 0x9C76C < addr + size]
    # No global allocation promise: only named definitions and explicit cases
    # in this pinned source are audited. Stock ROM command space remains unknown.
    collisions = []
    for directory in ('patches', 'utilities'):
        for p in (root / directory).rglob('*'):
            if p.suffix not in ('.c', '.h') or VARIANT in p.relative_to(root).parents:
                continue
            for number, line in enumerate(p.read_text(errors='replace').splitlines(), 1):
                named = re.search(r'^\s*#define\s+NEX_\w+\s+(430|0x1[aA][eE])\b', line)
                case = 'ioctl' in p.name and re.search(r'\bcase\s+(430|0x1[aA][eE])\s*:', line)
                if named or case:
                    collisions.append(f'{p.relative_to(root)}:{number}: {line.strip()}')
    if collisions:
        raise ValueError('command 430 collision: ' + '; '.join(collisions))
    return dict(nexmon_commit=head, base_sha256=BASE, base_size=len(data),
                ram_start=hex(base), ram_size=hex(0xC8000),
                hook_address=hex(0x20E264), original_hook_pointer=hex(pointer),
                reader_rom_address=hex(0x9C76C), flashpatch_entries=len(flashpatches),
                reader_entry_overlaps=overlapping, command=430,
                reader_enabled=False, clock_domain_verified=False,
                blockers=['Actual Pi board/chip/kernel/loaded firmware/NVRAM/CLM inventory absent',
                          'Clean BCM43455c0 ROM not supplied; reader instructions not verified',
                          'Shared wlc/hw layout and clock-enabled predicate not verified on target',
                          'AP clock domain and transport behavior require hardware measurement'])


def prepare(root, write_patch=False):
    report = audit(root)
    here = Path(__file__).resolve().parent
    files = {
        'Makefile': (here / 'firmware/Makefile').read_bytes(),
        'patch.ld': (root / 'patches/bcm43455c0/7_45_206/nexmon/patch.ld').read_bytes(),
        'src/tsf.c': (here / 'firmware/tsf.c').read_bytes(),
        'src/tsf_protocol.h': (here / 'firmware/tsf_protocol.h').read_bytes(),
        'src/patch.c': (here / 'firmware/patch.c').read_bytes(),
    }
    for rel, data in files.items():
        p = root / VARIANT / rel
        if p.exists() and p.read_bytes() != data:
            raise ValueError(f'existing variant differs: {p}; preserve it or use a fresh checkout')
    patches = []
    for rel, data in files.items():
        p = root / VARIANT / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data)
        r = subprocess.run(['git', 'diff', '--no-index', '--', '/dev/null', str(VARIANT / rel)],
                           cwd=root, stdout=subprocess.PIPE, check=False)
        if r.returncode != 1:
            raise ValueError('could not generate patch')
        patches.append(r.stdout)
    if write_patch:
        (here / 'nexmon-tsf.patch').write_bytes(b''.join(patches))
    return report


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('nexmon', type=Path)
    p.add_argument('--write-patch', action='store_true')
    a = p.parse_args()
    print(json.dumps(prepare(a.nexmon.resolve(), a.write_patch), indent=2))
