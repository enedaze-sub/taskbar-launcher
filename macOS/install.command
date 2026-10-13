#!/bin/bash
set -euo pipefail
task_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
task_python=''
# Finder/Terminal may not include Homebrew or python.org Python on PATH.
for candidate in "$(command -v python3 || true)" /opt/homebrew/bin/python3 /usr/local/bin/python3 /Library/Frameworks/Python.framework/Versions/Current/bin/python3; do
  if [[ -n "$candidate" && -x "$candidate" ]] && "$candidate" -c 'import sys; raise SystemExit(0 if (3,10) <= sys.version_info[:2] < (3,16) else 1)' 2>/dev/null; then
    task_python="$candidate"
    break
  fi
done
if [[ -z "$task_python" ]]; then
  printf '%s\n' '처음 앱을 만들 때 Python 3.10~3.15가 필요합니다.' 'https://www.python.org/downloads/macos/ 에서 Python을 설치한 뒤 다시 실행해 주세요.'
  exit 1
fi
exec "$task_python" "$task_dir/installer.py" "$@"
