# Agent Operating Guide

This is a thin pointer. The authoritative, full operating guide for any AI assistant on this
project lives in **`CLAUDE.md`** (repo root). Read it first. This file exists so that agents and
tools that look for `AGENTS.md` are routed to the same single source of truth and do not drift.

## Read first

1. **`CLAUDE.md`** — project context system, language rules, Human-in-the-Loop protocol,
   architecture, common commands, and library/convention guardrails.
2. **`context/README.md`** — defines the reading order and precedence rules for the `context/*.md`
   durable-memory files (the source of truth for product behavior, technical rules, and state).

## Non-negotiables (summary — full text in `CLAUDE.md`)

- **Language:** converse with the user in Spanish; all code, comments, commits, PRs, logs, and
  docs in English; end-user UI strings bilingual es-AR (default) / en-US via i18next.
- **Source of truth:** `context/*.md` over chat. Significant decisions go in
  `context/design-decisions.md`; status updates go in `context/progress-tracker.md`.
- **Human-in-the-Loop:** split each task into Agent-owned vs User-owned steps; give the user
  concrete commands and expected results; stop at checkpoints; never create or commit real secrets.
- **Service boundaries:** `.NET` owns the `app` schema, FastAPI owns the `rag` schema — never
  cross-write. Frontends call only their assigned same-origin `/api/*` backend.

## Current work

Status, active tasks, open questions, and the next safe step are tracked in
**`context/progress-tracker.md`** — consult it instead of any hardcoded plan reference.
