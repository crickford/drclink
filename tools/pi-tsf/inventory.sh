#!/bin/bash
# Read-only baseline. Output can contain MACs, hostnames and network identifiers.
set -u
run() {
    printf '\n###'
    printf ' %q' "$@"
    printf '\n'
    "$@" 2>&1
    rc=$?
    printf '[exit=%s]\n' "$rc"
}
run date -u --iso-8601=seconds
run uname -a
run cat /etc/os-release
run bash -c 'tr -d "\0" < /proc/device-tree/model; printf "\n"'
run bash -c 'tr -d "\0" < /proc/device-tree/compatible; printf "\n"'
run bash -c 'sed -n "/^Revision/p" /proc/cpuinfo'
run ip -brief link
run iw dev
run iw phy
run iw reg get
run rfkill list
run lsmod
run journalctl -k -b --no-pager --grep 'brcm|firmware|mmc'
run bash -c 'for n in /sys/class/net/*; do
    test -e "$n/phy80211" || continue
    printf "\nInterface: %s\n" "${n##*/}"
    ethtool -i "${n##*/}"
    readlink -f "$n/device/driver"
    cat "$n/device/modalias" "$n/device/uevent"
done'
run bash -c 'modinfo brcmfmac | sed -n "/^filename:/p; /^version:/p; /^srcversion:/p; /^vermagic:/p; /^firmware:/p"'
run bash -c 'for d in /lib/firmware/brcm /lib/firmware/cypress; do
    test -d "$d" || continue
    find "$d" -maxdepth 1 \( -name "*43455*" -o -name "*4345*" \) -print0 |
    while IFS= read -r -d "" f; do
        ls -ld "$f"
        readlink -f "$f"
        test ! -f "$f" || sha256sum "$f"
    done
done'
run bash -c 'dpkg-query -W firmware-brcm80211 raspi-firmware linux-image-rpi-v8 raspberrypi-kernel 2>&1'
# Candidate file hashes are not proof of which file the driver loaded. Compressed
# hashes are of the compressed bytes. Resolve both questions before deployment.
