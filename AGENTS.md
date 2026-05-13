# Agent Operating Guide

This project uses `context/*.md` files as durable project memory. Read them before design, implementation, review, or documentation work.

## Language Rules

- Talk with the user in Spanish unless they ask otherwise.
- Write code, comments, commits, project context, and documentation in English.
- End-user product UI strings are Spanish (es-AR).

## Source Of Truth

Read `context/README.md` first. It defines the project reading order and precedence rules.

The most important implementation references are:

- `context/project-overview.md`
- `context/architecture.md`
- `context/code-standards.md`
- `context/rag-spec.md`
- `context/ui-context.md`
- `context/code-patterns.md`
- `context/ai-workflow-rules.md`
- `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`

## Human-In-The-Loop Implementation Protocol

The user wants active participation during MVP implementation. Do not treat implementation as a fully autonomous background task.

For every implementation task:

1. Split the task into `Agent-owned` and `User-owned` steps before executing.
2. Give the user concrete commands to run when local setup, tool installation, Docker, dependency installation, project scaffolding, service startup, or secret creation is involved.
3. For each user command, state the expected result and what output or confirmation the user should report back.
4. Keep agent-owned edits scoped to code, docs, configuration, and verification that can be done safely from the workspace.
5. Do not create or commit real secrets. Secret file creation and secret value decisions are always user-owned.
6. Stop at checkpoints when a user-owned command result is required before the next safe step.
7. Update `context/progress-tracker.md` after each meaningful phase or checkpoint.
8. Update `context/design-decisions.md` when a significant architecture, workflow, stack, data, security, deployment, or product decision changes.

## Execution Rules

- Follow the MVP implementation plan task by task.
- Use small commits, each scoped to one task or coherent subtask.
- Prefer TDD for application behavior: failing test, minimal implementation, passing test, refactor.
- Keep service boundaries strict: .NET owns `app`, FastAPI owns `rag`, frontends call their assigned same-origin APIs.
- Use `uv` for Python/FastAPI dependency management.
- Use `pnpm` for frontend workspace dependency management.
- Do not introduce unapproved libraries without updating `context/design-decisions.md`.
- Do not proceed through failing baseline verification without documenting the failure and getting the user's decision.

## Current Plan

The active implementation roadmap is:

- `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`

Start with Task 0 after confirming the Git workspace and branch/worktree strategy.
