## 2026-11-10 — 0.3.5

- Add a shared simulation scheduler for asynchronous native providers. Continue independent managed sub-ticks while native work runs; defer only handoffs whose completion signal is still busy.
- Preserve the current published snapshot for rendering. Bound accumulated elapsed time, clear deferred time on pause, isolate game lifetimes and propagate native faults.
- Exercise the real Harmony prefix against the private game loop shape, including variable render intervals and ready-to-busy transitions within a call. Runtime performance is recorded separately by the consumer.

## 2026-11-10 — 0.3.4

- Add opt-in shared CPU work optimizations: parent-world pickup indices, event-driven offset refresh, ordered tag candidates, and bounded navigation selection.
- Add optional publication revision support and concurrent decoded-color reuse. Existing providers without revisions retain their original read path.
- Add local differential tests and real Harmony hook tests in the OniMods consumer repository. Gameplay performance acceptance is recorded separately.

## 2026-11-10 — 0.3.3

- Batch multicomponent overlay reads within each render frame; release snapshots when the frame, provider or view changes.
- Compute oxygen overlay colours from total breathable gas mass, including polluted oxygen.
- Host coverage: 602 assertions passed before this version-only release change. Paused full-map overlay timings improved in the tested scene; running gameplay FPS is not yet accepted.
