# GitHub Copilot Instructions

## Primary instruction source

Use the repository root [`AGENTS.md`](../AGENTS.md) as the **primary** source of truth for behavior, architecture context, testing standards, and completion criteria.

If this file and `AGENTS.md` appear to conflict, prefer `AGENTS.md` unless this file explicitly states a GitHub Copilot-only exception.

## Copilot-specific guidance

This file should only contain **GitHub Copilot-specific** instruction details.
Keep product, architecture, and general engineering standards centralized in `AGENTS.md`.

## Operating expectations for Copilot

- Apply the `AGENTS.md` testing bar strictly (TUnit/TUnit.Mocks, AAA comments, naming, cancellation token rule).
- Treat work as incomplete until relevant tests pass.
- Before specialist work, load the relevant installed skill from `.agents/skills/` (committed, with `skills-lock.json`); `AGENTS.md` ("Installed skills") lists each one, when to load it, and how to add or update them. Install only into `.agents/` (`-a universal`), never into `.claude/` or another per-agent folder.
- Keep edits minimal, focused, and aligned with existing SDK and repository conventions.
