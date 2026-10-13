#!/bin/bash
set -euo pipefail
task_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [[ -n "${DECKLAUNCHER_PYTHON:-}" ]]; then
  exec "$DECKLAUNCHER_PYTHON" "$task_dir/run.py" "$@"
fi
for candidate in "$(command -v python3 || true)" /opt/homebrew/bin/python3 /usr/local/bin/python3 /Library/Frameworks/Python.framework/Versions/Current/bin/python3; do
  if [[ -n "$candidate" && -x "$candidate" ]] && "$candidate" -c 'import PySide6' 2>/dev/null; then
    exec "$candidate" "$task_dir/run.py" "$@"
  fi
done
printf '%s\n' '일반 사용은 install.command로 만든 DeckLauncher.app을 실행하세요.' '소스 직접 실행은 PySide6가 설치된 Python이 필요합니다.'
exit 1
