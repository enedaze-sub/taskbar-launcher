#!/usr/bin/env bash
set -euo pipefail
task_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [[ ! -x "$task_dir/.venv/bin/python" ]]; then
  printf '%s\n' '먼저 bash install.sh로 설치해 주세요. 또는 python3 -m venv .venv 후 requirements.txt를 설치하세요.'
  exit 1
fi
if [[ -n "${DISPLAY:-}" ]]; then
  export QT_QPA_PLATFORM="${QT_QPA_PLATFORM:-xcb}"
fi
exec "$task_dir/.venv/bin/python" "$task_dir/run.py" "$@"
