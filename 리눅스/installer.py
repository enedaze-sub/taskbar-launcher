#!/usr/bin/env python3
"""Per-user Linux installer. It never modifies the Windows build or launcher data."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import venv

ROOT=Path(__file__).resolve().parent
PACKAGES=['python3-venv','libxcb-cursor0','libxkbcommon-x11-0','libxcb-icccm4',
          'libxcb-image0','libxcb-keysyms1','libxcb-render-util0','libxcb-xinerama0',
          'libxcb-shape0','libxcb-randr0','libxcb-sync1','libxcb-xfixes0','libxcb-util1',
          'libegl1','libgl1','libopengl0','libdbus-1-3','libx11-xcb1']

def desktop_quote(value):
    # Desktop Entry values first use \ escaping, then Exec uses double-quote escaping.
    quoted='"'+str(value).replace('\\','\\\\').replace('"','\\"').replace('`','\\`').replace('$','\\$')+'"'
    return quoted.replace('\\','\\\\').replace('%','%%')

def desktop_text(target):
    script=target/'run.sh'
    return ('[Desktop Entry]\nVersion=1.0\nType=Application\nName=DeckLauncher\nName[ko]=나의 작은 런처\n'
            'Comment=Apps, folders and a little launcher\nComment[ko]=앱과 폴더를 모으는 나의 작은 런처\n'
            f'Exec=/bin/bash {desktop_quote(script)}\nIcon={target}/assets/launcher.png\n'
            'Terminal=false\nCategories=Utility;\nStartupNotify=false\nStartupWMClass=DeckLauncher\n'
            'Actions=Settings;\n\n[Desktop Action Settings]\nName=Settings\nName[ko]=런처 설정\n'
            f'Exec=/bin/bash {desktop_quote(script)} --settings\n')

def install(with_deps=False,online=False):
    if not sys.platform.startswith('linux'):raise RuntimeError('설치 스크립트는 리눅스 PC에서 실행해 주세요.')
    if not (3,10)<=sys.version_info[:2]<(3,16):raise RuntimeError('Python 3.10~3.15가 필요합니다. Ubuntu 22.04/24.04/26.04 기준으로 준비했습니다.')
    if hasattr(os,'geteuid') and os.geteuid()==0:raise RuntimeError('sudo로 설치하지 마세요. 일반 사용자로 bash install.sh를 실행해 주세요.')
    if with_deps:
        if not shutil.which('apt-get'):raise RuntimeError('이 옵션은 Ubuntu/Debian용입니다. README의 다른 배포판 안내를 확인해 주세요.')
        subprocess.run(['sudo','apt-get','update'],check=True)
        subprocess.run(['sudo','apt-get','install','-y']+PACKAGES,check=True)
    data=Path(os.environ.get('XDG_DATA_HOME',str(Path.home()/'.local/share'))).expanduser().absolute()
    target=data/'decklauncher-app';target.mkdir(parents=True,exist_ok=True)
    for name in ('decklauncher','Fonts','assets','tests','third_party_licenses'):
        shutil.copytree(ROOT/name,target/name,dirs_exist_ok=True,ignore=shutil.ignore_patterns('__pycache__','*.pyc'))
    for name in ('run.py','run.sh','requirements.txt','README.md','LICENSE','THIRD_PARTY_NOTICES.md'):
        shutil.copy2(ROOT/name,target/name)
    (target/'run.sh').chmod(0o755)
    try:venv.EnvBuilder(with_pip=True).create(target/'.venv')
    except Exception as ex:raise RuntimeError('가상 환경을 만들지 못했습니다. Ubuntu에서 sudo apt install python3-venv 후 다시 실행하세요.\n'+str(ex))
    interpreter=target/'.venv/bin/python';wheels=ROOT/'wheels'
    offline=platform.machine() in ('x86_64','AMD64') and wheels.is_dir() and any(wheels.glob('*.whl')) and not online
    if offline:
        for item in json.loads((ROOT/'wheels-SHA256.json').read_text(encoding='utf-8')):
            path=wheels/item['File']
            if hashlib.sha256(path.read_bytes()).hexdigest()!=item['SHA256']:raise RuntimeError('동봉 구성요소의 해시가 다릅니다. 패키지를 다시 풀어 주세요.')
    cmd=[str(interpreter),'-m','pip','--disable-pip-version-check','--no-input','install']
    if offline:cmd+=['--no-index','--find-links',str(wheels)]
    cmd+=['-r',str(target/'requirements.txt')];subprocess.run(cmd,check=True)
    plugin=next((target/'.venv').glob('lib/python*/site-packages/PySide6/Qt/plugins/platforms/libqxcb.so'),None)
    if plugin and shutil.which('ldd'):
        report=subprocess.run(['ldd',str(plugin)],capture_output=True,text=True,check=False)
        missing=[line.strip() for line in report.stdout.splitlines() if 'not found' in line]
        if missing:raise RuntimeError('GUI 시스템 라이브러리가 필요합니다:\n'+'\n'.join(missing)+'\nUbuntu에서는 bash install.sh --with-system-deps 로 다시 설치하세요.')
    # Exercise native Qt imports without needing an active display.
    subprocess.run([str(interpreter),'-c','from PySide6.QtWidgets import QApplication; from PySide6.QtNetwork import QLocalServer'],check=True)
    desktop=data/'applications/decklauncher.desktop';desktop.parent.mkdir(parents=True,exist_ok=True)
    desktop.write_text(desktop_text(target),encoding='utf-8');desktop.chmod(0o755)
    (target/'install-info.json').write_text(json.dumps(dict(AppPath=str(target),DesktopFile=str(desktop)),indent=2),encoding='utf-8')
    if shutil.which('update-desktop-database'):subprocess.run(['update-desktop-database',str(desktop.parent)],check=False)
    print('\n설치했습니다. 앱 목록에서 「나의 작은 런처」를 실행한 뒤 독에서 즐겨찾기에 추가하세요.')
    print('설정만 열기: /bin/bash '+str(target/'run.sh')+' --settings')
    print('등록 정보는 별도 '+str(data/'decklauncher')+' 폴더에 보관됩니다.')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--with-system-deps',action='store_true',help='Ubuntu/Debian 시스템 라이브러리도 설치 (sudo 요청)')
    parser.add_argument('--online',action='store_true',help='동봉 wheel 대신 인터넷에서 Qt 내려받기')
    args=parser.parse_args()
    try:install(args.with_system_deps,args.online)
    except (RuntimeError,subprocess.CalledProcessError,OSError) as ex:print('\n설치 중 확인이 필요합니다: '+str(ex),file=sys.stderr);return 1
    return 0
if __name__=='__main__':raise SystemExit(main())
