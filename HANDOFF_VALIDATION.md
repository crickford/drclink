# Handoff validation

Prepared 19 October 2026, America/Los_Angeles. This validation covers the handoff, not fresh radio or firmware behavior.

- Verified the earlier Windows probe and driver-boundary ZIP SHA-256 values recorded in `docs/SOURCES.md`.
- Compared every extracted tool file with those ZIPs: **213 Windows-probe files and 4 boundary-collector files unchanged**.
- Checked **21 relative Markdown links** across the documentation; all resolve to included files/directories.
- Compared all three copied JSON summaries and all six JSONL event records semantically with the user-uploaded evidence; all match. Descriptive filenames and a possible final newline are the only reconstruction changes.
- Included the current saved Astra firmware report and preserved its pinned source references and uncertainty labels.
- Checked that required entry-point/context/status/milestone documents are present and that no upstream Git checkout or Pi firmware `.bin` is included.
- Generated a per-file SHA-256 inventory in `MANIFEST.json`; it excludes its own hash. The final ZIP is checked for archive integrity and against this inventory before delivery.

No implementation changes were made to the existing tools. Their original compile/parser/unit-check results are historical, not rerun as part of this documentation task. No Windows hotspot, service, kernel driver, Pi firmware or GamePad operation was performed. The Pi inventory, clock prototype, security interoperability and media/latency milestones remain untested exactly as recorded in `docs/STATUS_AND_DECISIONS.md`.
