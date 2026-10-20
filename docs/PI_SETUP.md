# Bring up the new Pi and establish management access

These are setup instructions, not a record of hardware tests. The user has a new
Pi 4B and wired Ethernet available. No Pi inventory or SSH endpoint has been received.

## 1. Install a stock OS and boot over Ethernet

Use a microSD card (32 GB or larger is convenient), a suitable Pi 4 USB-C power
supply, and the built-in Ethernet port connected to your router. Keep a spare
card or a card reader available for recovery. No extra wireless radio is needed.

On Windows, install [Raspberry Pi Imager](https://www.raspberrypi.com/software/).
Select Raspberry Pi 4 and the current **Raspberry Pi OS Lite (64-bit)** offered
by Imager. This is the management/inventory baseline, not a claim that its
shipped firmware matches the Nexmon research target. Record the image release.

In OS customization:

- Hostname: `drclink-pi`.
- Create a normal user, for example `drc`; choose your own password.
- Enable SSH, preferably with your Windows public key. In PowerShell,
  `ssh-keygen -t ed25519` creates a key if you do not already have one;
  `Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub` displays the **public** key.
  Keep private keys and passwords out of this repository and chat.
- Set your timezone and actual Wi-Fi regulatory country. Leave Wi-Fi network
  provisioning empty when using wired Ethernet.

Write the card, insert it, connect Ethernet, and power up. Writing the image erases
the selected card; double-check the device in Imager. Allow a few minutes to boot.
Find the address in your router's DHCP list if `drclink-pi.local` does not resolve.
Then from PowerShell:

```powershell
ssh drc@drclink-pi.local
```

Check the SSH host fingerprint using a local console or another trusted channel
before accepting it; preserve host-key checking. On the Pi:

```bash
sudo apt update
sudo apt full-upgrade
sudo apt install git python3 build-essential ethtool iw rfkill openssh-server
sudo reboot
```

Reconnect after reboot. Do not install Nexmon or replace Wi-Fi firmware yet.
Record the inventory after updates, since kernel and firmware may have changed.
If the regulatory country was not offered without Wi-Fi provisioning in Imager,
set the actual country through `sudo raspi-config` → Localisation → WLAN Country.

## 2. Collect the baseline

Copy the repository from Windows (or clone it once these changes are available).
The current implementation files may still be local to the Codex workspace;
do not assume they have been pushed. From the Pi's repository root:

```bash
mkdir -p pairing-private
sudo bash tools/pi-tsf/inventory.sh > pairing-private/pi-inventory.txt 2>&1
```

Send the inventory text for review. It can contain MAC addresses and network
identifiers; redact unrelated identifiers if needed. No keys/passwords are
collected. Missing utilities and errors remain visible in the report. Firmware
file candidates/hashes alone do not establish the loaded path; we will reconcile
kernel logs, board-specific names, symlinks and any compression before deployment.

If you have not copied the new collector yet, these commands on the Pi are
enough for the first review (paste their output):

```bash
uname -a
cat /etc/os-release
tr -d '\0' < /proc/device-tree/model; printf '\n'
ethtool -i wlan0
iw dev
iw phy
iw reg get
sudo journalctl -k -b --no-pager --grep 'brcm|firmware|mmc'
```

Substitute the actual interface from `iw dev` if it is not `wlan0`. We can collect
the more detailed file/symlink/hash inventory once SSH is available.

## 3. Let this Codex cloud workspace reach SSH

SSH working from Windows does **not** make a private LAN address reachable from
this cloud workspace. Current workspace policy reports `vpn_configured: false`
and no TCP grants. Do not forward port 22 on your Internet router.

Two supported routes:

1. Open this repo in a Codex environment running on your Windows/LAN machine,
   where the Pi is reachable, and provide the Pi address and SSH username. That
   session needs its own SSH public key authorized on the Pi.
2. For this managed cloud workspace, configure its **Tailscale VPN integration**
   in the environment settings and enroll the Pi in the same tailnet. Install
   Tailscale on the Pi using its [official Linux instructions](https://tailscale.com/download/linux),
   then run `sudo tailscale up` and complete login privately. Ordinary OpenSSH
   over Tailscale is sufficient; enabling Tailscale SSH is unnecessary. Configure
   the cloud environment's TCP access grant for the Pi's exact Tailscale IPv4
   `/32` (not the whole tailnet). Permit that workspace identity to reach the
   Pi on TCP 22 in your tailnet ACL. Apply the environment configuration; TCP
   grant changes require a replacement environment. Do not put enrollment tokens
   or auth keys in chat or the repository.

Once the workspace VPN and grant are ready, provide only the Pi Tailscale IPv4,
SSH username and verified SSH host fingerprint. We can generate a dedicated
workspace SSH key and give you its public half to authorize in
`~/.ssh/authorized_keys`. The private half stays in the workspace; a replacement
workspace may need a new key. Keep your Windows key separate.

The cloud agent will use the supported TCP CONNECT forwarding path through
`proxy:8088` and verify the Pi's host key. Installing a VPN client inside the
executor or merely changing proxy variables does not supply VPN access. No VPN
configuration tool is currently exposed in this chat; this step belongs in the
environment's configuration UI. If that UI lacks VPN integration, use route 1.

Keep the `drc` account as a normal account. Inventory requiring elevated access
can initially be run by you with `sudo`; we do not need blanket passwordless
root for initial SSH. Discuss narrowly scoped elevation once actual experiments
and deployment/recovery commands are concrete.

## 4. Preserve recovery and separate future USB setup

Keep Ethernet management connected throughout radio tests, a stock card/image,
and offline access to the card. Before later firmware deployment, save the exact
loaded firmware, NVRAM and CLM files, symlink layout, hashes, OS/kernel packages
and a recovery procedure. With a failed boot/radio, power down, mount the card
on another machine and restore those exact files, or boot the spare stock card.

USB Ethernet to Windows remains the intended media transport. Defer gadget
configuration until inventory is reviewed; it changes boot/module/network
configuration and deserves a separate test. The Pi 4 USB-C port carries device
data and power: a laptop port is not assumed to supply adequate power. Plan a
verified power/data arrangement before replacing the normal supply. Never connect
two competing supplies with an improvised cable. Ethernet management lets us
experiment without prematurely depending on USB gadget or the GamePad radio.
