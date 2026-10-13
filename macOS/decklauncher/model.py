from __future__ import annotations

import copy
import json
import math
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
import uuid
import zipfile

DESIGNS = [('classic', '기본 부채꼴'), ('cat', '냥냥 포켓'), ('neon', '사이버 HUD'),
           ('paper', '다이어리 스크랩'), ('modern', '글래스 도크'), ('space', '별빛 궤도')]
COLORS = [
    ('민트 나이트', '#111C2B', '#2A3B50', '#6DE0D4', '#F0F5FC'),
    ('라벤더 문', '#242038', '#403755', '#C7ACF8', '#F6F0FF'),
    ('딸기 밀크', '#F4E6EC', '#EAD2DF', '#AC3C6C', '#542A40'),
    ('바닐라 라테', '#EEE6D5', '#DDD1B9', '#80552B', '#443526'),
    ('네온 오빗', '#151523', '#282737', '#D9EE77', '#F4F5E9'),
    ('클라우드 화이트', '#E7EEF8', '#FFFFFF', '#4F74F7', '#1E2940'),
    ('피치 크림', '#FFF5EC', '#FFE3D3', '#F2879B', '#6A4A44'),
    ('네온 민트', '#0A0F1F', '#16223D', '#35F0FF', '#E8FBFF'),
    ('크림 페이퍼', '#F3ECDA', '#FFFDF5', '#E0786A', '#4A3B31'),
    ('별빛 보라', '#100C2A', '#2B2360', '#FFD36E', '#F4EFFF'),
]

def background():
    return dict(ImageFile=None, Zoom=1, OffsetX=0, OffsetY=0, Darkness=.42)

def theme():
    n,b,s,a,t = COLORS[0]
    return dict(Name=n, Background=b, Surface=s, Accent=a, Text=t, Design='classic',
                Frame='rounded', Slots=7, IconSize=40, Radius=301, Gap=.7,
                ShowLabels=True, ShowNotes=True)

def entry(name='새 항목', folder=False, parent=None, **values):
    item = dict(Id=uuid.uuid4().hex, ParentId=parent, Name=name, IsFolder=folder,
                Target='', Arguments='', WorkingDirectory='', Note='', IconFile=None,
                Background=background())
    item.update(values)
    return item

def safe_image(name):
    if name is not None and (not isinstance(name, str) or not name or
                             '/' in name or '\\' in name or ':' in name or name in ('.', '..')):
        raise ValueError('잘못된 이미지 파일 이름입니다.')

def check_number(value, low, high):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and low <= value <= high

def validate_background(bg):
    safe_image(bg.get('ImageFile'))
    for k,lo,hi in [('Zoom',1,4), ('OffsetX',-1,1), ('OffsetY',-1,1), ('Darkness',0,.85)]:
        if not check_number(bg.get(k),lo,hi): raise ValueError('배경 조정 값이 잘못되었습니다.')

def validate_theme(t):
    for k in ('Background', 'Surface', 'Accent', 'Text'):
        if not re.fullmatch(r'#[\da-fA-F]{6}', t.get(k,'')): raise ValueError('색상 값이 잘못되었습니다.')
    for k,lo,hi in [('Slots',5,9),('IconSize',28,48),('Radius',280,315),('Gap',.3,2.5)]:
        if not check_number(t.get(k),lo,hi): raise ValueError('배치 값이 잘못되었습니다.')
    if int(t['Slots']) != t['Slots']: raise ValueError('칸 수는 정수여야 합니다.')
    if t['Design'] not in dict(DESIGNS) or t['Frame'] not in ('rounded','circle','minimal'):
        raise ValueError('알 수 없는 디자인입니다.')

class Library:
    def __init__(self, data=None):
        self.data = copy.deepcopy(data) if data is not None else {}
        defaults = dict(Version=1, UiRevision=6, HideAfterLaunch=True, WheelDialEnabled=False,
                        CustomThemes=[], Fonts=dict(Launcher='', Interface=''),
                        Background=background(), Theme=theme(), Entries=[],
                        MacPlacement=dict(Edge='auto', Align='cursor', Offset=0))
        for k,v in defaults.items(): self.data.setdefault(k,v)
        self.data['Theme'] = {**theme(), **self.data['Theme']}
        if self.data['Theme']['Design'] == 'glass': self.data['Theme']['Design']='modern'
        for item in self.entries:
            for k,v in entry().items(): item.setdefault(k, v)
            item['Background'] = {**background(), **item['Background']}
        for custom in self.data['CustomThemes']:
            custom['Settings']={**theme(), **custom['Settings']}
            if custom['Settings']['Design']=='glass':custom['Settings']['Design']='modern'
        self.validate()

    @property
    def entries(self): return self.data['Entries']
    @property
    def theme(self): return self.data['Theme']
    def find(self, item_id): return next((e for e in self.entries if e['Id']==item_id), None)
    def children(self, parent=None): return [e for e in self.entries if e['ParentId']==parent]
    def can_move(self, item_id, parent):
        seen={item_id}
        while parent is not None:
            if parent in seen: return False
            seen.add(parent)
            folder=self.find(parent)
            if not folder or not folder['IsFolder']: return False
            parent=folder['ParentId']
        return True

    def validate(self):
        if not isinstance(self.entries,list) or len(self.entries)>10000: raise ValueError('잘못된 항목 목록입니다.')
        ids=set()
        for e in self.entries:
            if not isinstance(e['Id'],str) or not e['Id'] or e['Id'] in ids: raise ValueError('항목 ID가 중복되었습니다.')
            ids.add(e['Id'])
            if not isinstance(e['Name'],str) or not e['Name'].strip(): raise ValueError('이름이 없는 항목입니다.')
            for k in ('Target','Arguments','WorkingDirectory','Note'):
                if not isinstance(e[k],str): raise ValueError('항목 형식이 잘못되었습니다.')
            safe_image(e['IconFile']); validate_background(e['Background'])
        for e in self.entries:
            if not self.can_move(e['Id'],e['ParentId']): raise ValueError('폴더 구조가 잘못되었습니다.')
        validate_theme(self.theme); validate_background(self.data['Background'])
        for custom in self.data['CustomThemes']: validate_theme(custom['Settings'])
        placement=self.data['MacPlacement']
        if placement.get('Edge') not in ('auto','bottom','top','left','right') or placement.get('Align') not in ('cursor','center') or not check_number(placement.get('Offset'),-250,250):
            raise ValueError('독 배치 설정이 잘못되었습니다.')
        for k in ('Launcher','Interface'):
            if not isinstance(self.data['Fonts'].get(k,''),str) or len(self.data['Fonts'].get(k,''))>120:
                raise ValueError('글꼴 이름이 잘못되었습니다.')

    def move(self, ids, parent, anchor=None, after=False):
        ids=set(ids); chosen=[e for e in self.entries if e['Id'] in ids]
        if not chosen or len(chosen)!=len(ids) or any(not self.can_move(e['Id'],parent) for e in chosen): return False
        target=self.find(anchor)
        if anchor and (not target or anchor in ids or target['ParentId']!=parent): return False
        remaining=[e for e in self.entries if e['Id'] not in ids]
        at=remaining.index(target)+int(after) if target else len(remaining)
        for e in chosen: e['ParentId']=parent
        self.data['Entries']=remaining[:at]+chosen+remaining[at:]
        return True

    def remove(self, ids):
        removed=set(ids)
        while True:
            more={e['Id'] for e in self.entries if e['ParentId'] in removed}
            if more <= removed: break
            removed |= more
        self.data['Entries']=[e for e in self.entries if e['Id'] not in removed]

    def trail(self, parent):
        names=[]
        while parent:
            e=self.find(parent)
            if not e: break
            names.insert(0,e['Name']); parent=e['ParentId']
        return '라이브러리' + (' / ' + ' / '.join(names) if names else '')

    def images(self):
        return {x for x in [self.data['Background']['ImageFile']] +
                [p for e in self.entries for p in (e['IconFile'],e['Background']['ImageFile'])] if x}

class Store:
    def __init__(self, directory=None):
        default=Path.home()/'Library'/'Application Support'/'DeckLauncher'
        self.directory=Path(directory or default)
        self.icons=self.directory/'icons'
        self.file=self.directory/'library.json'
    def load(self):
        if not self.file.exists(): return Library()
        return Library(json.loads(self.file.read_text(encoding='utf-8-sig')))
    def save(self, library):
        library.validate(); self.icons.mkdir(parents=True,exist_ok=True)
        raw=json.dumps(library.data,ensure_ascii=False,indent=2)
        fd,name=tempfile.mkstemp(prefix='.library-',suffix='.tmp',dir=self.directory)
        try:
            with os.fdopen(fd,'w',encoding='utf-8') as f: f.write(raw); f.flush(); os.fsync(f.fileno())
            if self.file.exists(): shutil.copy2(self.file,str(self.file)+'.bak')
            os.replace(name,self.file)
        finally:
            if Path(name).exists(): Path(name).unlink()
    def copy_image(self,path):
        self.icons.mkdir(parents=True,exist_ok=True)
        name=uuid.uuid4().hex+Path(path).suffix.lower()
        shutil.copy2(path,self.icons/name); return name
    def backup(self,path,library):
        library.validate()
        with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED) as z:
            z.writestr('library.json',json.dumps(library.data,ensure_ascii=False,indent=2))
            for name in library.images():
                if (self.icons/name).is_file(): z.write(self.icons/name,'icons/'+name)
    def restore(self,path):
        with zipfile.ZipFile(path) as z:
            members=z.infolist()
            if len(members)>12000 or sum(i.file_size for i in members)>512*1024*1024:
                raise ValueError('백업이 너무 큽니다. 최대 512MB입니다.')
            if len({i.filename for i in members})!=len(members): raise ValueError('중복된 백업 파일입니다.')
            data=Library(json.loads(z.read('library.json').decode('utf-8-sig')))
            names=set(z.namelist())
            for name in data.images():
                if 'icons/'+name not in names: raise ValueError('백업에 이미지가 빠져 있습니다: '+name)
            with tempfile.TemporaryDirectory() as tmp:
                for name in data.images(): (Path(tmp)/name).write_bytes(z.read('icons/'+name))
                self.icons.mkdir(parents=True,exist_ok=True)
                for name in data.images(): shutil.copy2(Path(tmp)/name,self.icons/name)
                self.save(data)
        return data

def windows_target(target):
    return bool(re.match(r'^[A-Za-z]:[\\/]|^\\\\',target)) or target.lower().endswith(('.exe','.lnk','.bat','.cmd'))
