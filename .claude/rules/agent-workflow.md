# Agent Workflow

Roster and responsibilities: `docs/gdd/GDD.md#ai-architecture`. Definitions:
`.claude/agents/`. Miles is creative director and approves every merge.

| Agent | Model | Writes |
|---|---|---|
| `producer` | opus | task specs, `memory/systems.md`, `memory/content.md` |
| `gameplay-engineer` | sonnet | game-system C# + tests |
| `ui-engineer` | sonnet | UI scripts, screens, prefabs (function) |
| `ui-stylist` | sonnet | colors, fonts, sprites, layout values (look) |
| `content-designer` | sonnet | data assets under `Assets/Resources/` |
| `combat-balance-analyst` | sonnet | recommendations in `docs/balance/` |
| `economy-balance-analyst` | sonnet | economy table + recommendations in `docs/balance/` |
| `qa-tester` | haiku | EditMode tests, failure reports |
| `release-engineer` | sonnet | build scripts, IAP code, store material |
| `backend-engineer` | sonnet | server code, data model, networking |

## Routes

- **Direct:** Miles gives a task straight to one agent.
- **Via Producer:** a bigger request or GDD change goes to `producer`, which writes specs
  in `docs/tasks/`. The main session shows Miles the list and questions before building.

A larger feature flows: spec → gameplay-engineer / ui-engineer / content-designer build →
ui-stylist → balance analysts → qa-tester → Miles reviews and merges → producer updates
`memory/` and moves the spec to `done/`. Skip steps that don't apply.

When QA reports failures, send them back to the building agent (continue the same agent
so it keeps context). After two failed rounds, escalate to Miles.

## Limits

- **At most two agents run at once** (usage limits).
- Only one Unity process per project folder (Editor or batch mode), so two agents that
  both need to run tests or touch the Editor can't run at the same time. Pair a Unity
  agent with a read-only one (producer, an analyst) instead.
- Each spec names the files its agent may read; agents don't explore beyond them
  without reason. `CLAUDE.md` and `memory/` files stay short and describe the current
  game, never a history.

## Status and dashboard

- A building agent sets its spec's `**Status:**` to `in-progress` when it starts and
  `review` when it reports; the dashboard sets `approved` when Miles approves; the producer sets `done` post-merge. Questions for Miles go
  in the spec's Notes, one per line: `- Question for Miles: <the question>` (omit the line
  entirely if there are none; never write "none" or a bare heading); manual checks are `- [ ] (manual, Miles)`.
- `tools/crew/dashboard.py` (http://localhost:8090) reads those fields plus
  `run_crew.py`'s live state to show Miles what needs him and what is running.

## Verification

- `tools/run-editmode-tests.sh [filter]` — headless EditMode tests (~2.5 min). Exits 2 if
  the Editor has the project open; use Unity MCP then.
- After an MCP test run, check the Editor still has `Assets/Scenes/SampleScene.unity`
  open (test runs can leave an empty untitled scene). If not, reload it with
  `manage_scene` `load` — otherwise Miles presses Play and sees nothing.
- Editor/device-only checks are listed as manual for Miles, never claimed as verified.
- Agents don't commit; the main session commits after Miles approves.
