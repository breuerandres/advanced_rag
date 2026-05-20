# AI Workflow Rules

## Approach

Build this project incrementally using a spec-driven workflow. Context files define what to build, how to build it, and the current state of progress. Do not infer durable behavior from chat alone.

During brainstorming, persist meaningful product and architecture decisions immediately in the relevant `context/*.md` file. Significant decisions must also be summarized in `context/design-decisions.md` with rationale and tradeoffs.

Before clearing or resuming a chat session, update `context/progress-tracker.md` with the latest completed decisions, open questions, and a handoff section that identifies the next safe step.

## Human-In-The-Loop Implementation

The user wants active participation during implementation. Agents must make the work collaborative instead of fully autonomous.

For every implementation task:

- Split work into `Agent-owned` and `User-owned` steps before execution.
- Give the user concrete commands for local setup, dependency installation, Docker/Compose checks, project scaffolding, service startup, and secret-file preparation when those actions are relevant.
- Include the expected result for each user command and ask the user to report the specific output or confirmation needed for the next step.
- Keep agent-owned changes focused on code, documentation, configuration, and verification that can be performed from the workspace.
- Treat real secret creation and secret value choices as user-owned work. Do not invent or commit real secrets.
- Stop at checkpoints when a user-owned command result is needed before the next safe step.
- Record meaningful progress and verification results in `context/progress-tracker.md`.

## Scoping Rules

- Work on one feature unit at a time.
- Prefer small, verifiable increments over large speculative changes.
- Do not combine unrelated system boundaries in a single implementation step.
- Keep the current architecture decisions in `context/architecture.md` as the source of truth.
- Keep product behavior and user flows in `context/project-overview.md`.
- Keep workflow status, open questions, and next steps in `context/progress-tracker.md`.

## Brainstorming Rules

- Ask one design question at a time when requirements are still being shaped.
- After a decision is made, persist it before moving to the next major topic.
- Do not start implementation until the design section and written spec are approved.
- If a design decision changes an existing context file, update that file immediately.
- If a decision affects final documentation, add or update an entry in `context/design-decisions.md`.

## When to Split Work

Split an implementation step if it combines:

- Frontend changes and backend contract changes that cannot be verified together quickly.
- Multiple unrelated API routes or bounded contexts.
- Database migrations across both `app` and `rag` schemas.
- Public chat behavior and internal indexing behavior.
- Authentication/authorization changes plus unrelated UI polish.
- Behavior not clearly defined in the context files.

If a change cannot be verified end to end quickly, the scope is too broad and must be split.

## Handling Missing Requirements

- Do not invent product behavior not defined in the context files.
- If a requirement is ambiguous, resolve it in the relevant context file before implementing.
- If a requirement is missing, add it as an open question in `context/progress-tracker.md` before continuing.
- Prefer explicitly documenting a conservative default over leaving behavior implicit.

## Protected Files

Do not modify these without explicit document:

- Third-party library internals.
- Generated dependency lockfiles except through the relevant package manager.
- Production secret files or real customer `.env` files.
- Database migration history after it has been applied to a shared environment.

## Keeping Docs In Sync

Update the relevant context file whenever decisions or implementation change:

- System architecture, storage, auth, routing, deployment, or invariants: `context/architecture.md`.
- Product purpose, roles, user flows, scope, and success criteria: `context/project-overview.md`.
- Coding conventions, API rules, and file organization: `context/code-standards.md`.
- Visual direction, UI states, components, and layout rules: `context/ui-context.md`.
- Current status, next steps, blockers, and open questions: `context/progress-tracker.md`.
- Rationale for significant decisions: `context/design-decisions.md`.

## Before Moving To The Next Unit

1. The current unit works end to end within its defined scope.
2. No invariant defined in `context/architecture.md` was violated.
3. `context/progress-tracker.md` reflects the completed work and next step.
4. Relevant context files have been updated.
5. Verification commands for the touched stack pass, or failures are documented.
