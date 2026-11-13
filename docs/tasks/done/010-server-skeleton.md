# 010 — Server project skeleton and local dev run

**Owner:** backend-engineer
**Size:** M
**Status:** done
**GDD:** docs/gdd/GDD.md#technical-strategy (Stack), #where-combat-runs,
#scaling-and-cost-control
**Depends on:** none

## Goal
A C# server project lives in the repo, runs on Miles' machine with one command, and
answers a health check the Unity Editor can reach, so later tasks have somewhere to put
profile and fight endpoints.

## Acceptance criteria
- [ ] Server source under `Server/` at the repo root (outside `Assets/`, so Unity doesn't
      import it): an ASP.NET Core (.NET 10) web project with a Dockerfile that builds and
      runs it in a container, plus a server test project (xUnit or NUnit).
- [ ] `tools/run-server-dev.sh` starts it locally with a Postgres container (Docker
      Compose: server + `postgres`, data in a named volume); the
      server reads all config (port, connection strings, signing keys) from environment
      variables, with a committed `.env.example` and no secrets in git (`.gitignore`
      updated).
- [ ] `GET /health` returns the server version, server UTC time and whether the database
      is reachable.
- [ ] `tools/run-server-tests.sh` runs the server tests headless and exits non-zero on
      failure; at least the health test exists and passes.
- [ ] The server project is set up to reference the CombatCore source from
      `Assets/Scripts/CombatCore/` without copying it (e.g. linked source files), even if
      008 hasn't landed yet (an empty or stub reference is fine; 016 wires it).
- [ ] `CLAUDE.md` Build & Test Commands lists the two new scripts (one line each).
- [x] (manual, Miles) Run `tools/run-server-dev.sh` and open the health URL.

## Context
- Memory: memory/systems.md (Save and persistence), `.claude/agents/backend-engineer.md`
- May read: GDD sections above, `tools/run-editmode-tests.sh` (script style),
  `.gitignore`, `.gitattributes`
- May write: `Server/` (new), `tools/run-server-dev.sh`, `tools/run-server-tests.sh`,
  `.gitignore`, `CLAUDE.md` (commands section only)

## Out of scope
- Cloud hosting, deploy config, CI, budget alert (hosting is chosen after the local
  slice works), any game endpoint, database tables (012).

## Notes
- Decisions (Miles): self-hosted ASP.NET Core (.NET 10) in a container, sharing the C#
  combat library (Q1); managed Postgres in production, Docker Postgres for local dev
  (Q2); hosting chosen after the local slice works, leaning Azure Container Apps, and
  nothing in this slice deploys (Q4). Keep the container stateless and config in env
  vars so it can move to Container Apps later without code changes.
- Postgres access: Npgsql (EF Core or Dapper, agent's choice; say which in the report).
  Migrations or schema scripts live under `Server/` and run on startup in dev.
