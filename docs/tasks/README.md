# Task Board

Each unit of work is one file here: `NNN-short-slug.md` (e.g. `004-hold-to-commit.md`).
The producer (or Miles) writes them; the owner agent builds them; qa-tester tests them;
Miles merges. Merged specs move to `done/`.

## Template

```markdown
# NNN — Title

**Owner:** gameplay-engineer | ui-engineer | ui-stylist | content-designer |
combat-balance-analyst | economy-balance-analyst | qa-tester | release-engineer |
backend-engineer
**Size:** S | M | L
**Status:** todo | in-progress | review | done
**GDD:** docs/gdd/GDD.md#<section>
**Depends on:** NNN (or none)

## Goal
One or two sentences on the player-facing outcome.

## Acceptance criteria
- [ ] Testable, observable statements. Logic criteria must be covered by EditMode tests.
- [ ] Editor/device-only checks are marked (manual).

## Context
- Memory: memory/systems.md, … (only what's needed)
- May read: exact files/folders the agent needs
- May write: files/folders it is expected to change

## Out of scope
What this task explicitly does not do.

## Notes
Existing code to reuse, constraints, open questions for Miles.
```

Keep specs small: one system or one screen, finishable in one agent session.
