#!/usr/bin/env bash
# Restarts the crew dashboard (tools/crew/dashboard.py) in the background.
# Crew runs and the chat are separate processes and keep going; open dashboard tabs reload.
# Output: tools/crew/dashboard.log. Usage: tools/crew/restart-dashboard.sh [--port N]
set -euo pipefail
cd "$(dirname "$0")/../.."

port=8090
[ "${1:-}" = "--port" ] && port="${2:?--port needs a number}"

# Stop whatever dashboard is listening on the port (only a python process, to be safe).
listener() { ss -ltnpH "sport = :$port" 2>/dev/null | grep -o 'pid=[0-9]*' | head -1 | cut -d= -f2; }
pid="$(listener || true)"
if [ -n "$pid" ]; then
  if ps -o args= -p "$pid" | grep -q 'dashboard\.py'; then
    kill "$pid"
  else
    echo "Port $port is used by something else: $(ps -o args= -p "$pid")" >&2
    exit 1
  fi
  for _ in $(seq 20); do
    [ -z "$(listener || true)" ] && break
    sleep 0.25
  done
fi

setsid nohup python3 tools/crew/dashboard.py --port "$port" >tools/crew/dashboard.log 2>&1 </dev/null &

for _ in $(seq 40); do
  if curl -s -o /dev/null "http://localhost:$port/status"; then
    echo "Dashboard running at http://localhost:$port"
    exit 0
  fi
  sleep 0.25
done
echo "Dashboard didn't start. Last lines of tools/crew/dashboard.log:" >&2
tail -20 tools/crew/dashboard.log >&2
exit 1
