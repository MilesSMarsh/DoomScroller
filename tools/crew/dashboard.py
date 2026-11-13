#!/usr/bin/env python3
"""Doom Scroller crew dashboard: what needs Miles, what's being worked on, what's next.

Reads the repo: task specs in docs/tasks/ (Status / Owner / Depends on, unticked
"(manual, Miles)" checks, open questions), the orchestrator's live state file
(tools/crew/.crew-state.json, written by run_crew.py), docs/crew-runs/ logs, and git.

Interactive: click a card to open the spec; answer questions, pass/fail manual checks,
approve or request changes, or leave a note. Input is written into the spec itself
(where agents read it) and appended to tools/crew/inbox.jsonl for the main session.

It can also start work: "Start" on a ready task runs `run_crew.py --spec <spec> --yes`
(build -> QA) in the background, "Plan" sends a request to the producer (stops for your
approval), and "Stop" ends the run. Output goes to tools/crew/crew.log.

The chat panel talks to a headless Claude Code session (`claude -p --resume`, permission
mode auto) in the repo; its history is in tools/crew/.chat.json, and the session can be
picked up in a terminal with `claude --resume <id>`. The dashboard never commits.

    tools/crew/dashboard.py            # serve at http://localhost:8090
    tools/crew/dashboard.py --port N
    tools/crew/dashboard.py --once     # print a text summary and exit
"""

from __future__ import annotations

import argparse
import datetime as dt
import html
import json
import os
import re
import signal
import subprocess
import sys
import threading
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[2]
TASKS = ROOT / "docs" / "tasks"
RUNS = ROOT / "docs" / "crew-runs"
STATE_FILE = ROOT / "tools" / "crew" / ".crew-state.json"
INBOX = ROOT / "tools" / "crew" / "inbox.jsonl"
CREW_LOG = ROOT / "tools" / "crew" / "crew.log"          # git-ignored
CHAT_FILE = ROOT / "tools" / "crew" / ".chat.json"        # git-ignored
CHAT_ERR = ROOT / "tools" / "crew" / ".chat-stderr.log"   # git-ignored
RUN_CREW = ROOT / "tools" / "crew" / "run_crew.py"
CLAUDE_BIN = os.environ.get("CLAUDE_BIN", "claude")
BOOT_ID = dt.datetime.now().isoformat()  # changes on restart; open pages reload to pick up new code

QUESTION_RE = re.compile(r"(?i)(open question|questions? for miles|needs miles|awaiting miles)")
RESOLVED_RE = re.compile(r"(?i)(decided|approved|resolved|answered|no open questions?|none remain)")
STATUSES = ("todo", "in-progress", "review", "changes-requested", "done")


@dataclass
class Item:
    line: int   # 0-based line index in the spec file
    text: str


@dataclass
class Spec:
    num: str
    title: str
    path: Path
    status: str
    owner: str
    depends: list[str]
    body: str
    manual_open: list[Item] = field(default_factory=list)
    questions: list[Item] = field(default_factory=list)
    criteria_done: int = 0
    criteria_total: int = 0

    @property
    def rel(self) -> str:
        return str(self.path.relative_to(ROOT))

    @property
    def short_title(self) -> str:
        return re.sub(r"^\d+\s*[—-]\s*", "", self.title)


def _field(text: str, name: str) -> str:
    m = re.search(rf"\*\*{name}:\*\*\s*(.+)", text)
    return m.group(1).strip() if m else ""


def spec_paths() -> list[Path]:
    return sorted(list(TASKS.glob("[0-9]*.md")) + list((TASKS / "done").glob("[0-9]*.md")))


# Dashboard/agent bookkeeping bullets: never questions themselves, and they end a question list.
BOOKKEEPING_RE = re.compile(r"^\s*- (Answered|Note|Approved|Approval withdrawn|Manual check|Changes requested|Decided)\b")
EMPTY_ANSWER_RE = re.compile(r"(?i)^(none|n/?a|no(ne)?\.?|nothing|-+|no questions?\.?)?$")


def _with_continuation(lines: list[str], i: int) -> tuple[str, int]:
    """The line plus its wrapped continuation lines (indented, not a new bullet)."""
    text, j = lines[i].strip(" -*"), i + 1
    while j < len(lines) and lines[j].startswith("  ") and lines[j].strip() and not lines[j].lstrip().startswith("- "):
        text += " " + lines[j].strip()
        j += 1
    return text, j


def _answered(lines: list[str], i: int, end: int) -> bool:
    """Resolved on the line itself, or an 'Answered' sub-bullet follows it."""
    if RESOLVED_RE.search(lines[i]):
        return True
    return end < len(lines) and lines[end].startswith("  ") and BOOKKEEPING_RE.match(lines[end]) is not None


def parse_questions(lines: list[str]) -> list[Item]:
    """Open questions for Miles. Handles 'Questions for Miles: <q>' on one line, a bare
    'Questions for Miles:' heading followed by bulleted questions, and ignores
    'Questions for Miles: none' (and other empty answers)."""
    out: list[Item] = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if not QUESTION_RE.search(line) or BOOKKEEPING_RE.match(line):
            i += 1
            continue
        text, end = _with_continuation(lines, i)
        m = QUESTION_RE.search(text)
        rest = re.sub(r"(?i)\(answered below\)", "", text[m.end():]).strip(" :*-—")
        if rest and not EMPTY_ANSWER_RE.match(rest):
            if not _answered(lines, i, end):
                out.append(Item(i, text))
            i = end
            continue
        if rest:  # "none", "n/a", ...
            i = end
            continue
        # Bare heading: the questions are the bullets under it, up to a blank line or section.
        j = end
        while j < len(lines) and lines[j].strip() and not lines[j].startswith("#"):
            if not lines[j].lstrip().startswith("- "):
                j += 1
                continue
            if BOOKKEEPING_RE.match(lines[j]):
                if not lines[j].startswith("  "):
                    break  # a top-level dashboard note ends the list
                j += 1
                continue
            q, k = _with_continuation(lines, j)
            if not _answered(lines, j, k):
                out.append(Item(j, q))
            j = k
        i = max(j, end)
    return out


def load_specs() -> list[Spec]:
    specs = []
    for path in spec_paths():
        text = path.read_text(errors="replace")
        lines = text.splitlines()
        title = lines[0].lstrip("# ").strip() if lines else path.stem
        status = (_field(text, "Status").split() or ["todo"])[0].strip("*").lower()
        if path.parent.name == "done":
            status = "done"
        owner = (_field(text, "Owner").split() or ["?"])[0].strip("`*")
        depends = re.findall(r"\b(\d{3})\b", _field(text, "Depends on"))
        spec = Spec(path.name[:3], title, path, status, owner, depends, text)

        items: list[Item] = []  # checkbox criteria, wrapped continuation lines joined
        last = -2
        for i, line in enumerate(lines):
            if re.match(r"\s*- \[[ xX]\]", line):
                items.append(Item(i, line.strip()))
                last = i
            elif items and last == i - 1 and line.startswith("      ") and line.strip():
                items[-1].text += " " + line.strip()
                last = i
        spec.criteria_total = len(items)
        spec.criteria_done = sum(1 for it in items if it.text[:5].lower() == "- [x]")
        spec.manual_open = [Item(it.line, it.text[6:]) for it in items
                            if it.text.startswith("- [ ]") and "(manual" in it.text]
        if status not in ("done", "approved"):
            spec.questions = parse_questions(lines)
        specs.append(spec)
    return specs


def git(*args: str) -> str:
    try:
        return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, timeout=10).stdout
    except (OSError, subprocess.TimeoutExpired):
        return ""


def crew_state() -> dict:
    try:
        state = json.loads(STATE_FILE.read_text())
    except (OSError, json.JSONDecodeError):
        return {}
    pid = state.get("pid")
    if state.get("running") and pid:
        try:
            os.kill(int(pid), 0)
        except (OSError, ValueError):
            state["running"] = False
            state["outcome"] = state.get("outcome") or "stopped (process gone)"
    return state


def read_inbox() -> list[dict]:
    try:
        return [json.loads(l) for l in INBOX.read_text().splitlines() if l.strip()]
    except (OSError, json.JSONDecodeError):
        return []


def latest_runs(n: int = 6) -> list[tuple[str, list[str]]]:
    if not RUNS.is_dir():
        return []
    dirs = sorted((p for p in RUNS.iterdir() if p.is_dir()), reverse=True)[:n]
    return [(d.name, sorted(f.stem for f in d.glob("*.md"))) for d in dirs]


def gather() -> dict:
    specs = load_specs()
    by_num = {s.num: s for s in specs}
    state = crew_state()

    def deps_done(s: Spec) -> bool:
        return all(by_num.get(d) is None or by_num[d].status == "done" for d in s.depends)

    # A spec the crew is still working on (builder or QA running) isn't ready for Miles:
    # builders mark it `review` and add manual checks before QA has finished.
    busy = Path(state.get("spec") or "").name[:3] if state.get("running") else None

    needs: list[dict] = []
    for s in specs:
        if s.num == busy or s.status in ("in-progress", "on-hold"):
            continue
        if s.status == "review":
            needs.append({"kind": "review", "label": "Review & merge", "spec": s,
                          "text": "Built and through QA. Approve it for commit, or send it back with changes."})
        for q in s.questions:
            needs.append({"kind": "answer", "label": "Answer", "spec": s, "text": q.text, "line": q.line})
        if s.status in ("review", "done"):  # approved specs are past testing
            for m in s.manual_open:
                needs.append({"kind": "manual", "label": "Manual check", "spec": s, "text": m.text, "line": m.line})
    spec_of_state = by_num.get(Path(state.get("spec") or "000").name[:3])
    if state.get("outcome") == "escalated" and not (spec_of_state and spec_of_state.status in ("approved", "done")):
        needs.insert(0, {"kind": "escalated", "label": "Escalated", "spec": spec_of_state,
                         "text": f"QA failed twice. Logs: {state.get('run_dir', 'docs/crew-runs/')}"})
    if state.get("outcome") == "awaiting-approval":
        needs.insert(0, {"kind": "approve", "label": "Approve spec", "spec": spec_of_state,
                         "text": "The producer wrote this spec. Approve to build it."})

    return {
        "generated": dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "specs": specs,
        "needs": needs,
        "in_progress": [s for s in specs if s.status == "in-progress"],
        "approved": [s for s in specs if s.status == "approved"],
        "ready": [s for s in specs if s.status in ("todo", "changes-requested") and deps_done(s)],
        "blocked": [s for s in specs if s.status in ("todo", "changes-requested") and not deps_done(s)],
        "done": [s for s in specs if s.status == "done"],
        "on_hold": [s for s in specs if s.status == "on-hold"],
        "state": state,
        "uncommitted": [l for l in git("status", "--porcelain").splitlines() if l.strip()],
        "ahead": git("rev-list", "--count", "@{u}..HEAD").strip() or "?",
        "runs": latest_runs(),
        "inbox": read_inbox(),
    }


# ---------- writing Miles' input back ----------

def _today() -> str:
    return dt.date.today().isoformat()


def _find_spec(num: str) -> Path:
    for p in spec_paths():
        if p.name.startswith(num):
            return p
    raise ValueError(f"no spec {num}")


def _append_note(text: str, note: str) -> str:
    """Append a bullet to the end of the '## Notes' section (create it if missing)."""
    lines = text.rstrip("\n").split("\n")
    idx = next((i for i, l in enumerate(lines) if re.match(r"##\s+Notes\b", l)), None)
    if idx is None:
        return "\n".join(lines) + f"\n\n## Notes\n{note}\n"
    end = next((i for i in range(idx + 1, len(lines)) if lines[i].startswith("## ")), len(lines))
    while end > idx + 1 and not lines[end - 1].strip():
        end -= 1
    lines.insert(end, note)
    return "\n".join(lines) + "\n"


def _set_status(text: str, status: str) -> str:
    return re.sub(r"(\*\*Status:\*\*\s*)[A-Za-z-]+", rf"\g<1>{status}", text, count=1)


def _check_line(lines: list[str], line: int, expect: str) -> None:
    if not (0 <= line < len(lines)) or expect[:25].strip() not in lines[line] + " " + " ".join(lines[line + 1:line + 4]):
        raise ValueError("The spec changed since this page loaded. Refresh and try again.")


def apply_action(a: dict) -> str:
    kind, num = a.get("action"), str(a.get("spec", ""))[:3]
    text = (a.get("text") or "").strip().replace("\n", " ")
    path = _find_spec(num)
    body = path.read_text()
    lines = body.split("\n")
    who = f"(Miles, {_today()})"

    if kind == "answer":
        if not text:
            raise ValueError("Type an answer first.")
        line = int(a["line"])
        _check_line(lines, line, a.get("expect", ""))
        # Find the end of the question block, then add the answer as a sub-bullet.
        j = line + 1
        while j < len(lines) and lines[j].startswith("  ") and not lines[j].lstrip().startswith("- "):
            j += 1
        indent = re.match(r"\s*", lines[line]).group(0) + "  "
        lines.insert(j, f"{indent}- Answered {who}: {text}")
        lines[line] = lines[line].replace("Open question", "Answered question", 1) \
            if "Open question" in lines[line] else lines[line] + " (answered below)"
        body, msg = "\n".join(lines), "Answer saved to the spec."
    elif kind in ("pass", "fail"):
        line = int(a["line"])
        _check_line(lines, line, a.get("expect", ""))
        if "- [ ]" not in lines[line]:
            raise ValueError("That check is already ticked. Refresh the page.")
        if kind == "pass":
            lines[line] = lines[line].replace("- [ ]", "- [x]", 1)
            body, msg = "\n".join(lines), "Marked as passed."
            if text:
                body = _append_note(body, f"- Manual check passed {who}: {text}")
        else:
            if not text:
                raise ValueError("Say what went wrong.")
            item = re.sub(r"^\s*- \[ \]\s*\(manual[^)]*\)\s*", "", lines[line])[:80]
            body = _append_note("\n".join(lines), f"- Manual check FAILED {who}: \"{item}…\" — {text}")
            body, msg = _set_status(body, "changes-requested"), "Failure noted; spec sent back."
    elif kind == "approve":
        if _field(body, "Status") != "review":
            raise ValueError("Already approved (or not in review). Refresh the page.")
        body = _append_note(body, f"- Approved for merge {who}." + (f" {text}" if text else ""))
        body = _set_status(body, "approved")
        msg = "Approved. Claude will commit and run the post-merge step when it reads the inbox."
    elif kind == "unapprove":
        if _field(body, "Status") != "approved":
            raise ValueError("That spec isn't approved. Refresh the page.")
        body = _set_status(_append_note(body, f"- Approval withdrawn {who}." + (f" {text}" if text else "")), "review")
        msg = "Approval withdrawn; it's back in review."
    elif kind == "changes":
        if not text:
            raise ValueError("Say what needs to change.")
        if crew_state().get("outcome") == "escalated" and Path(crew_state().get("spec") or "").name == path.name:
            _write_state(outcome="changes requested")
        body = _set_status(_append_note(body, f"- Changes requested {who}: {text}"), "changes-requested")
        msg = "Sent back with your notes."
    elif kind == "note":
        if not text:
            raise ValueError("Type a note first.")
        body, msg = _append_note(body, f"- Note {who}: {text}"), "Note added to the spec."
    else:
        raise ValueError(f"unknown action {kind!r}")

    path.write_text(body)
    with INBOX.open("a") as f:
        f.write(json.dumps({"at": dt.datetime.now().isoformat(timespec="seconds"), "spec": path.name,
                            "action": kind, "text": text, "item": a.get("expect", "")[:200]}) + "\n")
    return msg


# ---------- starting and stopping crew runs ----------

def _child_env() -> dict:
    env = dict(os.environ)
    for k in ("CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT"):  # don't look like a nested session
        env.pop(k, None)
    return env


def _write_state(**fields) -> None:
    try:
        state = json.loads(STATE_FILE.read_text()) if STATE_FILE.exists() else {}
    except (OSError, json.JSONDecodeError):
        state = {}
    state.update(fields, updated=dt.datetime.now().isoformat(timespec="seconds"))
    STATE_FILE.write_text(json.dumps(state, indent=2))


_launch_lock = threading.Lock()


def _launch_crew(args: list[str], spec: str | None, request: str | None) -> None:
    with _launch_lock:
        if crew_state().get("running"):
            raise ValueError("The crew is already running. Wait for it to finish or stop it first.")
        with CREW_LOG.open("w") as log:
            log.write(f"$ run_crew.py {' '.join(args)}\n")
            log.flush()
            proc = subprocess.Popen([sys.executable, "-u", str(RUN_CREW), *args], cwd=ROOT,
                                    stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT,
                                    start_new_session=True, env=_child_env())
        threading.Thread(target=proc.wait, daemon=True).start()  # reap when it exits
        now = dt.datetime.now().isoformat(timespec="seconds")
        _write_state(running=True, pid=proc.pid, agent="starting", step="", step_started=now,
                     spec=spec, request=request, outcome=None, run_dir="")


def start_task(num: str, force: bool = False) -> str:
    d = gather()
    spec = next((s for s in d["specs"] if s.num == num), None)
    if spec is None:
        raise ValueError(f"No spec {num}.")
    if spec.status == "done":
        raise ValueError(f"{num} is already done.")
    if not force and spec not in d["ready"]:
        raise ValueError(f"{num} isn't ready (status {spec.status}, needs {', '.join(spec.depends) or 'nothing'}).")
    _launch_crew(["--spec", spec.rel, "--yes"], spec.rel, None)
    return f"Started {num}: {spec.owner} builds it, then qa-tester checks it."


def plan_task(text: str) -> str:
    text = text.strip()
    if not text:
        raise ValueError("Describe what you want first.")
    _launch_crew([text], None, text)
    return "The producer is writing a spec. It will wait for your approval before anything is built."


def stop_crew() -> str:
    st = crew_state()
    if not st.get("running"):
        raise ValueError("Nothing is running.")
    pid = int(st["pid"])
    try:
        if os.getpgid(pid) == pid:
            os.killpg(pid, signal.SIGTERM)  # run_crew and the claude call it is waiting on
        else:
            os.kill(pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    _write_state(running=False, outcome="stopped by Miles")
    spec = st.get("spec")
    if spec and (ROOT / spec).is_file():
        text = (ROOT / spec).read_text()
        if _field(text, "Status").startswith("in-progress"):
            (ROOT / spec).write_text(_set_status(text, "todo"))
    return "Stopped. Any half-made changes are left in the working tree."


def dismiss_escalation() -> str:
    if crew_state().get("outcome") != "escalated":
        raise ValueError("Nothing is escalated.")
    _write_state(outcome="dismissed")
    return "Dismissed. The spec keeps its current status."


def finish_spec(num: str, note: str) -> str:
    spec = next((s for s in load_specs() if s.num == num), None)
    if spec is None or spec.status != "approved":
        raise ValueError(f"{num} isn't approved. Refresh the page.")
    return chat_send(f"Miles clicked Commit & finish on {spec.rel}: commit its files, then run the "
                     f"producer's post-merge step (memory/, Status done, move to done/)."
                     + (f" Miles' note: {note}" if note.strip() else "")) and "Sent to Claude (see the chat panel)."


def crew_log_tail(n: int = 30) -> str:
    try:
        return "\n".join(CREW_LOG.read_text(errors="replace").splitlines()[-n:])
    except OSError:
        return ""


# ---------- chat with a headless Claude Code session ----------

CHAT_SYSTEM = (
    "You are the main Claude Code session for Doom Scroller. Miles is talking to you through "
    "the crew dashboard (tools/crew/dashboard.py), not a terminal, so you can't ask permission "
    "interactively: if something needs his approval (commits, pushes, deletes, large refactors), "
    "ask in your reply and wait for his answer. Follow CLAUDE.md and .claude/rules/. "
    "His dashboard input is in tools/crew/inbox.jsonl; the crew's live state is in "
    "tools/crew/.crew-state.json and its output in tools/crew/crew.log. He starts crew runs "
    "from the dashboard, so don't launch tools/crew/run_crew.py unless he asks. "
    "Keep replies short; plain markdown."
)
_chat_lock = threading.Lock()
_chat_proc: subprocess.Popen | None = None


def chat_load() -> dict:
    try:
        return json.loads(CHAT_FILE.read_text())
    except (OSError, json.JSONDecodeError):
        return {"session": None, "messages": []}


def _chat_append(msgs: list[dict], session: str | None = None) -> None:
    c = chat_load()
    if session:
        c["session"] = session
    c["messages"].extend(msgs)
    CHAT_FILE.write_text(json.dumps(c, indent=1))


def _tool_summary(block: dict) -> str:
    inp = block.get("input") or {}
    detail = (inp.get("description") or inp.get("command") or inp.get("file_path")
              or inp.get("pattern") or inp.get("prompt") or "")
    return f"{block.get('name', 'tool')}: {str(detail)[:140]}".rstrip(": ")


def _chat_run(text: str, session: str | None) -> None:
    global _chat_proc
    try:
        cmd = [CLAUDE_BIN, "-p", "--output-format", "stream-json", "--verbose",
               "--permission-mode", "auto", "--append-system-prompt", CHAT_SYSTEM]
        if session:
            cmd += ["--resume", session]
        with CHAT_ERR.open("w") as err:
            proc = subprocess.Popen(cmd, cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                    stderr=err, text=True, env=_child_env(), start_new_session=True)
        _chat_proc = proc
        proc.stdin.write(text)  # prompt via stdin, so text starting with "-" isn't read as a flag
        proc.stdin.close()
        said = False
        for line in proc.stdout:
            try:
                ev = json.loads(line)
            except json.JSONDecodeError:
                continue
            new: list[dict] = []
            if ev.get("type") == "assistant":
                for b in (ev.get("message") or {}).get("content") or []:
                    if b.get("type") == "text" and b.get("text", "").strip():
                        new.append({"role": "assistant", "text": b["text"]})
                    elif b.get("type") == "tool_use":
                        new.append({"role": "tool", "text": _tool_summary(b)})
            elif ev.get("type") == "result" and ev.get("is_error"):
                new.append({"role": "error", "text": str(ev.get("result") or ev.get("subtype"))[:2000]})
            said = said or bool(new)
            if new or (ev.get("type") == "system" and ev.get("session_id")):
                _chat_append(new, ev.get("session_id"))
        proc.wait()
        if proc.returncode and not said:
            tail = CHAT_ERR.read_text(errors="replace")[-1500:] if CHAT_ERR.exists() else ""
            _chat_append([{"role": "error", "text": f"claude exited {proc.returncode}. {tail}".strip()}])
    except OSError as ex:
        _chat_append([{"role": "error", "text": f"Couldn't start claude: {ex}"}])
    finally:
        _chat_proc = None
        _chat_lock.release()


def chat_send(text: str) -> str:
    text = text.strip()
    if not text:
        raise ValueError("Type a message first.")
    if not _chat_lock.acquire(blocking=False):
        raise ValueError("Claude is still replying. Wait, or press Stop.")
    try:
        _chat_append([{"role": "user", "text": text, "at": dt.datetime.now().isoformat(timespec="seconds")}])
        threading.Thread(target=_chat_run, args=(text, chat_load().get("session")), daemon=True).start()
    except Exception:
        _chat_lock.release()
        raise
    return "sent"


def chat_stop() -> str:
    proc = _chat_proc
    if proc is None:
        raise ValueError("Claude isn't replying.")
    try:
        os.killpg(proc.pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    _chat_append([{"role": "error", "text": "Stopped."}])
    return "Stopped."


def chat_new() -> str:
    if _chat_lock.locked():
        raise ValueError("Claude is still replying. Stop it first.")
    old = chat_load()
    if old.get("messages"):
        archive = CHAT_FILE.with_name(f".chat-{dt.datetime.now():%Y%m%d-%H%M%S}.json")
        archive.write_text(json.dumps(old, indent=1))
    CHAT_FILE.write_text(json.dumps({"session": None, "messages": []}))
    return "New conversation."


def chat_status() -> dict:
    c = chat_load()
    return {"busy": _chat_lock.locked(), "session": c.get("session"), "messages": c.get("messages", [])[-300:]}


# ---------- rendering ----------

CSS = """
:root{--bg:#f6f5f2;--card:#fff;--ink:#1d1d1f;--mute:#6b6b70;--line:#e3e1dc;--accent:#b4452f;
--warn:#a15c00;--ok:#2f7d4f;--info:#2d5fa8;--chip:#efede8;--field:#fbfaf8}
@media (prefers-color-scheme:dark){:root{--bg:#141416;--card:#1d1d20;--ink:#ececef;--mute:#9a9aa2;
--line:#2c2c31;--accent:#ff7a5c;--warn:#f0b04a;--ok:#5fcf8c;--info:#7fb0ff;--chip:#26262b;--field:#17171a}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);
font:14px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif}
main{max-width:1180px;margin:0 auto;padding:20px 16px 64px}
header{display:flex;justify-content:space-between;align-items:baseline;gap:12px;flex-wrap:wrap}
h1{font-size:20px;margin:0}h2{font-size:13px;text-transform:uppercase;letter-spacing:.06em;
color:var(--mute);margin:26px 0 10px}.mute{color:var(--mute)}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:12px;align-items:start}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px 14px}
.need{border-left:4px solid var(--accent)}.live{border-left:4px solid var(--info)}
.tag{display:inline-block;font-size:11px;font-weight:600;padding:1px 7px;border-radius:99px;
background:var(--chip);color:var(--mute);margin-right:6px}
.k-escalated,.k-approve{color:#fff;background:var(--accent)}.k-answer{color:var(--warn)}
.k-review{color:var(--ok)}.k-manual{color:var(--info)}
.num{font-variant-numeric:tabular-nums;font-weight:600}.row{display:flex;gap:8px;align-items:baseline}
code{font:12px ui-monospace,monospace;background:var(--chip);padding:1px 4px;border-radius:4px}
.bar{height:4px;background:var(--chip);border-radius:2px;margin-top:8px;overflow:hidden}
.bar i{display:block;height:100%;background:var(--ok)}
ul{margin:6px 0 0;padding-left:18px}li{margin:2px 0}
.pulse{display:inline-block;width:8px;height:8px;border-radius:50%;background:var(--info);
margin-right:6px;animation:p 1.4s infinite}@keyframes p{50%{opacity:.25}}
table{width:100%;border-collapse:collapse}td{padding:6px;border-top:1px solid var(--line);vertical-align:top}
tr.click{cursor:pointer}tr.click:hover td{background:var(--chip)}
details>summary{cursor:pointer;list-style:none;color:var(--info);font-size:13px;margin-top:8px}
details>summary::-webkit-details-marker{display:none}
details[open]>summary{margin-bottom:6px}
.spec{white-space:pre-wrap;font:12px/1.5 ui-monospace,monospace;background:var(--field);border:1px solid var(--line);
border-radius:8px;padding:10px;max-height:420px;overflow:auto;margin:0}
form{margin-top:10px;display:flex;flex-direction:column;gap:6px}
textarea{width:100%;min-height:58px;resize:vertical;font:inherit;color:inherit;background:var(--field);
border:1px solid var(--line);border-radius:8px;padding:8px}
textarea:focus{outline:2px solid var(--info);outline-offset:-1px}
.btns{display:flex;gap:6px;flex-wrap:wrap}
button{font:600 13px system-ui,sans-serif;border-radius:8px;border:1px solid var(--line);
background:var(--chip);color:var(--ink);padding:6px 12px;cursor:pointer}
button:hover{filter:brightness(.96)}button.primary{background:var(--ok);border-color:var(--ok);color:#fff}
button.danger{background:transparent;color:var(--accent);border-color:var(--accent)}
.msg{font-size:12px;min-height:1em}.msg.ok{color:var(--ok)}.msg.err{color:var(--accent)}
.card.sent{opacity:.55}
.sec{--c:var(--mute);margin:22px 0 0;padding:4px 14px 14px;border-radius:12px;border:1px solid var(--line);
border-top:4px solid var(--c);background:color-mix(in srgb,var(--c) 6%,var(--bg))}
.sec>h2{color:var(--c);margin:10px 0 10px;display:flex;align-items:center;gap:8px}
.sec>h2 .cnt{font-size:11px;color:#fff;background:var(--c);border-radius:99px;padding:1px 8px;letter-spacing:0}
.sec .card{border-left:4px solid var(--c)}.sec .card.mute{border-left-color:var(--line)}
.sec-needs{--c:var(--accent)}.sec-commit{--c:var(--ok)}.sec-working{--c:var(--info)}
.sec-ready{--c:#7a4fc0}.sec-waiting{--c:var(--mute)}.sec-repo{--c:var(--mute)}.sec-done{--c:#4f8a6b}
@media (prefers-color-scheme:dark){.sec-ready{--c:#b48cff}.sec-done{--c:#7fc49d}}
.plan{margin:22px 0 0}.plan form{margin:0}.planrow{display:flex;gap:8px;align-items:stretch}
.planrow textarea{flex:1;min-height:44px}.planrow button{white-space:nowrap}
#modal{position:fixed;inset:0;background:rgba(0,0,0,.45);display:none;align-items:center;justify-content:center;padding:16px;z-index:9}
#modal.on{display:flex}#modal .card{width:min(900px,100%);max-height:90vh;overflow:auto}
#modal .spec{max-height:none}
button:disabled{opacity:.45;cursor:default;filter:none}
.log{white-space:pre-wrap;font:11px/1.45 ui-monospace,monospace;background:var(--field);border:1px solid var(--line);
border-radius:8px;padding:8px;max-height:260px;overflow:auto;margin:6px 0 0}
#chat{position:fixed;top:0;right:0;bottom:0;width:400px;background:var(--card);border-left:1px solid var(--line);
display:flex;flex-direction:column;z-index:5}
#chat .head{display:flex;justify-content:space-between;align-items:center;gap:8px;padding:12px 14px;border-bottom:1px solid var(--line)}
#chat .head b{font-size:15px}#chat .head .btns button{padding:4px 9px;font-size:12px}
#chatlog{flex:1;overflow:auto;padding:12px 14px;display:flex;flex-direction:column;gap:8px}
.m{max-width:92%;padding:8px 11px;border-radius:12px;white-space:pre-wrap;overflow-wrap:anywhere}
.m.user{align-self:flex-end;background:var(--info);color:#fff;border-bottom-right-radius:4px}
.m.assistant{align-self:flex-start;background:var(--chip);border-bottom-left-radius:4px}
.m.tool{align-self:flex-start;color:var(--mute);font:11px ui-monospace,monospace;padding:0 4px}
.m.error{align-self:flex-start;color:var(--accent);font-size:12px;padding:0 4px}
.m pre{margin:6px 0;white-space:pre-wrap;font:12px ui-monospace,monospace;background:var(--field);
border:1px solid var(--line);border-radius:6px;padding:6px}
.m.user code{background:rgba(255,255,255,.2)}.m a{color:inherit}
#chat form{margin:0;padding:10px 14px 14px;border-top:1px solid var(--line)}
#chat textarea{min-height:64px}
#chatbtn{position:fixed;right:16px;bottom:16px;z-index:6;display:none;border-radius:99px;padding:10px 16px;
background:var(--info);border-color:var(--info);color:#fff}
@media (min-width:1100px){main{margin-right:400px;max-width:none}}
@media (max-width:1099px){#chat{display:none;width:100%;border-left:0}#chat.open{display:flex}
#chatbtn{display:block}#chat.open~#chatbtn{display:none}}
"""

JS = r"""
const main = () => document.querySelector('main');
const busy = () => {
  const a = document.activeElement;
  return (a && main().contains(a) && ['TEXTAREA','INPUT'].includes(a.tagName))
    || main().querySelector('details[open]') || document.getElementById('modal').classList.contains('on')
    || [...main().querySelectorAll('textarea')].some(t => t.value.trim());
};
// A key for an element that survives a refresh: its card's spec/line plus its own text.
function keyOf(el, own) {
  const c = el.closest('[data-key]') || el.closest('[data-spec]') || el.closest('.card');
  if (c && c.dataset.key) return c.dataset.key + '|' + own;
  return (c ? (c.dataset.spec || '') + '|' + (c.dataset.line || '') + '|' + (c.dataset.spec ? '' : c.textContent.slice(0, 40)) : '') + '|' + own;
}
async function refreshMain(force) {
  if (!force && busy()) return;
  try {
    const html = await (await fetch('/', {cache: 'no-store'})).text();
    const doc = new DOMParser().parseFromString(html, 'text/html');
    // Keep what Miles has open or typed across the swap.
    const open = new Set([...main().querySelectorAll('details[open]')].map(d => keyOf(d, d.querySelector('summary').textContent)));
    const typed = new Map([...main().querySelectorAll('textarea')].filter(t => t.value).map(t => [keyOf(t, t.placeholder), t.value]));
    const focus = document.activeElement && main().contains(document.activeElement) && document.activeElement.tagName === 'TEXTAREA'
      ? keyOf(document.activeElement, document.activeElement.placeholder) : null;
    const logs = new Map([...main().querySelectorAll('.log')].map(l => [keyOf(l, 'log'), l.scrollHeight - l.scrollTop - l.clientHeight < 30]));
    main().innerHTML = doc.querySelector('main').innerHTML;
    document.getElementById('templates').innerHTML = doc.getElementById('templates').innerHTML;
    main().querySelectorAll('details').forEach(d => { if (open.has(keyOf(d, d.querySelector('summary').textContent))) d.open = true; });
    main().querySelectorAll('textarea').forEach(t => {
      const k = keyOf(t, t.placeholder);
      if (typed.has(k)) t.value = typed.get(k);
      if (k === focus) { t.focus(); t.setSelectionRange(t.value.length, t.value.length); }
    });
    main().querySelectorAll('.log').forEach(l => { if (logs.get(keyOf(l, 'log')) !== false) l.scrollTop = l.scrollHeight; });
  } catch (e) {}
}
setInterval(() => refreshMain(false), 10000);

// Watch the crew: refresh at once when a run starts, changes step, or finishes.
let crewSig = null;
async function crewPoll() {
  try {
    const s = await (await fetch('/status', {cache: 'no-store'})).json();
    if (window.bootId && s.boot !== window.bootId && !busy()) { location.reload(); return; }
    window.bootId = window.bootId || s.boot;
    const sig = JSON.stringify(s);
    if (crewSig !== null && sig !== crewSig) {
      await refreshMain(true);
      if (!s.running && s.outcome) {
        document.title = (s.outcome === 'qa-passed' ? '✓ ' : '! ') + 'Crew Dashboard';
        if (window.Notification && Notification.permission === 'granted')
          new Notification('Crew finished', {body: (s.spec || '') + ' → ' + s.outcome});
      } else document.title = (s.running ? '● ' : '') + 'Crew Dashboard';
    }
    crewSig = sig;
  } catch (e) {}
  setTimeout(crewPoll, 3000);
}
document.addEventListener('DOMContentLoaded', crewPoll);
document.addEventListener('visibilitychange', () => { if (!document.hidden && document.title.startsWith('✓')) document.title = 'Crew Dashboard'; });
function askNotify() { if (window.Notification && Notification.permission === 'default') Notification.requestPermission(); }

async function post(url, body) {
  const r = await fetch(url, {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(body || {})});
  const j = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(j.error || r.statusText);
  return j;
}

async function crew(btn, what, extra) {
  const box = btn.closest('form') || btn.closest('.card');
  const msg = box.querySelector('.msg'), ta = box.querySelector('textarea');
  if (what === 'stop' && !confirm('Stop the running crew? Its half-made changes stay in the working tree.')) return;
  if (msg) { msg.className = 'msg'; msg.textContent = 'Working…'; }
  btn.disabled = true;
  try {
    if (what !== 'stop') askNotify();  // offer a desktop notification for when it finishes
    const j = await post('/crew/' + what, Object.assign({text: ta ? ta.value : ''}, extra || {}));
    if (msg) { msg.className = 'msg ok'; msg.textContent = j.message; }
    if (ta) ta.value = '';
    setTimeout(() => refreshMain(true), 900);
  } catch (e) { if (msg) { msg.className = 'msg err'; msg.textContent = e.message; } btn.disabled = false; }
}

// ---- chat ----
function esc(s) { return s.replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c])); }
function md(s) {
  return s.split(/```[\w-]*\n?/).map((part, i) => {
    if (i % 2) return '<pre>' + esc(part.replace(/\n$/, '')) + '</pre>';
    return esc(part)
      .replace(/`([^`\n]+)`/g, '<code>$1</code>')
      .replace(/\*\*([^*\n]+)\*\*/g, '<b>$1</b>')
      .replace(/\[([^\]\n]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>')
      .replace(/(^|[\s(])(https?:\/\/[^\s<)]+)/g, '$1<a href="$2" target="_blank" rel="noopener">$2</a>');
  }).join('');
}
let chatCount = -1, chatBusy = false, chatTimer = null;
async function chatPoll() {
  clearTimeout(chatTimer);
  try {
    const j = await (await fetch('/chat', {cache: 'no-store'})).json();
    const log = document.getElementById('chatlog');
    if (j.messages.length !== chatCount || j.busy !== chatBusy) {
      const atBottom = log.scrollHeight - log.scrollTop - log.clientHeight < 60;
      log.innerHTML = j.messages.length ? j.messages.map(m =>
        `<div class="m ${esc(m.role)}">${m.role === 'tool' ? '› ' + esc(m.text) : md(m.text)}</div>`).join('')
        : '<div class="mute">Ask Claude anything about the project. It works in the repo with the same rules as a terminal session.</div>';
      if (j.busy) log.insertAdjacentHTML('beforeend', '<div class="m tool"><span class="pulse"></span>Claude is working…</div>');
      if (atBottom || chatCount < 0) log.scrollTop = log.scrollHeight;
      chatCount = j.messages.length;
    }
    chatBusy = j.busy;
    document.getElementById('chatsend').disabled = j.busy;
    document.getElementById('chatstop').disabled = !j.busy;
    document.getElementById('chatsid').textContent = j.session ? 'claude --resume ' + j.session : 'new session';
  } catch (e) {}
  chatTimer = setTimeout(chatPoll, chatBusy ? 1200 : 6000);
}
async function chatSend() {
  const ta = document.getElementById('chatin'), msg = document.getElementById('chatmsg');
  if (!ta.value.trim()) return;
  msg.textContent = '';
  try { await post('/chat/send', {text: ta.value}); ta.value = ''; chatBusy = true; chatCount = -2; chatPoll(); }
  catch (e) { msg.textContent = e.message; }
}
async function chatCmd(what) {
  const msg = document.getElementById('chatmsg');
  if (what === 'new' && !confirm('Start a new conversation? The current one is archived.')) return;
  try { await post('/chat/' + what); msg.textContent = ''; chatCount = -2; chatPoll(); }
  catch (e) { msg.textContent = e.message; }
}
function toggleChat() { document.getElementById('chat').classList.toggle('open'); }
document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('chatin').addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); chatSend(); }
  });
  chatPoll();
});

async function finish(btn) {
  const card = btn.closest('[data-spec]'), form = btn.closest('form');
  const ta = form.querySelector('textarea'), msg = form.querySelector('.msg');
  msg.className = 'msg'; msg.textContent = 'Sending…';
  try {
    const j = await post('/spec/finish', {spec: card.dataset.spec, text: ta ? ta.value : ''});
    msg.className = 'msg ok'; msg.textContent = j.message;
    form.querySelectorAll('button').forEach(b => b.disabled = true);
    document.getElementById('chat').classList.add('open'); chatBusy = true; chatCount = -2; chatPoll();
  } catch (e) { msg.className = 'msg err'; msg.textContent = e.message; }
}
async function act(btn, action) {
  const card = btn.closest('[data-spec]'), form = btn.closest('form');
  const ta = form.querySelector('textarea'), msg = form.querySelector('.msg');
  const body = {action, spec: card.dataset.spec, line: card.dataset.line ?? null,
                expect: card.dataset.expect || '', text: ta ? ta.value : ''};
  msg.className = 'msg'; msg.textContent = 'Saving…';
  try {
    const r = await fetch('/action', {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(body)});
    const j = await r.json();
    if (!r.ok) throw new Error(j.error || r.statusText);
    msg.className = 'msg ok'; msg.textContent = j.message;
    if (ta) ta.value = '';
    card.classList.add('sent'); form.querySelectorAll('button').forEach(b => b.disabled = true);
    setTimeout(() => refreshMain(true), 1600);
  } catch (e) { msg.className = 'msg err'; msg.textContent = e.message; }
}

function openSpec(num) {
  const src = document.getElementById('spec-' + num);
  if (!src) return;
  document.getElementById('modal-body').innerHTML = src.innerHTML;
  document.getElementById('modal').classList.add('on');
}
function closeModal(ev) {
  if (!ev || ev.target.id === 'modal' || ev.target.dataset.close !== undefined)
    document.getElementById('modal').classList.remove('on');
}
document.addEventListener('keydown', e => { if (e.key === 'Escape') closeModal(); });
"""


def e(s: object) -> str:
    return html.escape(str(s), quote=True)


def spec_head(s: Spec | None) -> str:
    if s is None:
        return ""
    pct = int(100 * s.criteria_done / s.criteria_total) if s.criteria_total else 0
    return (f'<div class="row"><span class="num">{e(s.num)}</span><span>{e(s.short_title)}</span></div>'
            f'<div class="mute">{e(s.owner)} · {e(s.status)} · {s.criteria_done}/{s.criteria_total} criteria'
            f'{" · needs " + ", ".join(s.depends) if s.depends else ""}</div>'
            f'<div class="bar"><i style="width:{pct}%"></i></div>')


def form_for(kind: str) -> str:
    if kind == "answer":
        ph, btns = "Your answer…", '<button class="primary" type="button" onclick="act(this,\'answer\')">Save answer</button>'
    elif kind == "manual":
        ph = "Optional note (required if it failed)…"
        btns = ('<button class="primary" type="button" onclick="act(this,\'pass\')">Passed</button>'
                '<button class="danger" type="button" onclick="act(this,\'fail\')">Failed</button>')
    elif kind == "approve":  # the producer's new spec, waiting before the build
        ph = "Optional note, or what needs changing…"
        btns = ('<button class="primary" type="button" onclick="crew(this,\'start\',{spec:this.closest(\'[data-spec]\').dataset.spec,force:true})">Approve &amp; build</button>'
                '<button class="danger" type="button" onclick="act(this,\'changes\')">Request changes</button>')
    elif kind == "escalated":  # QA failed twice; Miles decides what happens next
        ph = "What needs changing (for Request changes), or a note…"
        btns = ('<button class="primary" type="button" onclick="crew(this,\'start\',{spec:this.closest(\'[data-spec]\').dataset.spec,force:true})">Retry build</button>'
                '<button type="button" onclick="act(this,\'changes\')">Request changes</button>'
                '<button class="danger" type="button" onclick="crew(this,\'dismiss\')">Dismiss</button>')
    elif kind == "approved":  # approved, waiting for the commit + post-merge step
        ph = "Optional note for Claude…"
        btns = ('<button class="primary" type="button" onclick="finish(this)">Commit &amp; finish</button>'
                '<button class="danger" type="button" onclick="act(this,\'unapprove\')">Undo approval</button>')
    elif kind == "review":
        ph = "Optional note, or what needs changing…"
        btns = ('<button class="primary" type="button" onclick="act(this,\'approve\')">Approve</button>'
                '<button class="danger" type="button" onclick="act(this,\'changes\')">Request changes</button>')
    else:
        ph, btns = "Note for Claude / the agents…", '<button type="button" onclick="act(this,\'note\')">Add note</button>'
    return (f'<form onsubmit="return false"><textarea placeholder="{e(ph)}"></textarea>'
            f'<div class="btns">{btns}<button type="button" onclick="act(this,\'note\')">Just add a note</button></div>'
            f'<div class="msg"></div></form>')


def sec(kind: str, title: str, count: int | None = None) -> str:
    cnt = f' <span class="cnt">{count}</span>' if count is not None else ""
    return f'<section class="sec sec-{kind}"><h2>{e(title)}{cnt}</h2>'


def render(d: dict) -> str:
    st = d["state"]
    unread = len(d["inbox"])
    p = [f"<header><h1>Doom Scroller · crew dashboard</h1>"
         f"<span class='mute'>updated {e(d['generated'])} · auto-refresh pauses while you type</span></header>"]

    p.append(sec("needs", "Needs you", len(d["needs"])))
    if d["needs"]:
        p.append('<div class="grid">')
        for n in d["needs"]:
            s = n["spec"]
            attrs = f'data-spec="{e(s.num if s else "")}"'
            if "line" in n:
                attrs += f' data-line="{n["line"]}" data-expect="{e(n["text"][:200])}"'
            p.append(f'<div class="card need" {attrs}><span class="tag k-{e(n["kind"])}">{e(n["label"])}</span>'
                     f'{spec_head(s)}<div style="margin-top:6px">{e(n["text"])}</div>'
                     + (f'<details><summary>Open spec ▸</summary><div class="spec">{e(s.body)}</div></details>' if s else "")
                     + (form_for(n["kind"]) if s else "") + "</div>")
        p.append("</div>")
    else:
        p.append('<div class="card mute">Nothing waiting on you.</div>')
    p.append("</section>")

    p.append(sec("commit", "Approved — ready to commit", len(d["approved"])) + "<div class='grid'>")
    p += [f'<div class="card" data-spec="{e(s.num)}">{spec_head(s)}'
          f'<div class="mute">You approved this. Commit &amp; finish has Claude commit it and run the post-merge step.</div>'
          f'<details><summary>Open spec ▸</summary><div class="spec">{e(s.body)}</div></details>{form_for("approved")}</div>'
          for s in d["approved"]] or ['<div class="card mute">Nothing approved and waiting.</div>']
    p.append("</div></section>")

    running = bool(st.get("running"))
    log_tail = crew_log_tail()
    log_html = (f'<details><summary>Crew output ▸</summary><div class="log">{e(log_tail)}</div></details>'
                if log_tail else "")
    p.append(sec("working", "Being worked on") + "<div class='grid'>")
    if running:
        p.append(f'<div class="card live" data-key="crew"><span class="pulse"></span><b>{e(st.get("agent"))}</b> '
                 f'<span class="mute">{"step " + e(st.get("step")) + " " if st.get("step") else ""}since {e(str(st.get("step_started", ""))[11:])}</span>'
                 f'<div>{e(st.get("spec") or st.get("request") or "")}</div>'
                 f'<div class="mute">run_crew.py{" · logs <code>" + e(st["run_dir"]) + "</code>" if st.get("run_dir") else ""}</div>'
                 f'{log_html}<div class="btns" style="margin-top:8px"><button class="danger" type="button" '
                 f'onclick="crew(this,\'stop\')">Stop</button></div><div class="msg"></div></div>')
    elif st:
        p.append(f'<div class="card" data-key="crew"><span class="mute">Crew idle. Last run:</span> {e(st.get("spec") or st.get("request") or "")} → '
                 f'<b>{e(st.get("outcome") or "?")}</b> <span class="mute">({e(st.get("updated", ""))})</span>{log_html}</div>')
    for s in d["in_progress"]:
        p.append(f'<div class="card live" data-spec="{e(s.num)}">{spec_head(s)}'
                 f'<details><summary>Open spec ▸</summary><div class="spec">{e(s.body)}</div></details>{form_for("note")}</div>')
    if not st.get("running") and not d["in_progress"]:
        p.append('<div class="card mute">No agent is running.</div>')
    p.append("</div></section>")

    dis = ' disabled title="The crew is already running"' if running else ""
    p.append(f'<div class="plan" data-key="plan"><form onsubmit="return false"><label><b>Plan something new</b> '
             f'<span class="mute">— the producer turns this into a task spec, then waits for your approval before anything is built.</span></label>'
             f'<div class="planrow"><textarea placeholder="What do you want built or changed?"></textarea>'
             f'<button class="primary" type="button"{dis} onclick="crew(this,\'plan\')">Send to producer</button></div>'
             f'<div class="msg"></div></form></div>')
    p.append(sec("ready", "Ready to start", len(d["ready"])) + "<div class='grid'>")
    p += [f'<div class="card" data-spec="{e(s.num)}">{spec_head(s)}'
          f'<div class="btns" style="margin-top:8px"><button class="primary" type="button"{dis} '
          f'onclick="crew(this,\'start\',{{spec:\'{e(s.num)}\'}})">Start ({e(s.owner)} → QA)</button></div>'
          f'<div class="msg"></div>'
          f'<details><summary>Open spec ▸</summary><div class="spec">{e(s.body)}</div></details>{form_for("note")}</div>'
          for s in d["ready"]] or ['<div class="card mute">None.</div>']
    p.append("</div></section>")

    def table(rows: list[Spec], extra) -> str:
        out = ["<div class='card'><table>"]
        for s in rows:
            out.append(f"<tr class='click' onclick=\"openSpec('{e(s.num)}')\"><td class='num'>{e(s.num)}</td>"
                       f"<td>{e(s.short_title)}</td><td class='mute'>{e(s.owner)}</td><td class='mute'>{extra(s)}</td></tr>")
        out.append("</table></div>")
        return "".join(out)

    p.append(sec("waiting", "Waiting on other tasks", len(d["blocked"])))
    p.append(table(d["blocked"], lambda s: "needs " + e(", ".join(s.depends))) + "</section>")
    if d["on_hold"]:
        p.append(sec("waiting", "On hold", len(d["on_hold"])))
        p.append(table(d["on_hold"], lambda s: "parked by Miles") + "</section>")

    p.append(sec("repo", "Repo") + "<div class='grid'>")
    unc = d["uncommitted"]
    p.append(f"<div class='card'><b class='num'>{len(unc)}</b> uncommitted change(s) · "
             f"<b class='num'>{e(d['ahead'])}</b> unpushed commit(s)"
             + (f"<details><summary>Show files ▸</summary><ul>{''.join(f'<li><code>{e(l)}</code></li>' for l in unc)}</ul></details>" if unc else "")
             + "</div>")
    runs = "".join(f"<li><code>{e(n)}</code> {e(', '.join(steps))}</li>" for n, steps in d["runs"])
    p.append(f"<div class='card'><b>Recent crew runs</b><ul>{runs or '<li class=mute>none</li>'}</ul></div>")
    recent = "".join(f"<li><span class='mute'>{e(i.get('at', '')[5:16].replace('T', ' '))}</span> "
                     f"{e(i.get('spec', '')[:3])} <b>{e(i.get('action'))}</b> {e(i.get('text', '')[:80])}</li>"
                     for i in reversed(d["inbox"][-6:]))
    p.append(f"<div class='card'><b>Your recent input</b> <span class='mute'>({unread} in inbox)</span>"
             f"<ul>{recent or '<li class=mute>none yet</li>'}</ul></div>")
    p.append("</div></section>")

    p.append(sec("done", "Done", len(d["done"])))
    p.append(table(d["done"], lambda s: f"{s.criteria_done}/{s.criteria_total} criteria") + "</section>")

    hidden = "".join(f'<template id="spec-{e(s.num)}"><div class="row" style="justify-content:space-between">'
                     f'<b>{e(s.title)}</b><button type="button" data-close onclick="closeModal(event)">Close</button></div>'
                     f'<div class="mute" style="margin:4px 0 8px"><code>{e(s.rel)}</code></div>'
                     f'<div class="spec">{e(s.body)}</div>'
                     f'<div data-spec="{e(s.num)}">{form_for("note")}</div></template>' for s in d["specs"])
    modal = '<div id="modal" onclick="closeModal(event)"><div class="card" id="modal-body"></div></div>'
    chat = ('<aside id="chat"><div class="head"><div><b>Claude</b><div class="mute" style="font-size:11px">'
            '<code id="chatsid">…</code></div></div><div class="btns">'
            '<button type="button" id="chatstop" onclick="chatCmd(\'stop\')" disabled>Stop</button>'
            '<button type="button" onclick="chatCmd(\'new\')">New chat</button>'
            '<button type="button" id="chatclose" onclick="toggleChat()">Close</button></div></div>'
            '<div id="chatlog"></div>'
            '<form onsubmit="return false"><textarea id="chatin" placeholder="Message Claude (Enter to send, Shift+Enter for a new line)"></textarea>'
            '<div class="btns"><button class="primary" type="button" id="chatsend" onclick="chatSend()">Send</button></div>'
            '<div class="msg err" id="chatmsg"></div></form></aside>'
            '<button type="button" id="chatbtn" onclick="toggleChat()">Chat with Claude</button>')

    return ("<!doctype html><html lang=en><head><meta charset=utf-8>"
            "<meta name=viewport content='width=device-width,initial-scale=1'><title>Crew Dashboard</title>"
            f"<style>{CSS}@media (min-width:1100px){{#chatclose{{display:none}}}}</style></head><body><main>{''.join(p)}</main>"
            f"<div id='templates'>{hidden}</div>{modal}{chat}<script>{JS}</script></body></html>")


def text_summary(d: dict) -> str:
    out = [f"Crew dashboard — {d['generated']}", "", f"NEEDS YOU ({len(d['needs'])})"]
    for n in d["needs"]:
        s = n["spec"]
        out.append(f"  [{n['label']}] {s.num + ' ' if s else ''}{n['text'][:150]}")
    st = d["state"]
    out += ["", "BEING WORKED ON"]
    if st.get("running"):
        out.append(f"  {st.get('agent')} on {st.get('spec') or st.get('request')} (step {st.get('step')})")
    out += [f"  {s.num} {s.title} ({s.owner})" for s in d["in_progress"]] or ([] if st.get("running") else ["  nothing"])
    out += ["", "APPROVED, TO COMMIT: " + (", ".join(s.num for s in d["approved"]) or "none")]
    out += ["", "READY: " + (", ".join(f"{s.num} ({s.owner})" for s in d["ready"]) or "none"),
            "WAITING: " + (", ".join(f"{s.num}<-{'/'.join(s.depends)}" for s in d["blocked"]) or "none"),
            f"REPO: {len(d['uncommitted'])} uncommitted, {d['ahead']} unpushed · INBOX: {len(d['inbox'])} item(s)"]
    return "\n".join(out)


class Handler(BaseHTTPRequestHandler):
    def _send(self, code: int, body: bytes, ctype: str) -> None:
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _local(self) -> bool:
        host = self.headers.get("Host", "")
        origin = self.headers.get("Origin", "")
        return host.split(":")[0] in ("localhost", "127.0.0.1") and (not origin or urlparse(origin).netloc == host)

    def _json(self, code: int, obj: object) -> None:
        self._send(code, json.dumps(obj).encode(), "application/json")

    def do_GET(self):  # noqa: N802
        path = urlparse(self.path).path
        if not self._local():  # also blocks DNS-rebinding reads of the chat history
            return self._json(403, {"error": "forbidden"})
        if path == "/chat":
            return self._json(200, chat_status())
        if path == "/status":  # cheap poll target; the page refreshes when this changes
            st = crew_state()
            return self._json(200, {"boot": BOOT_ID, **{k: st.get(k) for k in ("running", "step", "spec", "outcome")}})
        if path != "/":
            return self._send(404, b"not found", "text/plain")
        self._send(200, render(gather()).encode(), "text/html; charset=utf-8")

    def do_POST(self):  # noqa: N802
        # Local-only writes: reject cross-site requests (a web page can't post into the repo
        # or drive Claude). The JSON content type also forces a CORS preflight.
        path = urlparse(self.path).path
        if not self._local() or "application/json" not in self.headers.get("Content-Type", ""):
            return self._json(403, {"error": "forbidden"})
        try:
            length = min(int(self.headers.get("Content-Length", 0)), 50000)
            a = json.loads(self.rfile.read(length) or b"{}")
            text = str(a.get("text") or "")
            routes = {
                "/action": lambda: apply_action(a),
                "/crew/start": lambda: start_task(str(a.get("spec", ""))[:3], bool(a.get("force"))),
                "/crew/plan": lambda: plan_task(text),
                "/crew/stop": stop_crew,
                "/crew/dismiss": lambda: dismiss_escalation(),
                "/spec/finish": lambda: finish_spec(str(a.get("spec", ""))[:3], text),
                "/chat/send": lambda: chat_send(text),
                "/chat/stop": chat_stop,
                "/chat/new": chat_new,
            }
            if path not in routes:
                return self._json(404, {"error": "not found"})
            self._json(200, {"message": routes[path]()})
        except (ValueError, KeyError, json.JSONDecodeError) as ex:
            self._json(400, {"error": str(ex)})

    def log_message(self, *args):
        pass


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", type=int, default=8090)
    ap.add_argument("--once", action="store_true", help="print a text summary and exit")
    args = ap.parse_args()
    if args.once:
        print(text_summary(gather()))
        return
    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    print(f"Crew dashboard on http://localhost:{args.port}  (Ctrl+C to stop)")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
