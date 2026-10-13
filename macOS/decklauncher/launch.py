"""macOS Launch Services, .app discovery and shell-free command execution."""
from __future__ import annotations
import os
from pathlib import Path
import plistlib
import shlex
import shutil
import subprocess
from urllib.parse import urlparse,unquote
from .model import entry,windows_target

APP_ROOTS=(Path('/Applications'),Path('/System/Applications'),
           Path('/System/Library/CoreServices/Applications'))

def app_values(path):
    path=Path(path)
    try:
        with (path/'Contents/Info.plist').open('rb') as stream:info=plistlib.load(stream)
    except (OSError,plistlib.InvalidFileException,ValueError) as ex:
        raise ValueError('앱 정보를 읽을 수 없습니다: '+path.name) from ex
    if not isinstance(info,dict) or info.get('CFBundlePackageType','APPL')!='APPL':raise ValueError('실행 가능한 Mac 앱 번들이 아닙니다.')
    name=info.get('CFBundleDisplayName') or info.get('CFBundleName') or path.stem
    icon=info.get('CFBundleIconFile','')
    resource=path/'Contents/Resources'/icon if isinstance(icon,str) and icon else None
    if resource and not resource.suffix:resource=resource.with_suffix('.icns')
    return dict(Name=str(name),Icon=str(resource) if resource and resource.is_file() else '',
                BundleId=str(info.get('CFBundleIdentifier','')))

def import_path(path,parent=None):
    path=Path(path).expanduser().absolute()
    if path.suffix.lower()=='.app':
        v=app_values(path)
        return entry(v['Name'],parent=parent,Target=str(path),NativeIcon=v['Icon'],BundleId=v['BundleId'])
    return entry(path.stem if path.is_file() else path.name,parent=parent,Target=str(path))

def applications(roots=None):
    defaults=roots is None
    roots=list(roots) if roots is not None else [*APP_ROOTS,Path.home()/'Applications']
    found={}
    for root in roots:
        if not root.is_dir():continue
        for directory,folders,files in os.walk(root,followlinks=False):
            for name in sorted(folders[:]):
                if name.startswith('.'):
                    folders.remove(name);continue
                if name.lower().endswith('.app'):
                    folders.remove(name);path=Path(directory)/name
                    try:found.setdefault(str(path.resolve()),import_path(path))
                    except (ValueError,OSError):continue
    finder=Path('/System/Library/CoreServices/Finder.app')
    if defaults and finder.is_dir():
        try:found.setdefault(str(finder.resolve()),import_path(finder))
        except (ValueError,OSError):pass
    return sorted(found.values(),key=lambda e:e['Name'].casefold())

def command(item):
    target=os.path.expandvars(os.path.expanduser(item['Target'].strip()))
    if windows_target(target):raise ValueError('Windows용 경로입니다. Mac 앱(.app)이나 실행 파일로 다시 연결해 주세요.')
    if not target:raise ValueError('실행 대상이 비어 있습니다.')
    if target.lower().endswith(('.desktop','.appimage')):raise ValueError('리눅스용 앱입니다. 편집에서 Mac 앱으로 다시 연결해 주세요.')
    args=shlex.split(item.get('Arguments',''))
    cwd=os.path.expandvars(os.path.expanduser(item.get('WorkingDirectory',''))) or None
    parsed=urlparse(target)
    if parsed.scheme and parsed.scheme!='file':
        if args:raise ValueError('웹 주소에는 별도 실행 인수를 사용할 수 없습니다.')
        return ['/usr/bin/open',target],cwd
    if parsed.scheme=='file':
        if parsed.netloc not in ('','localhost'):raise ValueError('로컬 파일 주소를 사용해 주세요.')
        target=unquote(parsed.path)
    path=Path(target)
    if path.suffix.lower()=='.app':
        if not path.is_dir():raise FileNotFoundError('앱을 찾을 수 없습니다: '+target)
        return ['/usr/bin/open','-a',str(path.absolute())]+(['--args']+args if args else []),cwd
    if path.exists():
        if path.is_file() and os.access(path,os.X_OK):return [str(path.absolute())]+args,cwd or str(path.parent.absolute())
        if args:raise ValueError('일반 문서나 폴더에는 실행 인수를 사용할 수 없습니다.')
        return ['/usr/bin/open',str(path.absolute())],cwd
    executable=shutil.which(target)
    if executable:return [executable]+args,cwd
    raise FileNotFoundError('앱이나 실행 파일을 찾을 수 없습니다: '+target)

def start(item):
    cmd,cwd=command(item)
    if cwd and not Path(cwd).is_dir():raise FileNotFoundError('작업 폴더가 없습니다: '+cwd)
    return subprocess.Popen(cmd,cwd=cwd,start_new_session=True)
