#!/usr/bin/env bash
# Starts the server + Postgres locally via Docker Compose. Ctrl+C to stop (volume kept).
# Health: http://localhost:${SERVER_PORT:-8088}/health
set -euo pipefail
cd "$(dirname "$0")/../Server"
[ -f .env ] || { cp .env.example .env; echo "Created Server/.env from .env.example"; }
exec docker compose up --build "$@"
