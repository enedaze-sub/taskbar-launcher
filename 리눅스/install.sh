#!/usr/bin/env bash
set -euo pipefail
task_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec python3 "$task_dir/installer.py" "$@"
