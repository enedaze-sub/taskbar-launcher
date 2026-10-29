# Windows hover performance investigation — 2026-10-29

Unreleased changes on main; the current release remains v8.4.4.

## Findings

- Opening a folder synchronously extracted shell icons and decoded custom images on the UI thread. In the initial separate-process run, the first two folder openings occupied the UI for 123.6 and 129.3 ms.
- Every pointer transition reconstructed gradients/effects and allocated new open/close timers. It also started scale animation clocks and changed Korean title/note text for transitions that could finish before the next rendered frame.
- Every hover panel reparsed the full settings XAML stylesheet, even though its owner had already loaded those styles.
- The existing memory trim is gated to hidden windows. These measurements did not identify it as the cause of repeated visible hover stalls.

## Changes

- Load shell icons and custom image files on a serial STA dispatcher with COM initialization. Freeze image sources before transfer; apply completion at background UI priority. Coalesce concurrent requests for the same icon and reject stale completions after editing or closing.
- Reuse two hover timers, prebuild normal/hover appearances, and merge note/scale requests once per render turn. Normal hover still uses the same scale and duration.
- Reuse the owner's parsed stylesheet for folder panels while applying the current palette locally.

## Reproduction and measurements

`DeckLauncher.exe --perftest <copied Data directory> <output directory>` opens offscreen test windows, opens each root folder twice, then raises 1,000 enter/leave pairs three times while pumping layout every 16 pairs. The same harness was run in two separate processes for each implementation. These are six steady-state samples per implementation, using a copy of the maintainer's 44 registrations; private names, targets and images are not included in this document.

| Median over six samples | Before | After |
|---|---:|---:|
| Synchronous hover input dispatch, 1,000 enter/leave pairs | 264.6 ms | 41.5 ms |
| Allocations on the UI thread for the full hover/layout sweep | 28.19 MB | 6.60 MB |

Icon completion is awaited before the optimized steady-state sweep. Startup/folder display can now show placeholders until icons arrive; reduced UI occupancy is not a claim that all file I/O has finished sooner. The machine reported hardware rendering tier 2. These are synthetic input/layout measurements, not on-screen FPS, actual pointer traces, or a reboot/cold-cache benchmark. First-launch WPF, font and graphics initialization still has a cost; post-reboot behavior needs a user check.

## Validation

281 Windows checks passed, including a deliberately blocked icon reader with a still-responsive UI dispatcher, STA/frozen-image transfer, live icon completion, final hovered note selection, cancellation on navigation, and current folder palette. Existing folder, shortcut, opacity and taskbar-icon checks also passed. Personal Data was not edited by this work.
