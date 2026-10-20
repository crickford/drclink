# Experiment evidence

The JSON/JSONL files below are copied from user uploads. Filenames were made descriptive; content was preserved, except that text reconstruction may add a trailing newline. JSON values and JSONL records are unchanged.

| File | Original uploaded name | Meaning |
| --- | --- | --- |
| [windows-protected-v0.1.0-summary.json](windows-protected-v0.1.0-summary.json) | `summary.json` | Protected WinRT control worked; one request and attached peer |
| [windows-open-v0.1.0-summary.json](windows-open-v0.1.0-summary.json) | `summary(1).json` | Projected null assignment failed; cleanup was misleading in this version |
| [windows-open-v0.1.0-events.jsonl](windows-open-v0.1.0-events.jsonl) | `events.jsonl` | Exact exception stages, HRESULTs and UTC timestamps for that open run |
| [windows-open-v0.1.1-summary.json](windows-open-v0.1.1-summary.json) | `summary(2).json` | Native setter invoked and rejected null; no Start attempt |
| [user-reported-hardware-and-service.md](user-reported-hardware-and-service.md) | Pasted conversation output | Selected hardware capabilities, OS/service state, and limits of those observations |

Do not infer a radio band, successful open authentication, Nintendo WPS or GamePad pairing from these files. The raw summaries explicitly mark those properties unmeasured/unimplemented. No WDI boundary ETL, Pi device inventory, modified Pi firmware run or GamePad media capture has been received.
