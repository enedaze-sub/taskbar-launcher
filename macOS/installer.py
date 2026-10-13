#!/usr/bin/env python3
"""Build a standalone Cocoa .app on the destination Mac, then install per-user."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import plistlib
import shutil
import socket
import subprocess
import sys
import tempfile
import uuid
import venv

ROOT=Path(__file__).resolve().parent
BUNDLE_ID='com.enedaze.decklauncher'
MIN_MAC=(14,4)

def mac_version(text):
    try:return tuple(int(p) for p in text.split('.')[:2])
    except ValueError:return (0,0)

def running_default():
    data=Path.home()/'Library/Application Support/DeckLauncher'
    key=hashlib.sha256(str(data.resolve()).encode()).hexdigest()[:16]
    address=Path('/tmp')/('decklauncher-mac-'+str(os.getuid()))/(key+'.socket')
    try:
        with socket.socket(socket.AF_UNIX,socket.SOCK_STREAM) as client:
            client.settimeout(.4);client.connect(str(address));return True
    except OSError:return False

def verify_bundle(app):
    with (app/'Contents/Info.plist').open('rb') as stream:info=plistlib.load(stream)
    if info.get('CFBundleIdentifier')!=BUNDLE_ID or info.get('CFBundlePackageType')!='APPL':raise RuntimeError('빌드된 앱의 식별자가 다릅니다.')
    name=info.get('CFBundleExecutable','')
    if not name or Path(name).name!=name:raise RuntimeError('앱 실행 파일이 잘못되었습니다.')
    executable=app/'Contents/MacOS'/name
    if not executable.is_file():raise RuntimeError('앱 실행 파일이 없습니다.')
    with executable.open('rb') as stream:magic=stream.read(4)
    if magic not in (b'\xcf\xfa\xed\xfe',b'\xfe\xed\xfa\xcf',b'\xca\xfe\xba\xbe',b'\xbe\xba\xfe\xca',b'\xca\xfe\xba\xbf',b'\xbf\xba\xfe\xca'):
        raise RuntimeError('macOS용 Mach-O 실행 파일이 아닙니다.')
    if not (app/'Contents/Resources/Fonts/Jua-Regular.ttf').is_file():raise RuntimeError('동봉 글꼴이 없습니다.')
    return info

def install(destination=None,online=False,universal=False):
    if sys.platform!='darwin':raise RuntimeError('이 설치기는 Mac에서 실행해야 합니다. Windows에서는 Mac 실행 파일을 빌드하지 않습니다.')
    if mac_version(platform.mac_ver()[0])<MIN_MAC:raise RuntimeError('macOS 14.4 이상이 필요합니다.')
    if not (3,10)<=sys.version_info[:2]<(3,16):raise RuntimeError('Python 3.10~3.15가 필요합니다.')
    if os.geteuid()==0:raise RuntimeError('sudo 없이 일반 사용자로 설치해 주세요.')
    if running_default():raise RuntimeError('실행 중인 런처를 메뉴 막대의 「DeckLauncher 종료」로 끝낸 뒤 다시 설치해 주세요.')
    tools=subprocess.run(['/usr/bin/xcode-select','-p'],capture_output=True,text=True)
    if tools.returncode!=0:raise RuntimeError('최초 빌드에 Apple Command Line Tools가 필요합니다. 터미널에서 xcode-select --install을 실행하고 설치가 끝난 뒤 다시 시도해 주세요.')
    target=Path(destination or Path.home()/'Applications/DeckLauncher.app').expanduser().absolute()
    if target.suffix.lower()!='.app' or target.is_symlink():raise RuntimeError('설치 위치는 심볼릭 링크가 아닌 .app 경로여야 합니다.')
    if target.exists():
        with (target/'Contents/Info.plist').open('rb') as stream:old=plistlib.load(stream)
        if old.get('CFBundleIdentifier')!=BUNDLE_ID:raise RuntimeError('같은 위치에 다른 앱이 있습니다. --destination으로 다른 위치를 골라 주세요.')
    cache=Path.home()/'Library/Caches/DeckLauncher'
    cache.mkdir(parents=True,exist_ok=True)
    key=hashlib.sha256((str(Path(sys.executable).resolve())+str(sys.version_info[:2])+platform.machine()).encode()).hexdigest()[:12]
    environment=cache/('build-python-'+key)
    venv.EnvBuilder(with_pip=True).create(environment)
    python=environment/'bin/python'
    wheels=ROOT/'wheels'
    if not online:
        records=json.loads((ROOT/'wheels-SHA256.json').read_text(encoding='utf-8'))
        for item in records:
            if hashlib.sha256((wheels/item['File']).read_bytes()).hexdigest()!=item['SHA256']:raise RuntimeError('구성요소의 해시가 다릅니다. 패키지를 다시 풀어 주세요.')
    command=[str(python),'-m','pip','--disable-pip-version-check','--no-input','install']
    if not online:command+=['--no-index','--find-links',str(wheels)]
    subprocess.run(command+['-r',str(ROOT/'requirements.txt')],check=True)
    with tempfile.TemporaryDirectory(prefix='app-build-',dir=cache) as temp:
        stage=Path(temp)
        command=[str(python),'-m','PyInstaller','--noconfirm','--clean','--noupx','--windowed','--onedir',
                 '--name','DeckLauncher','--osx-bundle-identifier',BUNDLE_ID,
                 '--distpath',str(stage/'dist'),'--workpath',str(stage/'work'),'--specpath',str(stage/'spec'),
                 '--icon',str(ROOT/'assets/launcher.icns'),'--hidden-import','installer','--hidden-import','PySide6.QtTest','--hidden-import','unittest.mock']
        if universal:command+=['--target-arch','universal2']
        for name in ('Fonts','assets','tests','third_party_licenses'):
            command+=['--add-data',str(ROOT/name)+':'+name]
        for name in ('README.md','LICENSE','THIRD_PARTY_NOTICES.md'):
            command+=['--add-data',str(ROOT/name)+':.']
        command.append(str(ROOT/'run.py'))
        print('독립 실행 가능한 DeckLauncher.app을 만드는 중입니다…',flush=True)
        subprocess.run(command,cwd=ROOT,check=True)
        app=stage/'dist/DeckLauncher.app';plist=app/'Contents/Info.plist'
        with plist.open('rb') as stream:info=plistlib.load(stream)
        info.update(CFBundleDisplayName='나의 작은 런처',LSMinimumSystemVersion='14.4',NSHighResolutionCapable=True,
                    NSPrincipalClass='NSApplication',LSUIElement=False,
                    NSHumanReadableCopyright='© 2026 enedaze-sub · CC BY-NC 4.0; Qt/fonts have separate licenses')
        with plist.open('wb') as stream:plistlib.dump(info,stream)
        verify_bundle(app)
        subprocess.run(['/usr/bin/codesign','--force','--deep','--sign','-',str(app)],check=True)
        subprocess.run(['/usr/bin/codesign','--verify','--deep','--strict',str(app)],check=True)
        target.parent.mkdir(parents=True,exist_ok=True)
        incoming=target.with_name('.DeckLauncher-incoming-'+uuid.uuid4().hex+'.app')
        backup=None
        try:
            shutil.copytree(app,incoming,symlinks=True);verify_bundle(incoming)
            if target.exists():
                backup=target.with_name('.DeckLauncher-previous-'+uuid.uuid4().hex+'.app');target.rename(backup)
            try:incoming.rename(target)
            except Exception:
                if backup:backup.rename(target)
                raise
        finally:
            if incoming.exists():shutil.rmtree(incoming)
    # Register the actual app bundle with Launch Services; no Dock database edits.
    registration=Path('/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister')
    if registration.is_file():subprocess.run([str(registration),'-f',str(target)],check=False)
    print('\n설치했습니다: '+str(target))
    print('Finder에서 앱을 실행한 뒤 Dock 아이콘의 옵션 → Dock에 유지를 선택하세요.')
    print('완성된 .app에는 Python과 Qt가 포함됩니다. 이후 실행에는 Python 설치가 필요하지 않습니다.')
    print('등록 정보: ~/Library/Application Support/DeckLauncher (기존 데이터는 수정하지 않았습니다)')
    if backup:print('이전 앱 보관 위치: '+str(backup))
    return target

def main():
    parser=argparse.ArgumentParser(description='Build/install DeckLauncher for macOS')
    parser.add_argument('--destination',help='설치할 .app 경로; 기본 ~/Applications/DeckLauncher.app')
    parser.add_argument('--online',action='store_true',help='동봉 wheel 대신 PyPI에서 설치')
    parser.add_argument('--universal',action='store_true',help='universal2 Python으로 양쪽 CPU용 앱 만들기')
    args=parser.parse_args()
    try:install(args.destination,args.online,args.universal)
    except (RuntimeError,OSError,subprocess.CalledProcessError,ValueError,KeyError) as ex:print('\n설치 중 확인이 필요합니다: '+str(ex),file=sys.stderr);return 1
    return 0
if __name__=='__main__':raise SystemExit(main())
