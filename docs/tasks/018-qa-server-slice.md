# 018 — QA: server playtest slice end to end

**Owner:** qa-tester
**Size:** M
**Status:** todo (blocked on 017)
**GDD:** docs/gdd/GDD.md#persistence-and-save-rules
**Depends on:** 017

## Goal
Confirm the server slice keeps progress and never rerolls a fight, and give Miles a
short manual playtest checklist.

## Acceptance criteria
- [ ] EditMode and server tests cover each acceptance criterion of 011, 012, 013, 016
      and 017 that is testable without a device; add missing ones, report gaps.
- [ ] Full runs pass: `tools/run-editmode-tests.sh` and `tools/run-server-tests.sh`
      (server tests against the local Docker Postgres).
- [ ] A failure report per failing criterion (file, criterion, observed vs. expected).
- [ ] A manual checklist for Miles (in the report, not a file): fresh install, reinstall
      keeps progress, force-quit mid-fight replays the same fight, server stopped →
      overlay → Retry, device clock change doesn't affect regen.

## Context
- Memory: memory/systems.md (Save and persistence)
- May read: specs 011–017, `Server/` tests, `Assets/Tests/EditMode/`,
  `Assets/Scripts/Network/`
- May write: `Assets/Tests/EditMode/`, `Server/` test project only

## Out of scope
- Load testing, device builds, hosting.

## Notes
- Known and accepted for this slice (don't report as failures): rewards, shops and
  crafting are client-authoritative and the client uploads the whole save (Miles, Q6);
  sign-in is anonymous by install id (Q3); nothing is deployed (Q4).
