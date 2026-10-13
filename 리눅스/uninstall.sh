#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import os
from pathlib import Path
import shutil
data=Path(os.environ.get('XDG_DATA_HOME',str(Path.home()/'.local/share'))).expanduser().absolute()
target=data/'decklauncher-app'
desktop=data/'applications/decklauncher.desktop'
if target.is_symlink():
    raise SystemExit('설치 경로가 심볼릭 링크입니다. 안전을 위해 자동 제거를 중단했습니다.')
if (target/'install-info.json').is_file():
    shutil.rmtree(target)
    if desktop.is_file(): desktop.unlink()
    print('프로그램을 제거했습니다. 등록 정보와 백업은 '+str(data/'decklauncher')+'에 남겨 두었습니다.')
else:
    raise SystemExit('이 설치 프로그램으로 설치한 경로를 찾지 못했습니다.')
PY
