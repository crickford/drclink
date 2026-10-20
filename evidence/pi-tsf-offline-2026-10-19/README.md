# Offline TSF continuation records

Local date: 19 October 2026, America/Los_Angeles. These records come from the
Codex Linux workspace, not a Pi or GamePad. No hardware experiments occurred.

- `artifact.json`: pinned-source audit and output artifact sanity checks/hashes.
- `build.txt`: clean build using the pinned compiler under the rootless QEMU
  runtime; ANSI codes/trailing whitespace removed and checkout path normalized
  to `<NEXMON_ROOT>`.
- `checks.txt`: sanitizer-backed C checks and Python unit-test results.

The image has an ABI handler but **no enabled hardware TSF reader**. A returned
build hash is not a clock measurement or evidence of GamePad compatibility.
See [validation](../../tools/pi-tsf/VALIDATION.md) for commands, versions,
reproduction and remaining blockers. No firmware images or captured key material
are retained here.
