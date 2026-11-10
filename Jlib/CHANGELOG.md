## 2026-11-10 — 0.3.4

- Add opt-in shared CPU work optimizations: parent-world pickup indices, event-driven offset refresh, ordered tag candidates, and bounded navigation selection.
- Add optional publication revision support and concurrent decoded-color reuse. Existing providers without revisions retain their original read path.
- Add local differential tests and real Harmony hook tests in the OniMods consumer repository. Gameplay performance acceptance is recorded separately.

## 2026-11-10 — 0.3.3

- Batch multicomponent overlay reads within each render frame; release snapshots when the frame, provider or view changes.
- Compute oxygen overlay colours from total breathable gas mass, including polluted oxygen.
- Host coverage: 602 assertions passed before this version-only release change. Paused full-map overlay timings improved in the tested scene; running gameplay FPS is not yet accepted.
