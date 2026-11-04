#!/usr/bin/env python3
"""Doom Scroller crew dashboard: what needs Miles, what's being worked on, what's next.

Reads the repo: task specs in docs/tasks/ (Status / Owner / Depends on, unticked
"(manual, Miles)" checks, open questions), the orchestrator's live state file
(tools/crew/.crew-state.json, written by run_crew.py), docs/crew-runs/ logs, and git.

Interactive: click a card to open the spec; answer questions, pass/fail manual checks,
approve or request changes, or leave a note. Input is written into the spec itself
(where agents read it) and appended to tools/crew/inbox.jsonl for the main session.
The dashboard never commits, builds, or runs agents.

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
import subprocess
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[2]
TASKS = ROOT / "docs" / "tasks"
RUNS = ROOT / "docs" / "crew-runs"
STATE_FILE = ROOT / "tools" / "crew" / ".crew-state.json"
INBOX = ROOT / "tools" / "crew" / "inbox.jsonl"

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
        if status != "done":
            for i, line in enumerate(lines):
                if QUESTION_RE.search(line) and not RESOLVED_RE.search(line):
                    q = line.strip(" -*")
                    j = i + 1
                    while j < len(lines) and lines[j].startswith("  ") and not lines[j].lstrip().startswith("- "):
                        q += " " + lines[j].strip()
                        j += 1
                    spec.questions.append(Item(i, q))
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

    needs: list[dict] = []
    for s in specs:
        if s.status == "review":
            needs.append({"kind": "review", "label": "Review & merge", "spec": s,
                          "text": "Built and through QA. Approve it for commit, or send it back with changes."})
        for q in s.questions:
            needs.append({"kind": "answer", "label": "Answer", "spec": s, "text": q.text, "line": q.line})
        if s.status in ("review", "done"):
            for m in s.manual_open:
                needs.append({"kind": "manual", "label": "Manual check", "spec": s, "text": m.text, "line": m.line})
    spec_of_state = by_num.get(Path(state.get("spec") or "000").name[:3])
    if state.get("outcome") == "escalated":
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
        "ready": [s for s in specs if s.status in ("todo", "changes-requested") and deps_done(s)],
        "blocked": [s for s in specs if s.status in ("todo", "changes-requested") and not deps_done(s)],
        "done": [s for s in specs if s.status == "done"],
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
        body = _append_note(body, f"- Approved for merge {who}." + (f" {text}" if text else ""))
        msg = "Approved. Claude will commit and run the post-merge step when it reads the inbox."
    elif kind == "changes":
        if not text:
            raise ValueError("Say what needs to change.")
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
#modal{position:fixed;inset:0;background:rgba(0,0,0,.45);display:none;align-items:center;justify-content:center;padding:16px;z-index:9}
#modal.on{display:flex}#modal .card{width:min(900px,100%);max-height:90vh;overflow:auto}
#modal .spec{max-height:none}
"""

JS = r"""
const busy = () => document.activeElement && ['TEXTAREA','INPUT'].includes(document.activeElement.tagName)
  || document.querySelector('details[open]') || document.getElementById('modal').classList.contains('on')
  || [...document.querySelectorAll('textarea')].some(t => t.value.trim());
setInterval(() => { if (!busy()) location.reload(); }, 15000);

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
    setTimeout(() => location.reload(), 1600);
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
    elif kind in ("review", "approve"):
        ph = "Optional note, or what needs changing…"
        btns = ('<button class="primary" type="button" onclick="act(this,\'approve\')">Approve</button>'
                '<button class="danger" type="button" onclick="act(this,\'changes\')">Request changes</button>')
    else:
        ph, btns = "Note for Claude / the agents…", '<button type="button" onclick="act(this,\'note\')">Add note</button>'
    return (f'<form onsubmit="return false"><textarea placeholder="{e(ph)}"></textarea>'
            f'<div class="btns">{btns}<button type="button" onclick="act(this,\'note\')">Just add a note</button></div>'
            f'<div class="msg"></div></form>')


def render(d: dict) -> str:
    st = d["state"]
    unread = len(d["inbox"])
    p = [f"<header><h1>Doom Scroller · crew dashboard</h1>"
         f"<span class='mute'>updated {e(d['generated'])} · auto-refresh pauses while you type</span></header>"]

    p.append(f"<h2>Needs you ({len(d['needs'])})</h2>")
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

    p.append("<h2>Being worked on</h2><div class='grid'>")
    if st.get("running"):
        p.append(f'<div class="card live"><span class="pulse"></span><b>{e(st.get("agent"))}</b> '
                 f'<span class="mute">step {e(st.get("step"))} since {e(str(st.get("step_started", ""))[11:])}</span>'
                 f'<div>{e(st.get("spec") or st.get("request") or "")}</div>'
                 f'<div class="mute">run_crew.py · logs <code>{e(st.get("run_dir", ""))}</code></div></div>')
    elif st:
        p.append(f'<div class="card"><span class="mute">Crew idle. Last run:</span> {e(st.get("spec") or "")} → '
                 f'<b>{e(st.get("outcome") or "?")}</b> <span class="mute">({e(st.get("updated", ""))})</span></div>')
    for s in d["in_progress"]:
        p.append(f'<div class="card live" data-spec="{e(s.num)}">{spec_head(s)}'
                 f'<details><summary>Open spec ▸</summary><div class="spec">{e(s.body)}</div></details>{form_for("note")}</div>')
    if not st.get("running") and not d["in_progress"]:
        p.append('<div class="card mute">No agent is running.</div>')
    p.append("</div>")

    p.append(f"<h2>Ready to start ({len(d['ready'])})</h2><div class='grid'>")
    p += [f'<div class="card" data-spec="{e(s.num)}">{spec_head(s)}'
          f'<details><summary>Open spec ▸</summary><div class="spec">{e(s.body)}</div></details>{form_for("note")}</div>'
          for s in d["ready"]] or ['<div class="card mute">None.</div>']
    p.append("</div>")

    def table(rows: list[Spec], extra) -> str:
        out = ["<div class='card'><table>"]
        for s in rows:
            out.append(f"<tr class='click' onclick=\"openSpec('{e(s.num)}')\"><td class='num'>{e(s.num)}</td>"
                       f"<td>{e(s.short_title)}</td><td class='mute'>{e(s.owner)}</td><td class='mute'>{extra(s)}</td></tr>")
        out.append("</table></div>")
        return "".join(out)

    p.append(f"<h2>Waiting on other tasks ({len(d['blocked'])})</h2>")
    p.append(table(d["blocked"], lambda s: "needs " + e(", ".join(s.depends))))

    p.append("<h2>Repo</h2><div class='grid'>")
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
    p.append("</div>")

    p.append(f"<h2>Done ({len(d['done'])})</h2>")
    p.append(table(d["done"], lambda s: f"{s.criteria_done}/{s.criteria_total} criteria"))

    hidden = "".join(f'<template id="spec-{e(s.num)}"><div class="row" style="justify-content:space-between">'
                     f'<b>{e(s.title)}</b><button type="button" data-close onclick="closeModal(event)">Close</button></div>'
                     f'<div class="mute" style="margin:4px 0 8px"><code>{e(s.rel)}</code></div>'
                     f'<div class="spec">{e(s.body)}</div>'
                     f'<div data-spec="{e(s.num)}">{form_for("note")}</div></template>' for s in d["specs"])
    modal = '<div id="modal" onclick="closeModal(event)"><div class="card" id="modal-body"></div></div>'

    return ("<!doctype html><html lang=en><head><meta charset=utf-8>"
            "<meta name=viewport content='width=device-width,initial-scale=1'><title>Crew Dashboard</title>"
            f"<style>{CSS}</style></head><body><main>{''.join(p)}</main>{hidden}{modal}<script>{JS}</script></body></html>")


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

    def do_GET(self):  # noqa: N802
        if urlparse(self.path).path != "/":
            return self._send(404, b"not found", "text/plain")
        self._send(200, render(gather()).encode(), "text/html; charset=utf-8")

    def do_POST(self):  # noqa: N802
        # Local-only writes: reject cross-site requests (a web page can't post into the repo).
        origin = self.headers.get("Origin", "")
        host = self.headers.get("Host", "")
        if urlparse(self.path).path != "/action" or (origin and urlparse(origin).netloc != host) \
                or not host.split(":")[0] in ("localhost", "127.0.0.1"):
            return self._send(403, json.dumps({"error": "forbidden"}).encode(), "application/json")
        try:
            length = min(int(self.headers.get("Content-Length", 0)), 20000)
            msg = apply_action(json.loads(self.rfile.read(length) or b"{}"))
            self._send(200, json.dumps({"message": msg}).encode(), "application/json")
        except (ValueError, KeyError, json.JSONDecodeError) as ex:
            self._send(400, json.dumps({"error": str(ex)}).encode(), "application/json")

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
