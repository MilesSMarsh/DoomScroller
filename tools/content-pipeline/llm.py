"""Calls Claude through Claude Code's headless mode (`claude -p`), so the pipeline runs on
the user's Claude Code login with no API key. Every call is tool-less, ignores project
CLAUDE.md/memory (runs from a temp dir with no setting sources), and returns JSON that
matches the given schema.
"""
from __future__ import annotations

import json
import shutil
import subprocess
import tempfile


class LLMError(RuntimeError):
    pass


def call(system: str, prompt: str, schema: dict, model: str, timeout: int = 600,
         retries: int = 2) -> tuple[dict, dict]:
    """Returns (structured_output, usage_info)."""
    exe = shutil.which("claude")
    if not exe:
        raise LLMError("`claude` (Claude Code CLI) is not on PATH. Install it and sign in.")
    cmd = [exe, "-p", "--model", model, "--output-format", "json",
           "--tools", "", "--strict-mcp-config", "--setting-sources", "",
           "--no-session-persistence", "--system-prompt", system,
           "--json-schema", json.dumps(schema)]
    last = ""
    for _ in range(retries + 1):
        with tempfile.TemporaryDirectory() as cwd:
            try:
                proc = subprocess.run(cmd, input=prompt, capture_output=True, text=True,
                                      timeout=timeout, cwd=cwd)
            except subprocess.TimeoutExpired:
                last = f"timed out after {timeout}s"
                continue
        if proc.returncode != 0:
            last = (proc.stderr or proc.stdout).strip()[-800:]
            continue
        try:
            out = json.loads(proc.stdout)
        except json.JSONDecodeError:
            last = "non-JSON reply: " + proc.stdout[-400:]
            continue
        data = out.get("structured_output")
        if out.get("is_error") or not isinstance(data, dict):
            last = str(out.get("result"))[-800:]
            continue
        usage = {"model": model, "costUsd": out.get("total_cost_usd"),
                 "durationMs": out.get("duration_ms")}
        return data, usage
    raise LLMError(f"claude -p failed after {retries + 1} tries: {last}")
