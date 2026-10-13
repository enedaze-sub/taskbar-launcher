from __future__ import annotations
import copy
from pathlib import Path
from PySide6.QtCore import Qt,Signal,QMimeData,QTimer,QPointF
from PySide6.QtGui import QColor,QFont,QDrag,QIcon
from PySide6.QtWidgets import (QDialog,QVBoxLayout,QHBoxLayout,QLabel,QPushButton,QLineEdit,
    QTextEdit,QFormLayout,QComboBox,QCheckBox,QDialogButtonBox,QTreeWidget,QTreeWidgetItem,
    QAbstractItemView,QSplitter,QFileDialog,QMessageBox,QListWidget,QListWidgetItem,QTabWidget,
    QScrollArea,QWidget,QGridLayout,QSlider,QColorDialog,QFontComboBox,QSpinBox,QDoubleSpinBox,QMenu)
from .model import Library,COLORS,DESIGNS,entry,background,windows_target
from .launch import applications,import_path,desktop_values
from .view import Canvas,MIME,mix
from .fonts import resolved

def style(widget,lib):
    t=lib.theme;b,s,a,x=[t[k] for k in ('Background','Surface','Accent','Text')]
    hover=mix(s,a,.2).name();border=mix(s,x,.2).name();muted=mix(x,b,.25).name()
    family=resolved(lib)
    widget.setFont(QFont(family,11))
    widget.setStyleSheet(f'''
        QWidget {{color:{x};font-family:"{family}";font-size:14px;}}
        QDialog,QMainWindow {{background:{b};}}
        QLabel[heading="true"] {{font-family:"{resolved(lib,True)}";font-size:27px;}}
        QLabel[muted="true"] {{color:{muted};}}
        QPushButton,QToolButton {{background:{s};border:1px solid {border};border-radius:12px;padding:9px 14px;}}
        QPushButton:hover,QToolButton:hover {{background:{hover};border-color:{a};}}
        QPushButton:pressed {{background:{a};color:{b};}}
        QPushButton:disabled {{color:{muted};}}
        QPushButton[primary="true"] {{background:{a};color:{b};font-weight:bold;}}
        QLineEdit,QTextEdit,QComboBox,QSpinBox,QDoubleSpinBox {{background:{s};border:1px solid {border};border-radius:9px;padding:8px;selection-background-color:{a};selection-color:{b};}}
        QComboBox::drop-down {{border:none;width:26px;}}
        QComboBox QAbstractItemView,QMenu {{background:{s};border:1px solid {border};selection-background-color:{hover};padding:6px;}}
        QMenu::item {{padding:9px 24px;border-radius:8px;}}
        QMenu::item:selected {{background:{hover};}}
        QTreeWidget,QListWidget {{background:{s};border:1px solid {border};border-radius:14px;padding:8px;}}
        QTreeWidget::item,QListWidget::item {{padding:8px;border-radius:8px;min-height:28px;}}
        QTreeWidget::item:selected,QListWidget::item:selected {{background:{hover};color:{x};}}
        QTreeWidget::item:hover,QListWidget::item:hover {{background:{hover};}}
        QHeaderView::section {{background:{s};border:none;padding:10px;color:{muted};}}
        QTabWidget::pane {{border:1px solid {border};border-radius:14px;padding:10px;}}
        QTabBar::tab {{background:{s};padding:12px 20px;border-radius:8px;margin:3px;}}
        QTabBar::tab:selected {{background:{hover};color:{a};}}
        QCheckBox {{spacing:10px;}}
        QCheckBox::indicator {{width:20px;height:20px;border:1px solid {border};border-radius:6px;background:{s};}}
        QCheckBox::indicator:checked {{background:{a};border:3px solid {hover};}}
        QSlider::groove:horizontal {{height:5px;background:{s};border-radius:2px;}}
        QSlider::handle:horizontal {{width:17px;background:{a};margin:-6px 0;border-radius:8px;}}
        QScrollArea {{background:transparent;border:none;}}
        QScrollBar:vertical {{background:{b};width:10px;}}
        QScrollBar::handle:vertical {{background:{border};min-height:25px;border-radius:5px;}}
        QToolTip {{background:{s};color:{x};border:1px solid {a};padding:8px;}}
    ''')

def button(text,action=None,primary=False):
    b=QPushButton(text);b.setCursor(Qt.PointingHandCursor);b.setProperty('primary',primary)
    if action:b.clicked.connect(action)
    return b
def label(text,heading=False,muted=False):
    w=QLabel(text);w.setWordWrap(True);w.setProperty('heading',heading);w.setProperty('muted',muted);return w
def choose_file(parent,title,filter='모든 파일 (*)'):
    return QFileDialog.getOpenFileName(parent,title,'',filter,options=QFileDialog.DontUseNativeDialog)[0]
def choose_save(parent,title,name,filter):
    return QFileDialog.getSaveFileName(parent,title,name,filter,options=QFileDialog.DontUseNativeDialog)[0]
def error(parent,ex):QMessageBox.warning(parent,'확인해 주세요',str(ex))

class Dialog(QDialog):
    def __init__(self,parent,lib,title,width=760,height=600):
        super().__init__(parent);self.lib=lib;self.setWindowTitle(title);self.resize(width,height);style(self,lib)
        self.body=QVBoxLayout(self);self.body.setContentsMargins(26,24,26,24);self.body.setSpacing(15)
    def exec(self):
        result=super().exec()
        # Dialogs have an owner; release their preview widgets and image caches
        # after callers have consumed the draft/result in this event turn.
        self.deleteLater()
        return result
    def footer(self):
        buttons=QDialogButtonBox(QDialogButtonBox.Cancel|QDialogButtonBox.Save)
        buttons.button(QDialogButtonBox.Cancel).setText('취소');buttons.button(QDialogButtonBox.Save).setText('저장')
        buttons.accepted.connect(self.accept);buttons.rejected.connect(self.reject);self.body.addWidget(buttons)

class Help(Dialog):
    def __init__(self,parent,lib):
        super().__init__(parent,lib,'리눅스 앱 등록 도움말',790,660)
        self.body.addWidget(label('앱을 등록하는 세 가지 방법',True))
        self.body.addWidget(label('리눅스에서는 Windows의 .exe 대신 앱 바로가기(.desktop)나 실행 파일을 사용해요.',muted=True))
        for title,text in [
            ('01  설치된 앱에서 고르기','「설치된 앱」을 누르고 원하는 앱을 찾아 선택하세요. 여러 개를 한 번에 등록할 수 있습니다. Flatpak·Snap 앱도 목록에서 확인할 수 있어요.'),
            ('02  파일 끌어다 놓기','파일 관리자에서 .desktop 바로가기, 실행 가능한 AppImage·스크립트, 문서 또는 폴더를 설정 목록에 끌어다 놓으세요. 「직접 등록」의 찾아보기로 선택해도 됩니다.'),
            ('03  직접 경로 입력하기','예: /usr/bin/firefox 또는 /home/내이름/앱/MyApp.AppImage\n명령 이름(firefox)이나 웹 주소(https://...)도 사용할 수 있습니다. 실행 인수는 아래 별도 칸에 입력하세요.')]:
            card=QWidget();box=QVBoxLayout(card);box.setContentsMargins(18,12,18,12)
            card.setStyleSheet('QWidget {background:'+lib.theme['Surface']+';border-radius:14px;}')
            box.addWidget(label(title,True));box.addWidget(label(text));self.body.addWidget(card)
        self.body.addWidget(label('AppImage가 실행되지 않으면 파일 속성에서 「프로그램으로 실행 허용」을 켜 주세요. Ubuntu 24.04에서 구형 AppImage는 libfuse2t64가 필요할 수 있습니다.\nWindows 백업을 가져온 뒤에는 해당 항목의 실행 대상을 리눅스 앱으로 다시 연결하세요.',muted=True))
        self.body.addWidget(button('알겠어요',self.accept,True))

class Editor(Dialog):
    def __init__(self,parent,store,lib,item=None,folder=False,parent_id=None):
        super().__init__(parent,lib,'항목 편집' if item else '폴더 만들기' if folder else '직접 등록',760,730)
        self.store=store;self.result=copy.deepcopy(item) if item else entry(folder=folder,parent=parent_id)
        self.body.addWidget(label('이름과 실행 대상을 정해 주세요' if not self.result['IsFolder'] else '폴더에 이름을 붙여 주세요',True))
        self.body.addWidget(label('메모는 아이콘 위에 마우스를 올리면 표시됩니다.',muted=True))
        form=QFormLayout();self.name=QLineEdit(self.result['Name']);self.note=QTextEdit(self.result['Note']);self.note.setMaximumHeight(90)
        form.addRow('이름',self.name)
        self.target=QLineEdit(self.result['Target']);self.args=QLineEdit(self.result['Arguments']);self.cwd=QLineEdit(self.result['WorkingDirectory'])
        if not self.result['IsFolder']:
            row=QHBoxLayout();row.addWidget(self.target,1);row.addWidget(button('찾아보기',self.choose_target));form.addRow('실행 대상',row)
            form.addRow('실행 인수',self.args);form.addRow('작업 폴더',self.cwd)
            if windows_target(self.result['Target']):self.body.addWidget(label('이 항목은 Windows용 경로입니다. 리눅스 앱이나 실행 파일로 다시 연결해 주세요.',muted=True))
        form.addRow('간략한 메모',self.note)
        self.parent_choice=QComboBox();self.parent_choice.addItem('라이브러리 · 맨 바깥',None)
        for e in lib.entries:
            if e['IsFolder'] and lib.can_move(self.result['Id'],e['Id']):self.parent_choice.addItem(lib.trail(e['Id']),e['Id'])
        self.parent_choice.setCurrentIndex(max(0,self.parent_choice.findData(self.result['ParentId'])));form.addRow('담을 폴더',self.parent_choice)
        self.body.addLayout(form)
        iconrow=QHBoxLayout();self.iconlabel=label('기본 아이콘 사용' if not self.result['IconFile'] else '사용자 이미지 선택됨')
        iconrow.addWidget(self.iconlabel,1);iconrow.addWidget(button('이미지 선택',self.choose_icon));iconrow.addWidget(button('기본 아이콘',self.clear_icon));self.body.addLayout(iconrow)
        self.body.addWidget(button('이 칸의 배경 꾸미기',self.edit_background))
        self.body.addWidget(button('등록 방법 보기',lambda:Help(self,lib).exec()))
        self.body.addStretch();self.footer()
    def choose_target(self):
        path=choose_file(self,'실행 파일이나 바로가기 선택')
        if path:
            self.target.setText(path)
            if path.endswith('.desktop'):
                try:
                    v=desktop_values(path);self.result['NativeIcon']=v['Icon']
                    if self.name.text() in ('새 항목',''):self.name.setText(v['Name'])
                except Exception as ex:error(self,ex)
    def choose_icon(self):
        path=choose_file(self,'아이콘 이미지 선택','이미지 (*.png *.jpg *.jpeg *.webp *.ico *.svg)')
        if path:
            try:self.result['IconFile']=self.store.copy_image(path);self.iconlabel.setText('사용자 이미지 선택됨')
            except OSError as ex:error(self,ex)
    def clear_icon(self):self.result['IconFile']=None;self.iconlabel.setText('기본 아이콘 사용')
    def edit_background(self):
        lib=Library(self.lib.data);item=lib.find(self.result['Id'])
        if not item:lib.entries.append(copy.deepcopy(self.result))
        else:item.update(copy.deepcopy(self.result))
        dialog=BackgroundDialog(self,self.store,lib,self.result['Id'])
        if dialog.exec():self.result['Background']=dialog.result
    def accept(self):
        if not self.name.text().strip():error(self,'이름을 입력해 주세요.');return
        if not self.result['IsFolder'] and not self.target.text().strip():error(self,'실행 대상을 입력해 주세요.');return
        self.result.update(Name=self.name.text().strip(),Target=self.target.text().strip(),Arguments=self.args.text().strip(),
                           WorkingDirectory=self.cwd.text().strip(),Note=self.note.toPlainText(),ParentId=self.parent_choice.currentData())
        super().accept()

class Apps(Dialog):
    def __init__(self,parent,store,lib):
        super().__init__(parent,lib,'설치된 앱 등록',760,650);self.selected=[]
        self.body.addWidget(label('이 PC에 설치된 앱',True));self.body.addWidget(label('원하는 앱을 고르세요. Ctrl·Shift로 여러 개를 선택할 수 있습니다.',muted=True))
        self.search=QLineEdit();self.search.setPlaceholderText('이름으로 검색');self.body.addWidget(self.search)
        self.list=QListWidget();self.list.setSelectionMode(QAbstractItemView.ExtendedSelection);self.body.addWidget(self.list,1)
        images=Canvas(store,lib).images
        for e in applications():
            row=QListWidgetItem(images.icon(e),e['Name']);row.setData(Qt.UserRole,e);row.setToolTip(e['Target']);self.list.addItem(row)
        self.search.textChanged.connect(lambda s:[self.list.item(i).setHidden(s.casefold() not in self.list.item(i).text().casefold()) for i in range(self.list.count())])
        if not self.list.count():self.body.addWidget(label('설치된 앱을 찾지 못했습니다. 직접 등록 또는 파일 끌어다 놓기를 사용해 주세요.'))
        self.footer()
    def accept(self):
        self.selected=[copy.deepcopy(i.data(Qt.UserRole)) for i in self.list.selectedItems()]
        if not self.selected:return
        super().accept()

class BackgroundCanvas(Canvas):
    adjusted=Signal(float,float);zoomed=Signal(int)
    def __init__(self,*args,**kwargs):super().__init__(*args,interactive=False,**kwargs);self.last=None;self.setCursor(Qt.SizeAllCursor)
    def mousePressEvent(self,event):self.last=event.position()
    def mouseMoveEvent(self,event):
        if self.last and event.buttons()&Qt.LeftButton:
            delta=event.position()-self.last;self.last=event.position();self.adjusted.emit(delta.x(),delta.y())
    def mouseReleaseEvent(self,event):self.last=None
    def wheelEvent(self,event):self.zoomed.emit(1 if event.angleDelta().y()>0 else -1)

class BackgroundDialog(Dialog):
    def __init__(self,parent,store,lib,item_id=None):
        super().__init__(parent,lib,'이 칸의 배경 꾸미기' if item_id else '런처 배경 꾸미기',1040,790)
        self.draft=Library(lib.data);self.item_id=item_id
        self.result=self.draft.find(item_id)['Background'] if item_id else self.draft.data['Background']
        self.body.addWidget(label('미리보기를 보며 이미지를 맞춰 보세요',True))
        row=QHBoxLayout();row.addWidget(label('이미지를 끌어 위치를 조정하고 휠로 확대할 수 있어요.',muted=True),1)
        row.addWidget(button('이미지 선택',lambda:self.choose(store)));row.addWidget(button('기본 배경',self.reset));self.body.addLayout(row)
        self.canvas=BackgroundCanvas(store,self.draft);self.canvas.setMinimumHeight(320)
        if item_id:self.canvas.current=self.draft.find(item_id)['ParentId'];items=self.draft.children(self.canvas.current);self.canvas.page=next(i for i,e in enumerate(items) if e['Id']==item_id)//int(self.draft.theme['Slots'])
        self.body.addWidget(self.canvas,1);self.canvas.adjusted.connect(self.adjust)
        self.sliders={};form=QFormLayout()
        for k,name,lo,hi in [('Zoom','확대',100,400),('OffsetX','가로 위치',-100,100),('OffsetY','세로 위치',-100,100),('Darkness','어둡게',0,85)]:
            s=QSlider(Qt.Horizontal);s.setRange(lo,hi);s.setValue(round(self.result[k]*100));s.valueChanged.connect(lambda v,key=k:self.set_value(key,v/100));form.addRow(name,s);self.sliders[k]=s
        self.canvas.zoomed.connect(lambda d:self.sliders['Zoom'].setValue(self.sliders['Zoom'].value()+d*10))
        self.body.addLayout(form);self.footer()
    def set_value(self,key,value):self.result[key]=value;self.canvas.update()
    def choose(self,store):
        path=choose_file(self,'배경 이미지 선택','이미지 (*.png *.jpg *.jpeg *.webp *.bmp)')
        if path:
            try:
                name=store.copy_image(path)
                if self.canvas.images.image(name).isNull():raise ValueError('이미지를 읽을 수 없습니다.')
                self.result['ImageFile']=name;self.canvas.update()
            except Exception as ex:error(self,ex)
    def reset(self):
        self.result.update(background())
        for k,s in self.sliders.items():s.setValue(round(self.result[k]*100))
        self.canvas.update()
    def adjust(self,dx,dy):
        shapes=self.canvas.geometry();items=self.canvas.visible();d=self.result
        crop=next((shapes[i][0] for i,e in enumerate(items) if e['Id']==self.item_id),None)
        from .view import shell
        bounds=(crop or shell(self.draft.theme['Design'])).boundingRect()
        pix=self.canvas.images.image(d.get('ImageFile'))
        if pix.isNull():return
        scale=max(bounds.width()/pix.width(),bounds.height()/pix.height())*d['Zoom']
        for k,delta,excess in [('OffsetX',dx/self.canvas.scale(),pix.width()*scale-bounds.width()),('OffsetY',dy/self.canvas.scale(),pix.height()*scale-bounds.height())]:
            if excess>1:self.sliders[k].setValue(round(max(-1,min(1,d[k]+delta*2/excess))*100))

class ThemeDialog(Dialog):
    def __init__(self,parent,store,lib):
        super().__init__(parent,lib,'색상 · UI 디자인',1100,820);self.draft=Library(lib.data)
        self.body.addWidget(label('색과 모양을 따로 골라 보세요',True))
        self.preview=Canvas(store,self.draft,interactive=False);self.preview.setMinimumHeight(260);self.body.addWidget(self.preview,1)
        self.tabs=QTabWidget();self.body.addWidget(self.tabs)
        palettes=QWidget();grid=QGridLayout(palettes)
        for i,c in enumerate(COLORS):
            b=button(c[0],lambda checked=False,color=c:self.palette(color));b.setStyleSheet(f'QPushButton{{background:{c[1]};color:{c[4]};border:2px solid {c[3]};padding:12px;border-radius:12px;}}')
            grid.addWidget(b,i//5,i%5)
        self.tabs.addTab(palettes,'기본 색상 · 10')
        designs=QWidget();grid=QGridLayout(designs)
        for i,(key,name) in enumerate(DESIGNS):grid.addWidget(button(name,lambda checked=False,k=key:self.design(k)),i//3,i%3)
        self.tabs.addTab(designs,'UI 디자인 · 6')
        custom=QWidget();form=QFormLayout(custom);self.color_buttons={}
        for key,name in [('Background','배경'),('Surface','칸'),('Accent','강조'),('Text','글자')]:
            b=button(self.draft.theme[key],lambda checked=False,k=key:self.pick_color(k));self.color_buttons[key]=b;form.addRow(name,b)
        row=QHBoxLayout();self.slot_count=QSpinBox();self.slot_count.setRange(5,9);self.slot_count.setValue(int(self.draft.theme['Slots']));self.slot_count.valueChanged.connect(lambda v:self.setting('Slots',v));row.addWidget(label('칸 수'));row.addWidget(self.slot_count)
        self.icon_size=QSpinBox();self.icon_size.setRange(28,48);self.icon_size.setValue(int(self.draft.theme['IconSize']));self.icon_size.valueChanged.connect(lambda v:self.setting('IconSize',v));row.addWidget(label('아이콘 크기'));row.addWidget(self.icon_size)
        self.frame=QComboBox()
        for name,key in [('둥근 사각형','rounded'),('원형','circle'),('간결하게','minimal')]:self.frame.addItem(name,key)
        self.frame.setCurrentIndex(max(0,self.frame.findData(self.draft.theme['Frame'])));self.frame.currentIndexChanged.connect(lambda:self.setting('Frame',self.frame.currentData()));row.addWidget(self.frame);form.addRow('구성',row)
        self.radius=QSlider(Qt.Horizontal);self.radius.setRange(280,315);self.radius.setValue(round(self.draft.theme['Radius']));self.radius.valueChanged.connect(lambda v:self.setting('Radius',v));form.addRow('부채꼴·HUD 반경',self.radius)
        self.gap=QDoubleSpinBox();self.gap.setRange(.3,2.5);self.gap.setSingleStep(.1);self.gap.setValue(self.draft.theme['Gap']);self.gap.valueChanged.connect(lambda v:self.setting('Gap',v));form.addRow('부채꼴 칸 간격',self.gap)
        toggles=QHBoxLayout();self.toggles={}
        for key,name in [('ShowLabels','아이콘 이름 표시'),('ShowNotes','메모 표시')]:
            c=QCheckBox(name);c.setChecked(self.draft.theme[key]);c.toggled.connect(lambda v,k=key:self.setting(k,v));toggles.addWidget(c);self.toggles[key]=c
        form.addRow('',toggles);self.tabs.addTab(custom,'나만의 구성')
        saved=QWidget();layout=QVBoxLayout(saved);row=QHBoxLayout();self.saved=QComboBox();row.addWidget(self.saved,1)
        row.addWidget(button('불러오기',self.load_custom));row.addWidget(button('삭제',self.remove_custom));layout.addLayout(row)
        row=QHBoxLayout();self.custom_name=QLineEdit();self.custom_name.setPlaceholderText('커스텀 테마 이름');row.addWidget(self.custom_name,1);row.addWidget(button('현재 모습 저장',self.save_custom));layout.addLayout(row)
        layout.addWidget(label('커스텀 테마는 기본 색상과 별도로 보관됩니다. 아래 저장 버튼을 눌러야 실제 설정에 반영돼요.',muted=True));self.tabs.addTab(saved,'저장한 커스텀 테마');self.refresh_custom();self.footer()
    def update_preview(self):self.preview.update();style(self,self.draft)
    def setting(self,k,v):self.draft.theme[k]=v;self.update_preview()
    def palette(self,color):
        for key,value in zip(('Name','Background','Surface','Accent','Text'),color):self.draft.theme[key]=value
        for k,b in self.color_buttons.items():b.setText(self.draft.theme[k])
        self.update_preview()
    def design(self,key):self.setting('Design',key)
    def pick_color(self,key):
        dialog=QColorDialog(QColor(self.draft.theme[key]),self);dialog.setOption(QColorDialog.DontUseNativeDialog);style(dialog,self.draft)
        if dialog.exec():self.draft.theme[key]=dialog.selectedColor().name();self.color_buttons[key].setText(self.draft.theme[key]);self.update_preview()
    def refresh_custom(self):
        self.saved.clear()
        for i,t in enumerate(self.draft.data['CustomThemes']):self.saved.addItem(t['Name'],i)
    def save_custom(self):
        import uuid
        name=self.custom_name.text().strip()
        if not name:error(self,'커스텀 테마 이름을 입력해 주세요.');return
        match=next((x for x in self.draft.data['CustomThemes'] if x['Name']==name),None)
        if match:match['Settings']=copy.deepcopy(self.draft.theme)
        else:self.draft.data['CustomThemes'].append(dict(Id=uuid.uuid4().hex,Name=name,Settings=copy.deepcopy(self.draft.theme)))
        self.refresh_custom()
    def load_custom(self):
        i=self.saved.currentData()
        if i is None:return
        self.draft.data['Theme']=copy.deepcopy(self.draft.data['CustomThemes'][i]['Settings'])
        t=self.draft.theme;self.slot_count.setValue(t['Slots']);self.icon_size.setValue(round(t['IconSize']));self.radius.setValue(round(t['Radius']));self.gap.setValue(t['Gap']);self.frame.setCurrentIndex(self.frame.findData(t['Frame']))
        for k,b in self.color_buttons.items():b.setText(t[k])
        for k,c in self.toggles.items():c.setChecked(t[k])
        self.update_preview()
    def remove_custom(self):
        i=self.saved.currentData()
        if i is not None:self.draft.data['CustomThemes'].pop(i);self.refresh_custom()

class FontDialog(Dialog):
    def __init__(self,parent,store,lib):
        super().__init__(parent,lib,'글꼴 고르기',950,680);self.draft=Library(lib.data);self.result=copy.deepcopy(lib.data['Fonts'])
        self.body.addWidget(label('이 PC의 글꼴로 꾸며 보세요',True));self.body.addWidget(label('기본 런처 글꼴은 주아, 설정창은 고운돋움입니다. 동봉 글꼴과 설치된 글꼴을 선택할 수 있어요.',muted=True))
        form=QFormLayout();self.choices={}
        for key,name,fallback in [('Launcher','런처·아이콘 이름','Jua'),('Interface','설정창·입력칸','Gowun Dodum')]:
            choice=QFontComboBox();choice.setCurrentFont(QFont(resolved(self.draft,key=='Launcher')));choice.currentFontChanged.connect(lambda f,k=key:self.changed(k,f.family()));self.choices[key]=choice;form.addRow(name,choice)
        self.body.addLayout(form);self.preview=Canvas(store,self.draft,interactive=False);self.body.addWidget(self.preview,1)
        self.body.addWidget(button('기본 글꼴로',self.reset));self.footer()
    def changed(self,key,name):self.result[key]=name;self.draft.data['Fonts']=copy.deepcopy(self.result);style(self,self.draft);self.preview.update()
    def reset(self):
        self.choices['Launcher'].setCurrentFont(QFont('Jua'));self.choices['Interface'].setCurrentFont(QFont('Gowun Dodum'));self.result=dict(Launcher='',Interface='');self.draft.data['Fonts']=self.result;self.preview.update()

class LibraryTree(QTreeWidget):
    moved=Signal(list,object,object,bool);droppedFiles=Signal(list,object)
    def __init__(self,parent=None):
        super().__init__(parent);self.setColumnCount(2);self.setHeaderLabels(['이름','메모 / 실행 대상']);self.setColumnWidth(0,330)
        self.setSelectionMode(QAbstractItemView.ExtendedSelection);self.setDragEnabled(True);self.setAcceptDrops(True);self.setDropIndicatorShown(True)
        self.setDragDropMode(QAbstractItemView.DragDrop);self.setDefaultDropAction(Qt.MoveAction)
    def mimeTypes(self):return [MIME,'text/uri-list']
    def mimeData(self,items):
        mime=QMimeData();mime.setData(MIME,'\n'.join(i.data(0,Qt.UserRole) for i in items if i.data(0,Qt.UserRole)).encode());return mime
    def startDrag(self,actions):
        drag=QDrag(self);drag.setMimeData(self.mimeData(self.selectedItems()));drag.exec(Qt.MoveAction)
    def dragEnterEvent(self,event):
        if event.mimeData().hasFormat(MIME) or event.mimeData().hasUrls():event.acceptProposedAction()
    def dragMoveEvent(self,event):super().dragMoveEvent(event);event.acceptProposedAction()
    def dropEvent(self,event):
        target=self.itemAt(event.position().toPoint());parent=None;anchor=None;after=False
        if target:
            item_id=target.data(0,Qt.UserRole);pos=self.dropIndicatorPosition();folder=target.data(0,Qt.UserRole+1)
            if pos==QAbstractItemView.OnItem and folder:parent=item_id
            else:
                parent=target.parent().data(0,Qt.UserRole) if target.parent() else None;anchor=item_id;after=pos==QAbstractItemView.BelowItem
        if event.mimeData().hasFormat(MIME):self.moved.emit(bytes(event.mimeData().data(MIME)).decode().split('\n'),parent,anchor,after)
        else:self.droppedFiles.emit([u.toLocalFile() for u in event.mimeData().urls() if u.isLocalFile()],parent)
        event.acceptProposedAction()

class RootDrop(QPushButton):
    dropped=Signal(list)
    def __init__(self):super().__init__('라이브러리로 꺼내기 · 여기에 놓기');self.setAcceptDrops(True)
    def dragEnterEvent(self,e):
        if e.mimeData().hasFormat(MIME):e.acceptProposedAction()
    def dropEvent(self,e):self.dropped.emit(bytes(e.mimeData().data(MIME)).decode().split('\n'));e.acceptProposedAction()

class Settings(Dialog):
    def __init__(self,parent,controller):
        super().__init__(parent,controller.library,'런처 설정',1130,790);self.c=controller;self.store=controller.store
        row=QHBoxLayout();row.addWidget(label('나의 작은 런처',True),1);row.addWidget(button('꾸미기',self.appearance));row.addWidget(button('···',self.options));self.body.addLayout(row)
        self.body.addWidget(label('앱과 폴더를 모으고, 끌어서 순서를 바꿔 보세요. 폴더 가운데에 놓으면 안으로 들어갑니다.',muted=True))
        row=QHBoxLayout();row.addWidget(button('설치된 앱',self.add_apps,True));row.addWidget(button('직접 등록',self.add_entry));row.addWidget(button('새 폴더',self.add_folder));row.addStretch();row.addWidget(button('? 등록 도움말',lambda:Help(self,self.c.library).exec()));self.body.addLayout(row)
        split=QSplitter();self.tree=LibraryTree();split.addWidget(self.tree)
        panel=QWidget();box=QVBoxLayout(panel);box.setContentsMargins(18,10,2,10);self.detail=label('항목을 선택해 주세요',True);box.addWidget(self.detail)
        self.description=label('파일 관리자에서 실행 파일이나 바로가기를 목록에 끌어다 놓아도 등록됩니다.',muted=True);self.description.setTextInteractionFlags(Qt.TextSelectableByMouse);box.addWidget(self.description)
        for name,action in [('실행',self.open_selected),('편집',self.edit),('칸 배경 꾸미기',self.background_selected),('위로 이동',lambda:self.reorder(-1)),('아래로 이동',lambda:self.reorder(1)),('상위 폴더로 꺼내기',self.move_up),('삭제',self.remove)]:box.addWidget(button(name,action))
        box.addStretch();split.addWidget(panel);split.setSizes([760,320]);self.body.addWidget(split,1)
        row=QHBoxLayout();root=RootDrop();root.dropped.connect(lambda ids:self.move(ids,None));row.addWidget(root,1);row.addWidget(button('닫기',self.accept,True));self.body.addLayout(row)
        self.tree.moved.connect(self.move);self.tree.droppedFiles.connect(self.import_files);self.tree.itemSelectionChanged.connect(self.selection)
        self.tree.itemDoubleClicked.connect(lambda i,col:self.edit() if not i.data(0,Qt.UserRole+1) else None)
        self.tree.setContextMenuPolicy(Qt.CustomContextMenu);self.tree.customContextMenuRequested.connect(self.context);self.refresh()
    def selected(self):return [i.data(0,Qt.UserRole) for i in self.tree.selectedItems()]
    def item(self):return self.c.library.find(self.selected()[0]) if self.selected() else None
    def parent_id(self):
        e=self.item();return e['Id'] if e and e['IsFolder'] else e['ParentId'] if e else None
    def refresh(self):
        self.lib=self.c.library;style(self,self.lib);self.tree.clear();items={}
        def add(parent_id,parent_item=None):
            for e in self.lib.children(parent_id):
                i=QTreeWidgetItem([e['Name'],e['Note'] or e['Target']]);i.setData(0,Qt.UserRole,e['Id']);i.setData(0,Qt.UserRole+1,e['IsFolder']);i.setIcon(0,self.c.canvas.images.icon(e));i.setToolTip(1,e['Target'])
                if parent_item:parent_item.addChild(i)
                else:self.tree.addTopLevelItem(i)
                items[e['Id']]=i
                if e['IsFolder']:add(e['Id'],i)
        add(None);self.tree.expandAll();self.selection()
    def save_refresh(self):
        try:self.c.save();self.refresh()
        except Exception as ex:error(self,ex)
    def selection(self):
        e=self.item();self.detail.setText(e['Name'] if e else '항목을 선택해 주세요')
        self.description.setText((self.lib.trail(e['ParentId'])+'\n\n'+(e['Note']+'\n\n' if e['Note'] else '')+('폴더 · '+str(len(self.lib.children(e['Id'])))+'개 항목' if e['IsFolder'] else e['Target'])+('\n\nWindows용 경로 · 리눅스 앱으로 다시 연결해 주세요.' if windows_target(e['Target']) else '')) if e else '아이콘과 메모, 배경 이미지를 바꿀 수 있어요.')
    def add_apps(self):
        d=Apps(self,self.store,self.lib)
        if d.exec():
            parent=self.parent_id()
            for e in d.selected:e['ParentId']=parent;self.lib.entries.append(e)
            self.save_refresh()
    def add_entry(self):self.edit_dialog(Editor(self,self.store,self.lib,parent_id=self.parent_id()))
    def add_folder(self):self.edit_dialog(Editor(self,self.store,self.lib,folder=True,parent_id=self.parent_id()))
    def edit(self):
        if self.item():self.edit_dialog(Editor(self,self.store,self.lib,self.item()))
    def edit_dialog(self,d):
        if d.exec():
            original=self.lib.find(d.result['Id'])
            if original:original.update(d.result)
            else:self.lib.entries.append(d.result)
            self.save_refresh()
    def move(self,ids,parent,anchor=None,after=False):
        if self.lib.move(ids,parent,anchor,after):self.save_refresh()
        else:error(self,'이 위치로 이동할 수 없습니다. 폴더를 자기 자신이나 그 안쪽으로 옮길 수 없어요.')
    def move_up(self):
        e=self.item()
        if e:self.move(self.selected(),self.lib.find(e['ParentId'])['ParentId'] if e['ParentId'] else None)
    def reorder(self,direction):
        e=self.item()
        if not e:return
        siblings=self.lib.children(e['ParentId']);at=siblings.index(e)+direction
        if 0<=at<len(siblings):self.move([e['Id']],e['ParentId'],siblings[at]['Id'],direction>0)
    def import_files(self,paths,parent):
        errors=[]
        for path in paths:
            try:self.lib.entries.append(import_path(path,parent))
            except Exception as ex:errors.append(str(ex))
        self.save_refresh()
        if errors:error(self,'\n'.join(errors))
    def remove(self):
        ids=self.selected()
        if ids and QMessageBox.question(self,'항목 삭제','선택한 항목을 지울까요? 폴더 내부 항목도 함께 지워집니다. 원본 파일은 그대로 남아요.')==QMessageBox.Yes:self.lib.remove(ids);self.save_refresh()
    def open_selected(self):
        e=self.item()
        if e and not e['IsFolder']:self.c.open(e['Id'])
    def background_selected(self):
        e=self.item()
        if e:
            d=BackgroundDialog(self,self.store,self.lib,e['Id'])
            if d.exec():e['Background']=d.result;self.save_refresh()
    def context(self,pos):
        menu=QMenu(self)
        for n,f in [('편집',self.edit),('실행',self.open_selected),('칸 배경 꾸미기',self.background_selected),('상위로 꺼내기',self.move_up),('삭제',self.remove)]:menu.addAction(n,f)
        menu.exec(self.tree.viewport().mapToGlobal(pos))
    def appearance(self):
        d=ActionSheet(self,self.lib,'꾸미기',[('색상 · UI 디자인','10가지 색상, 6가지 모양과 나만의 구성',self.theme_dialog),('전체 배경 이미지','미리보기에서 확대하고 위치 맞추기',self.background_dialog),('글꼴 고르기','런처와 설정창의 글꼴을 각각 선택',self.font_dialog)])
        d.exec()
    def theme_dialog(self):
        d=ThemeDialog(self,self.store,self.lib)
        if d.exec():self.lib.data['Theme']=d.draft.theme;self.lib.data['CustomThemes']=d.draft.data['CustomThemes'];self.save_refresh()
    def background_dialog(self):
        d=BackgroundDialog(self,self.store,self.lib)
        if d.exec():self.lib.data['Background']=d.result;self.save_refresh()
    def font_dialog(self):
        d=FontDialog(self,self.store,self.lib)
        if d.exec():self.lib.data['Fonts']=d.result;self.save_refresh()
    def options(self):
        d=ActionSheet(self,self.lib,'런처 옵션',[('백업 저장','등록 정보와 사용자 이미지를 ZIP으로 저장',self.backup),('백업 가져오기','Windows 백업도 가져올 수 있어요',self.restore),('독 배치 설정','화면 가장자리와 가로 위치 조정',self.placement),('저장 폴더 열기','등록 정보와 이미지가 저장된 위치',self.c.open_data),('런처 종료','백그라운드 실행도 끝내기',self.c.quit)],wheel=True,changed=self.save_refresh)
        d.exec()
    def backup(self):
        path=choose_save(self,'백업 저장','decklauncher-backup.zip','ZIP 백업 (*.zip)')
        if path:
            try:self.store.backup(path,self.lib);QMessageBox.information(self,'백업 완료','등록 정보와 이미지를 저장했습니다.')
            except Exception as ex:error(self,ex)
    def restore(self):
        path=choose_file(self,'백업 가져오기','ZIP 백업 (*.zip)')
        if not path:return
        if QMessageBox.question(self,'백업 가져오기','현재 등록 목록을 백업 내용으로 바꿀까요? 현재 목록은 자동 백업해 둡니다.')!=QMessageBox.Yes:return
        try:
            from datetime import datetime
            self.store.directory.mkdir(parents=True,exist_ok=True)
            self.store.backup(self.store.directory/('가져오기전-'+datetime.now().strftime('%Y%m%d-%H%M%S')+'.zip'),self.lib)
            self.c.library=self.store.restore(path);self.c.canvas.library=self.c.library;self.c.canvas.navigate(None);self.c.save();self.refresh()
            n=sum(windows_target(e['Target']) for e in self.lib.entries if not e['IsFolder'])
            if n:QMessageBox.information(self,'Windows 백업 가져옴',f'{n}개 항목이 Windows용 경로입니다. 편집에서 리눅스 앱으로 다시 연결해 주세요. 폴더·아이콘·메모는 유지했습니다.')
        except Exception as ex:error(self,ex)
    def placement(self):
        d=Dialog(self,self.lib,'독 배치 설정',600,360);d.body.addWidget(label('독 가까이에 펼쳐 놓기',True))
        d.body.addWidget(label('클릭한 위치에 가까운 화면 가장자리로 펼칩니다. 독 아이콘 좌표 자체를 읽는 방식은 아니며, X11·XWayland에서 사용할 수 있어요.',muted=True))
        form=QFormLayout();edge=QComboBox()
        for name,key in [('클릭 위치로 자동','auto'),('아래쪽','bottom'),('위쪽','top'),('왼쪽','left'),('오른쪽','right')]:edge.addItem(name,key)
        align=QComboBox();align.addItem('클릭한 위치 기준','cursor');align.addItem('화면 중앙','center');offset=QSpinBox();offset.setRange(-250,250)
        current=self.lib.data['LinuxPlacement'];edge.setCurrentIndex(max(0,edge.findData(current['Edge'])));align.setCurrentIndex(max(0,align.findData(current['Align'])));offset.setValue(current['Offset']);form.addRow('가장자리',edge);form.addRow('정렬 기준',align);form.addRow('가장자리에서 간격',offset);d.body.addLayout(form);d.footer()
        if d.exec():self.lib.data['LinuxPlacement']=dict(Edge=edge.currentData(),Align=align.currentData(),Offset=offset.value());self.save_refresh()

class ActionSheet(Dialog):
    def __init__(self,parent,lib,title,actions,wheel=False,changed=None):
        super().__init__(parent,lib,title,540,300+len(actions)*70);self.body.addWidget(label(title,True))
        if wheel:
            c=QCheckBox('마우스 휠로 다이얼 돌리기');c.setChecked(lib.data['WheelDialEnabled'])
            def toggle(v):lib.data['WheelDialEnabled']=v;changed()
            c.toggled.connect(toggle);self.body.addWidget(c);self.body.addWidget(label('기본값은 꺼짐 · 이전/다음 버튼으로 페이지를 바꿉니다.',muted=True))
        for name,description,action in actions:
            b=button(name+'\n'+description);b.setMinimumHeight(64)
            def invoke(checked=False,f=action):self.accept();QTimer.singleShot(0,f)
            b.clicked.connect(invoke);self.body.addWidget(b)
        self.body.addWidget(button('닫기',self.reject))
