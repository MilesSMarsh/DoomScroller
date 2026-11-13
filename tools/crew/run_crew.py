#!/usr/bin/env python3
"""Doom Scroller agent crew orchestrator.

Runs the crew defined in .claude/agents/ as a pipeline, one headless Claude Code
call per agent (`claude -p --agent <name>`), handing each agent's output to the next:

    producer  -> writes a task spec in docs/tasks/ and names the building agent
    builder   -> gameplay-engineer / ui-engineer / content-designer builds it
    qa-tester -> writes + runs EditMode tests, reports PASS or FAIL
       FAIL   -> report goes back to the SAME builder session (keeps context);
                 after MAX_QA_ROUNDS failed rounds, escalate to Miles
    (Miles reviews and commits)
    producer  -> post-merge: updates memory/, moves the spec to docs/tasks/done/

Agents run one at a time (the project allows at most two, and only one Unity
process can touch the project). Every agent's report is saved under
docs/crew-runs/<timestamp>/ so a run can be audited.

Usage:
    tools/crew/run_crew.py "Find one small Must-scope GDD gap and spec it"
    tools/crew/run_crew.py --spec docs/tasks/003-foo.md          # skip planning
    tools/crew/run_crew.py --post-merge docs/tasks/003-foo.md    # after Miles merges
    add --yes to skip the approval pause, --dry-run to print the calls only.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUNS_DIR = ROOT / "docs" / "crew-runs"
BUILDERS = {"gameplay-engineer", "ui-engineer", "content-designer", "backend-engineer",
            "ui-stylist", "release-engineer"}
MAX_QA_ROUNDS = 2  # .claude/rules/agent-workflow.md: escalate after two failed rounds

FIVE_LINE = "Report in the five-line format from CLAUDE.md."
UNITY_NOTE = (
    "If the Unity Editor has the project open, run tests through Unity MCP and "
    "afterwards make sure Assets/Scenes/SampleScene.unity is still the open scene."
)


class CrewError(RuntimeError):
    pass


STATE_FILE = ROOT / "tools" / "crew" / ".crew-state.json"  # git-ignored; read by dashboard.py


def write_state(**fields) -> None:
    """Record what the crew is doing right now, for tools/crew/dashboard.py."""
    try:
        state = json.loads(STATE_FILE.read_text()) if STATE_FILE.exists() else {}
    except (OSError, json.JSONDecodeError):
        state = {}
    state.update(fields, pid=os.getpid(), updated=dt.datetime.now().isoformat(timespec="seconds"))
    STATE_FILE.write_text(json.dumps(state, indent=2))


def set_spec_status(spec: str, status: str) -> None:
    """Update the '**Status:** ...' line of a spec (todo | in-progress | review | done)."""
    path = ROOT / spec
    if not path.is_file():
        return
    text = path.read_text()
    new_text, n = re.subn(r"(\*\*Status:\*\*\s*)[A-Za-z-]+", rf"\g<1>{status}", text, count=1)
    if n:
        path.write_text(new_text)


class Crew:
    def __init__(self, claude: str, permission_mode: str, dry_run: bool, run_dir: Path):
        self.claude = claude
        self.permission_mode = permission_mode
        self.dry_run = dry_run
        self.run_dir = run_dir
        self.step = 0

    def call(self, agent: str, prompt: str, resume: str | None = None) -> tuple[str, str | None]:
        """Run one agent headlessly. Returns (report text, session id)."""
        self.step += 1
        cmd = [self.claude, "-p", "--agent", agent, "--output-format", "json",
               "--permission-mode", self.permission_mode]
        if resume:
            cmd += ["--resume", resume]
        cmd.append(prompt)
        label = f"{self.step:02d}-{agent}"
        print(f"\n=== [{label}] {'(resumed) ' if resume else ''}running...", flush=True)
        if not self.dry_run:
            write_state(running=True, agent=agent, step=label, step_started=dt.datetime.now().isoformat(timespec="seconds"),
                        run_dir=str(self.run_dir.relative_to(ROOT)))
        if self.dry_run:
            print("    " + " ".join(cmd[:-1]) + f" '<{len(prompt)} char prompt>'")
            return f"(dry run: {agent})", None

        proc = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
        if proc.returncode != 0:
            raise CrewError(f"{agent} exited {proc.returncode}: {proc.stderr.strip()[:500]}")
        try:
            data = json.loads(proc.stdout)
        except json.JSONDecodeError as e:
            raise CrewError(f"{agent} returned non-JSON output: {proc.stdout[:300]}") from e
        if data.get("is_error"):
            raise CrewError(f"{agent} reported an error: {data.get('result', '')[:500]}")

        report = (data.get("result") or "").strip()
        (self.run_dir / f"{label}.md").write_text(f"# {label}\n\n## Prompt\n\n{prompt}\n\n## Report\n\n{report}\n")
        print(report)
        return report, data.get("session_id")


def tag(report: str, name: str) -> str | None:
    """Read a machine-readable trailer line such as 'SPEC: docs/tasks/003.md'."""
    m = re.findall(rf"^\s*{name}:\s*(\S+)", report, re.MULTILINE)
    return m[-1].strip("`") if m else None


def approve(question: str, auto_yes: bool) -> bool:
    if auto_yes:
        return True
    try:
        return input(f"\n{question} [y/N] ").strip().lower() in {"y", "yes"}
    except EOFError:  # non-interactive (no stdin): treat as "not approved yet"
        print("(no input available; not approved)")
        return False


def plan(crew: Crew, request: str) -> tuple[str, str]:
    if not crew.dry_run:
        write_state(spec=None, outcome=None, request=request)
    report, _ = crew.call("producer", (
        f"Request from Miles: {request}\n\n"
        "Write ONE small task spec in docs/tasks/ (follow docs/tasks/README.md), Must scope "
        f"only. {FIVE_LINE} Then end with exactly two lines:\n"
        "SPEC: <repo-relative path of the spec>\n"
        "AGENT: <" + " | ".join(sorted(BUILDERS)) + ">"
    ))
    spec, agent = tag(report, "SPEC"), tag(report, "AGENT")
    if crew.dry_run:
        return "docs/tasks/NNN-dry-run.md", "gameplay-engineer"
    if not spec or not (ROOT / spec).is_file():
        raise CrewError(f"producer did not name an existing spec (got {spec!r})")
    if agent not in BUILDERS:
        raise CrewError(f"producer named an unknown builder {agent!r}")
    return spec, agent


def builder_for(spec: str) -> str:
    text = (ROOT / spec).read_text() if (ROOT / spec).is_file() else ""
    owner = re.search(r"\*\*Owner:\*\*\s*`?([a-z-]+)", text)
    if owner and owner.group(1) in BUILDERS:
        return owner.group(1)
    for name in BUILDERS:
        if re.search(rf"\b{name}\b", text):
            return name
    return "gameplay-engineer"


def build_and_verify(crew: Crew, spec: str, builder: str) -> bool:
    if not crew.dry_run:
        set_spec_status(spec, "in-progress")
        write_state(spec=spec, outcome=None)
    _, build_session = crew.call(builder, (
        f"Build the task in {spec}. Read only the files the spec names. {UNITY_NOTE} "
        f"Don't commit. {FIVE_LINE}"
    ))

    for qa_round in range(1, MAX_QA_ROUNDS + 1):
        qa_report, _ = crew.call("qa-tester", (
            f"Verify {spec} against its acceptance criteria. Add EditMode tests for "
            f"uncovered criteria, then run the full suite. {UNITY_NOTE} {FIVE_LINE} "
            "Then end with exactly one line: RESULT: PASS or RESULT: FAIL"
        ))
        result = (tag(qa_report, "RESULT") or "").upper()
        if crew.dry_run or result == "PASS":
            print(f"\n>>> QA passed (round {qa_round}).")
            if not crew.dry_run:
                set_spec_status(spec, "review")
                write_state(running=False, outcome="qa-passed")
            return True
        if qa_round == MAX_QA_ROUNDS:
            break
        print(f"\n>>> QA failed (round {qa_round}); sending the report back to {builder}.")
        _, build_session = crew.call(builder, (
            f"QA found failures on {spec}. Fix the game code (not the tests, unless a test "
            f"is wrong; say so if it is). QA report:\n\n{qa_report}\n\n{FIVE_LINE}"
        ), resume=build_session)

    print(f"\n>>> QA failed {MAX_QA_ROUNDS} rounds; escalating to Miles. See {crew.run_dir}.")
    if not crew.dry_run:
        set_spec_status(spec, "review")
        write_state(running=False, outcome="escalated")
    return False


def post_merge(crew: Crew, spec: str) -> None:
    crew.call("producer", (
        f"Miles approved and merged {spec}. Update memory/systems.md (and memory/content.md "
        "if content changed) to describe the current state, set the spec's status to done "
        f"and move it to docs/tasks/done/. {FIVE_LINE}"
    ))


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("request", nargs="?", help="what Miles wants; the producer turns it into a spec")
    ap.add_argument("--spec", help="skip planning and build this existing spec")
    ap.add_argument("--post-merge", metavar="SPEC", help="run only the producer's post-merge step")
    ap.add_argument("--yes", action="store_true", help="don't pause for Miles' approval of the spec")
    ap.add_argument("--dry-run", action="store_true", help="print the agent calls without running them")
    ap.add_argument("--permission-mode", default="auto",
                    help="Claude Code permission mode for each agent (default: auto)")
    ap.add_argument("--claude", default=os.environ.get("CLAUDE_BIN", "claude"), help="claude executable")
    args = ap.parse_args()
    if not (args.request or args.spec or args.post_merge):
        ap.error("give a request, --spec, or --post-merge")

    run_dir = RUNS_DIR / dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    if not args.dry_run:
        run_dir.mkdir(parents=True, exist_ok=True)
    crew = Crew(args.claude, args.permission_mode, args.dry_run, run_dir)

    try:
        if args.post_merge:
            post_merge(crew, args.post_merge)
            if not args.dry_run:
                write_state(running=False, spec=args.post_merge, outcome="merged")
            return 0

        if args.spec:
            spec, builder = args.spec, builder_for(args.spec)
        else:
            spec, builder = plan(crew, args.request)
            if not approve(f"Producer wrote {spec} for {builder}. Build it?", args.yes):
                if not args.dry_run:
                    write_state(running=False, spec=spec, outcome="awaiting-approval")
                print("Stopped before building. Edit the spec and rerun with --spec.")
                return 0

        passed = build_and_verify(crew, spec, builder)
        if passed:
            print(f"\nReady for Miles' review. After merging: tools/crew/run_crew.py --post-merge {spec}")
        return 0 if passed else 1
    except CrewError as e:
        print(f"\nCrew stopped: {e}", file=sys.stderr)
        if not args.dry_run:
            write_state(running=False, outcome=f"crashed: {e}"[:300])
        return 2


if __name__ == "__main__":
    sys.exit(main())
