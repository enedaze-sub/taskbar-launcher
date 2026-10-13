from __future__ import annotations
import configparser
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
from urllib.parse import urlparse
from .model import entry, windows_target

def desktop_values(path):
    parser=configparser.ConfigParser(interpolation=None,strict=False)
    parser.optionxform=str
    parser.read(path,encoding='utf-8-sig')
    section=parser['Desktop Entry']
    if section.get('Type','Application') != 'Application': raise ValueError('앱 바로가기가 아닙니다.')
    def val(k):
        text=section.get(k,'')
        return re.sub(r'\\([sntr\\])',lambda m:{'s':' ','n':'\n','t':'\t','r':'\r','\\':'\\'}[m[1]],text)
    lang=os.environ.get('LC_MESSAGES',os.environ.get('LANG','')).split('.')[0]
    name=section.get('Name['+lang+']',section.get('Name['+lang.split('_')[0]+']',val('Name')))
    return dict(Name=name or Path(path).stem, Exec=val('Exec'), Icon=val('Icon'),
                Path=val('Path'), Terminal=val('Terminal').lower()=='true',
                Hidden=val('Hidden').lower()=='true', NoDisplay=val('NoDisplay').lower()=='true',
                TryExec=val('TryExec'))

def exec_words(text):
    # Desktop Exec is its own format: single quotes are literal, and double-quoted
    # $, `, quote and backslash escapes are removed without shell expansion.
    words=[];word=[];quoted=False;escaped=False;started=False
    for char in text:
        if escaped:word.append(char);escaped=False;started=True
        elif char=='\\':escaped=True;started=True
        elif char=='"':quoted=not quoted;started=True
        elif char.isspace() and not quoted:
            if started:words.append(''.join(word));word=[];started=False
        else:word.append(char);started=True
    if quoted or escaped:raise ValueError('바로가기의 따옴표 또는 경로 표기가 잘못되었습니다.')
    if started:words.append(''.join(word))
    return words

def desktop_command(path,extra=''):
    values=desktop_values(path)
    words=exec_words(values['Exec']); result=[]
    for word in words:
        if word in ('%f','%F','%u','%U','%d','%D','%n','%N','%m','%v'): continue
        if word=='%i':
            if values['Icon']: result += ['--icon',values['Icon']]
            continue
        word=word.replace('%%','\0').replace('%c',values['Name']).replace('%k',str(Path(path).absolute()))
        if re.search(r'%[A-Za-z]',word): raise ValueError('지원하지 않는 바로가기 실행 코드입니다.')
        result.append(word.replace('\0','%'))
    result+=shlex.split(extra)
    if not result: raise ValueError('바로가기에 실행 명령이 없습니다.')
    if values['Terminal']:
        terminal=shutil.which('x-terminal-emulator') or shutil.which('konsole') or shutil.which('gnome-terminal')
        if not terminal: raise ValueError('터미널 앱을 찾을 수 없습니다.')
        result=[terminal,'--' if Path(terminal).name=='gnome-terminal' else '-e']+result
    return result,values['Path'] or None

def import_path(path,parent=None):
    path=Path(path).expanduser().absolute()
    if path.suffix.lower()=='.desktop':
        values=desktop_values(path)
        return entry(values['Name'],parent=parent,Target=str(path),Note='',NativeIcon=values['Icon'])
    return entry(path.stem if path.is_file() else path.name,parent=parent,Target=str(path))

def applications():
    base=Path(os.environ.get('XDG_DATA_HOME',str(Path.home()/'.local/share')))
    dirs=[base/'applications']+[Path(p)/'applications' for p in os.environ.get('XDG_DATA_DIRS','/usr/local/share:/usr/share').split(':') if p]
    dirs += [Path.home()/'.local/share/flatpak/exports/share/applications',Path('/var/lib/flatpak/exports/share/applications'),Path('/var/lib/snapd/desktop/applications')]
    found={}
    for directory in dirs:
        if not directory.is_dir(): continue
        for path in sorted(directory.rglob('*.desktop')):
            key=str(path.relative_to(directory)).replace('/','-')
            if key in found: continue
            found[key]=None  # Hidden user entries override system entries too.
            try:
                v=desktop_values(path)
                if v['Hidden'] or v['NoDisplay'] or not v['Exec']: continue
                if v['TryExec'] and not shutil.which(v['TryExec']): continue
                found[key]=import_path(path)
            except (ValueError,KeyError,configparser.Error,OSError): continue
    return sorted((e for e in found.values() if e),key=lambda e:e['Name'].casefold())

def command(item):
    target=os.path.expandvars(os.path.expanduser(item['Target'].strip()))
    if windows_target(target): raise ValueError('Windows용 경로입니다. 편집에서 리눅스 앱이나 실행 파일로 다시 연결해 주세요.')
    if not target: raise ValueError('실행 대상이 비어 있습니다.')
    args=shlex.split(item.get('Arguments',''))
    cwd=os.path.expandvars(os.path.expanduser(item.get('WorkingDirectory',''))) or None
    parsed=urlparse(target)
    if parsed.scheme and parsed.scheme!='file': return ['xdg-open',target],cwd
    path=Path(target)
    if parsed.scheme=='file': return ['xdg-open',target],cwd
    if path.suffix=='.desktop':
        if not path.is_file(): raise FileNotFoundError('바로가기를 찾을 수 없습니다: '+target)
        if not args and shutil.which('gio'): return ['gio','launch',str(path)],cwd
        cmd,desktop_cwd=desktop_command(path,item.get('Arguments',''))
        return cmd,cwd or desktop_cwd
    if path.exists():
        if path.is_file() and os.access(path,os.X_OK): return [str(path.absolute())]+args,cwd or str(path.parent.absolute())
        if args: raise ValueError('일반 문서나 폴더에는 실행 인수를 사용할 수 없습니다.')
        return ['xdg-open',str(path.absolute())],cwd
    executable=shutil.which(target)
    if executable: return [executable]+args,cwd
    raise FileNotFoundError('실행 파일을 찾을 수 없습니다: '+target)

def start(item):
    cmd,cwd=command(item)
    if cwd and not Path(cwd).is_dir(): raise FileNotFoundError('작업 폴더가 없습니다: '+cwd)
    # Never feed a user-entered executable/argument string to a shell.
    return subprocess.Popen(cmd,cwd=cwd,start_new_session=True)
