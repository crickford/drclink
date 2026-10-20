#!/usr/bin/env python3
"""Optional rootless x86_64 Linux toolchain runtime; no system installation."""
import argparse
import hashlib
from pathlib import Path
import platform
import shlex
import subprocess
import urllib.request

PACKAGES = [
    ('g/glibc/libc6-i386_2.41-12+deb13u4_amd64.deb',
     '50c1653ce29519e1fcd8742f2c67f5edad8d4c3aed3d1ffbea2b7c3497a809d2', 'root'),
    ('g/gcc-14/lib32stdc++6_14.2.0-19_amd64.deb',
     '326011247094e9a5e5b7dd431b67ccf6dddfd9c4ee5078a8d1a15661883791c2', 'root'),
    ('g/gcc-14/lib32gcc-s1_14.2.0-19_amd64.deb',
     'aa7674a786d58a1efaacb3ce377c06a2d3153c1fa7360b08375c7dd67c90c3fe', 'root'),
    ('q/qemu/qemu-user-static_7.2+dfsg-7+deb12u18+b3_amd64.deb',
     'c3e3ba2bd87f8c5b9a5da5ef21b5a3b82d7c63b89dd448d9ddaa4eabc5b6e402', 'qemu'),
    ('g/gawk/gawk_5.3.2-1_amd64.deb',
     '40f40d768e028b15598b84de036e884cd0b33f83f7658178407c6751eda297f4', 'native'),
    ('libs/libsigsegv/libsigsegv2_2.14-1+b2_amd64.deb',
     'bac603b965f303ea88dc39cd723b3d88cd86e3be9c948783f0d5e0d00e971302', 'native'),
    ('v/vim/xxd_9.1.1230-2_amd64.deb',
     'd1ae72a9ae7c3f81a23299422f0f219b55d2a76eb88c78815584983d6b897324', 'native'),
]


def executable(path, code):
    path.write_text(code)
    path.chmod(0o755)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('destination', type=Path)
    p.add_argument('nexmon', type=Path)
    args = p.parse_args()
    if (platform.system(), platform.machine()) != ('Linux', 'x86_64'):
        p.error('this optional runtime is for Linux x86_64 only')
    dest, nexmon = args.destination.resolve(), args.nexmon.resolve()
    dest.mkdir(parents=True, exist_ok=True)
    for relative, digest, folder in PACKAGES:
        archive = dest / Path(relative).name
        if not archive.exists():
            temp = archive.with_suffix('.partial')
            with urllib.request.urlopen('https://deb.debian.org/debian/pool/main/' + relative, timeout=60) as src, temp.open('wb') as out:
                while chunk := src.read(1024 * 1024):
                    out.write(chunk)
            temp.rename(archive)
        if hashlib.sha256(archive.read_bytes()).hexdigest() != digest:
            raise ValueError(f'package checksum mismatch: {archive}')
        subprocess.run(['dpkg-deb', '-x', str(archive), str(dest / folder)], check=True)
    bin_dir = dest / 'bin'
    bin_dir.mkdir(exist_ok=True)
    q = shlex.quote
    launch = bin_dir / 'exec-i386'
    executable(launch, '#!/bin/bash\nexec ' + q(str(dest / 'qemu/usr/bin/qemu-i386-static')) + ' ' +
               q(str(dest / 'root/usr/lib32/ld-linux.so.2')) + ' --library-path ' +
               q(str(dest / 'root/usr/lib32')) + ' "$@"\n')
    compiler_dir = nexmon / 'buildtools/gcc-arm-none-eabi-5_4-2016q2-linux-x86/bin'
    for binary in compiler_dir.glob('arm-none-eabi-*'):
        command = '#!/bin/bash\nexec ' + q(str(launch)) + ' ' + q(str(binary))
        if binary.name.endswith('-gcc'):
            command += ' -wrapper ' + q(str(launch))
        executable(bin_dir / binary.name, command + ' "$@"\n')
    # Equivalent zlib stream consumed by Nexmon's tinflate path. Version and
    # output hashes must be recorded; identical versions are needed for bitwise
    # reproduction. No arbitrary CLI options are accepted by this small shim.
    executable(bin_dir / 'zlib-flate', '#!/usr/bin/env python3\nimport sys,zlib\n'
               'if sys.argv[1:] != ["-compress"]: sys.exit("expected -compress")\n'
               'sys.stdout.buffer.write(zlib.compress(sys.stdin.buffer.read()))\n')
    env = dest / 'env.sh'
    env.write_text('export PATH=' + q(str(bin_dir) + ':' + str(dest / 'native/usr/bin')) + ':"$PATH"\n' +
                   'export LD_LIBRARY_PATH=' + q(str(dest / 'native/usr/lib/x86_64-linux-gnu')) + ':"${LD_LIBRARY_PATH:-}"\n' +
                   'export DRCLINK_CC_PREFIX=' + q(str(bin_dir / 'arm-none-eabi-')) + '\n')
    print(f'Runtime ready. Source {env} before invoking build.sh.')


if __name__ == '__main__':
    main()
