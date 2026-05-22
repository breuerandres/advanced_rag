# v2 Refactor Progress Tracker

Mirror of `docs/v2/03-phases.md` checklists but maintained as a journal. The MVP
`progress-tracker.md` is **frozen** as historical record; do not edit it.

## Current state at handoff

- Branch: `feature/v2-generic`
- Baseline commit on `main`: `chore: import existing MVP working tree as baseline`
- Operator: andresbr (this PC) → next PC handover pending
- Tests run: none (per user instruction)
- Dependencies installed: none

## Phase 0 — Preparation

- 2026-05-22 — `git init` on the working copy; baseline commit on `main`.
- 2026-05-22 — Branch `feature/v2-generic` created.
- 2026-05-22 — `HANDOFF.md` authored.
- 2026-05-22 — `docs/v2/*` (6 files) authored.
- 2026-05-22 — `docs/adr/0001–0010` (10 ADRs) authored.
- 2026-05-22 — `context/v2-overview.md` and this `context/v2-progress.md` created.
- 2026-05-22 — Appended v2 entries to `context/design-decisions.md`.
- 2026-05-22 — **Commit**: `docs: introduce v2 handoff documentation and ADRs`.

## Phase 1 — Foundations generic

Pending until first commit from the next PC.

Items expected per `docs/v2/03-phases.md` §Phase 1.

## Phase 1.5 — Unified auth + design system base

Pending.

## Phase 1.7 — UX refactor per SPA

Pending.

## Phase 2 — Hybrid retrieval + dimensions

Pending.

## Phase 3 — Object storage + bulk import

Pending.

## Phase 4 — CdA features

Pending.

## Phase 5 — Quality

Pending.

## Phase 6 — Ship readiness

Pending.

## Open questions

See `docs/v2/open-questions.md`. As of handoff, OQ-001 through OQ-010 are all open.

## Notes for the next operator

- After bringing the MVP baseline up on the new PC, run the full existing test suite
  before touching v2 work. Mark which tests are red because of v2 partial scaffolds vs
  pre-existing issues.
- Phase order in `docs/v2/03-phases.md` is recommended but not strictly required;
  internal dependencies are noted per task. Some Phase 1 and Phase 1.5 tasks can be done
  in parallel.
- When a phase task completes, add a journal entry to this file under the matching
  section with the date and the commit hash.
- When an open question is resolved, move its entry from `docs/v2/open-questions.md` to
  the `## Resolved` section there with the chosen option and date.
