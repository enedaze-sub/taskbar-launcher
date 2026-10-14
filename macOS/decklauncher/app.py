from __future__ import annotations
import argparse
import hashlib
import os
from pathlib import Path
import sys
import tempfile
import time
from PySide6.QtCore import (Qt,QTimer,QEvent,QObject,QPointF,QLockFile,Signal,QRect)
from PySide6.QtGui import QFontDatabase,QIcon,QCursor,QRegion,QPixmap,QPainter
from PySide6.QtNetwork import QLocalServer,QLocalSocket
from PySide6.QtWidgets import (QApplication,QWidget,QVBoxLayout,QScrollArea,QMenu,
                               QSystemTrayIcon,QMessageBox,QMenuBar)
from PySide6.QtGui import QKeySequence,QShortcut,QAction
from .model import Library,Store,entry,COLORS,DESIGNS
from .view import Canvas,shell
from .dialogs import Settings,Editor,BackgroundDialog,Dialog,label,button,style,error
from .launch import start,import_path

ROOT=Path(__file__).resolve().parent.parent

class Launcher(QWidget):
    def __init__(self,c):
        super().__init__();self.c=c;self.setWindowTitle('DeckLauncher · 나의 작은 런처')
        self.setWindowFlags(Qt.Window|Qt.FramelessWindowHint)
        self.setAttribute(Qt.WA_TranslucentBackground);self.setWindowIcon(QIcon(str(ROOT/'assets/launcher.png')))
        self.canvas=Canvas(c.store,c.library,self);self.canvas.resize(920,454);self.resize(920,454)
        self.canvas.opened.connect(c.open);self.canvas.hovered.connect(c.hover);self.canvas.control.connect(c.control)
        self.canvas.rearrange.connect(lambda ids,target,after:c.move(ids,target[0],target[1],after))
        self.canvas.filesDropped.connect(c.import_files);self.canvas.contextRequested.connect(c.context)
        for key,f in [('Esc',c.back),('Home',lambda:self.canvas.navigate(None)),('PgDown',lambda:self.canvas.change_page(1)),('PgUp',lambda:self.canvas.change_page(-1)),('Ctrl+F',c.manage)]:
            shortcut=QShortcut(QKeySequence(key),self);shortcut.activated.connect(f)
    def resizeEvent(self,event):
        self.canvas.resize(self.size());self.update_mask()
    def update_mask(self):
        from PySide6.QtGui import QTransform,QPainterPath,QPainterPathStroker
        s=self.canvas.scale();path=QTransform().translate((self.width()-920*s)/2,(self.height()-454*s)/2).scale(s,s).map(shell(self.c.library.theme['Design']))
        if self.c.library.theme['Design']=='cat':
            tail=QPainterPath();tail.moveTo(792,412);tail.cubicTo(852,416,888,372,880,320);tail.cubicTo(876,286,848,268,830,288)
            stroke=QPainterPathStroker();stroke.setWidth(24)
            tail=QTransform().translate((self.width()-920*s)/2,(self.height()-454*s)/2).scale(s,s).map(stroke.createStroke(tail));path=path.united(tail)
        self.setMask(QRegion(path.toFillPolygon().toPolygon()))
    def closeEvent(self,event):
        if self.c.exiting:event.accept()
        else:event.ignore();self.c.hide()
    def changeEvent(self,event):
        if event.type()==QEvent.WindowStateChange and self.isMinimized():QTimer.singleShot(0,self.c.hide)
        super().changeEvent(event)

class FolderPopup(QWidget):
    def __init__(self,c,folder,at,depth):
        super().__init__(c.window,Qt.Tool|Qt.FramelessWindowHint);self.c=c;self.depth=depth;self.folder_id=folder['Id']
        self.setAttribute(Qt.WA_ShowWithoutActivating);self.setWindowTitle(folder['Name']);style(self,c.library)
        self.setAttribute(Qt.WA_DeleteOnClose)
        self.setAttribute(Qt.WA_TranslucentBackground);self.setAttribute(Qt.WA_StyledBackground)
        self.setStyleSheet(self.styleSheet()+'FolderPopup {background:'+c.library.theme['Background']+';border:1px solid '+c.library.theme['Accent']+';border-radius:16px;}')
        layout=QVBoxLayout(self);layout.setContentsMargins(16,14,16,14);layout.addWidget(label(folder['Name'],True))
        children=c.library.children(folder['Id']);layout.addWidget(label(str(len(children))+'개 항목 · 바로 선택하세요',muted=True))
        scroll=QScrollArea();scroll.setWidgetResizable(True);rows=QWidget();box=QVBoxLayout(rows);box.setContentsMargins(0,0,0,0)
        for e in children:
            row=button(e['Name']+('  ›' if e['IsFolder'] else ''),lambda checked=False,i=e['Id']:c.open(i))
            row.setIcon(c.canvas.images.icon(e));row.setToolTip(e['Note']);row.setProperty('item_id',e['Id']);row.installEventFilter(self);box.addWidget(row)
        if not children:box.addWidget(label('아직 등록된 항목이 없어요',muted=True))
        box.addStretch();scroll.setWidget(rows)
        rows.setObjectName('popupRows');scroll.viewport().setObjectName('popupViewport')
        rows.setStyleSheet('QWidget#popupRows {background:'+c.library.theme['Background']+';}')
        scroll.viewport().setStyleSheet('QWidget#popupViewport {background:'+c.library.theme['Background']+';}')
        layout.addWidget(scroll);self.resize(300,min(470,120+len(children)*60))
        area=QApplication.screenAt(at.toPoint()) or QApplication.primaryScreen();work=area.availableGeometry()
        x=at.x()+34 if depth==0 else c.popups[depth-1].geometry().right()+8
        if x+self.width()>work.right():x=at.x()-self.width()-34 if depth==0 else c.popups[depth-1].x()-self.width()-8
        y=at.y()-50;self.move(int(max(work.left(),min(x,work.right()-self.width()))),int(max(work.top(),min(y,work.bottom()-self.height()))))
    def paintEvent(self,event):
        from PySide6.QtGui import QColor,QPen
        from PySide6.QtCore import QRectF
        painter=QPainter(self);painter.setRenderHint(QPainter.Antialiasing)
        painter.setBrush(QColor(self.c.library.theme['Background']));painter.setPen(QPen(QColor(self.c.library.theme['Accent']),1))
        painter.drawRoundedRect(QRectF(self.rect()).adjusted(.5,.5,-.5,-.5),16,16);painter.end()
    def eventFilter(self,obj,event):
        if event.type()==QEvent.Enter:
            self.c.close_timer.stop();e=self.c.library.find(obj.property('item_id'))
            if e:
                self.c.canvas.hover_id=e['Id'];self.c.canvas.update()
                if e['IsFolder']:self.c.schedule_popup(e,QPointF(obj.mapToGlobal(obj.rect().topRight())),self.depth+1)
                else:self.c.close_popups(self.depth+1)
        elif event.type()==QEvent.Leave:self.c.schedule_close()
        return False
    def enterEvent(self,event):self.c.close_timer.stop()
    def leaveEvent(self,event):self.c.schedule_close()

class Controller(QObject):
    def __init__(self,app,store,lib):
        super().__init__();self.app=app;self.store=store;self.library=lib;self.exiting=False;self.modal=0;self.popups=[];self.pending_popup=None;self.ignore_click_until=0
        self.window=Launcher(self);self.canvas=self.window.canvas
        self.hover_timer=QTimer(self);self.hover_timer.setSingleShot(True);self.hover_timer.setInterval(180);self.hover_timer.timeout.connect(self.show_pending_popup)
        self.close_timer=QTimer(self);self.close_timer.setSingleShot(True);self.close_timer.setInterval(380);self.close_timer.timeout.connect(self.maybe_close_popups)
        self.app.applicationStateChanged.connect(self.application_state)
        self.menu_bar=None
        if sys.platform=='darwin':self.mac_menu()
        from .outside import OutsideWatcher
        self.watcher=OutsideWatcher(self.outside_click,self)
        self.app.aboutToQuit.connect(self.watcher.close)
        self.tray=None
        if QSystemTrayIcon.isSystemTrayAvailable():
            self.tray=QSystemTrayIcon(QIcon(str(ROOT/'assets/launcher.png')),self);self.tray.setToolTip('DeckLauncher')
            menu=QMenu();menu.addAction('런처 열기',self.reveal);menu.addAction('런처 설정',self.manage);menu.addAction('종료',self.quit)
            self.tray_menu=menu;self.tray.setContextMenu(menu);self.tray.activated.connect(lambda reason:self.toggle() if reason==QSystemTrayIcon.Trigger else None);self.tray.show()
    def application_state(self,state):
        if self.exiting:return
        if state==Qt.ApplicationActive:
            # Qt Cocoa force-delivers this event on every Dock reopen, even when
            # the process is already active and all windows are hidden.
            if sys.platform=='darwin' and not self.modal and not self.window.isVisible():QTimer.singleShot(0,self.reveal)
        else:QTimer.singleShot(60,self.focus_loss)
    def mac_menu(self):
        self.menu_bar=QMenuBar();self.menu_bar.setNativeMenuBar(True)
        menu=self.menu_bar.addMenu('DeckLauncher')
        menu.addAction('런처 열기',self.reveal)
        settings=QAction('런처 설정…',self.menu_bar);settings.setMenuRole(QAction.PreferencesRole);settings.setShortcut(QKeySequence(QKeySequence.Preferences));settings.triggered.connect(self.manage);menu.addAction(settings)
        quit_action=QAction('DeckLauncher 종료',self.menu_bar);quit_action.setMenuRole(QAction.QuitRole);quit_action.setShortcut(QKeySequence(QKeySequence.Quit));quit_action.triggered.connect(self.quit);menu.addAction(quit_action)
    def exec_dialog(self,dialog):
        self.close_popups();self.modal+=1
        try:return dialog.exec()
        finally:self.modal-=1;QTimer.singleShot(80,self.focus_loss)
    def focus_loss(self):
        if self.modal or self.canvas.dragging:return
        if self.app.applicationState()!=Qt.ApplicationActive:self.hide()
    def outside_click(self,point):
        if not self.window.isVisible() or self.modal or self.canvas.dragging or time.monotonic()<self.ignore_click_until:return
        if self.app.activePopupWidget():return
        for w in self.app.topLevelWidgets():
            if not w.isVisible():continue
            if w==self.window:
                local=w.mapFromGlobal(point)
                if w.rect().contains(local) and (w.mask().isEmpty() or w.mask().contains(local)):return
            elif w.frameGeometry().contains(point):return
        self.hide()
    def save(self):
        self.store.save(self.library);self.canvas.library=self.library;self.canvas.refresh();self.window.update_mask()
    def restore_transparency_settings(self):
        if self.modal:
            for w in self.app.topLevelWidgets():
                if w!=self.window and w.isVisible():w.raise_();w.activateWindow();return
            return
        self.manage()
    def reveal(self):
        if self.library.data["Appearance"]["Opacity"]==0:
            self.restore_transparency_settings();return
        self.close_popups();screen=QApplication.screenAt(QCursor.pos()) or QApplication.primaryScreen();area=screen.availableGeometry()
        w=min(920,area.width()-16);h=round(w*454/920);h=min(h,area.height()-16);w=round(h*920/454)
        self.window.resize(w,h);place=self.library.data['MacPlacement']
        at=QCursor.pos();edge=place['Edge']
        if edge=='auto':
            full=screen.geometry();distances={'left':at.x()-full.left(),'right':full.right()-at.x(),'top':at.y()-full.top(),'bottom':full.bottom()-at.y()}
            edge=min(distances,key=distances.get)
            if distances[edge]>120:edge='bottom'
        x=at.x()-w/2 if place['Align']=='cursor' else area.center().x()-w/2;offset=place['Offset']
        if edge in ('left','right'):
            x=area.left()+offset if edge=='left' else area.right()+1-w-offset
            y=at.y()-h/2 if place['Align']=='cursor' else area.center().y()-h/2
        else:y=area.bottom()+1-h-offset if edge=='bottom' else area.top()+offset
        x=max(area.left(),min(x,area.right()+1-w));self.ignore_click_until=time.monotonic()+.35
        self.window.move(round(x),max(area.top(),min(round(y),area.bottom()+1-h)))
        self.window.showNormal();self.window.raise_();self.window.activateWindow();self.canvas.setFocus()
    def toggle(self):
        if self.library.data["Appearance"]["Opacity"]==0:
            self.restore_transparency_settings();return
        if self.modal:
            for w in self.app.topLevelWidgets():
                if isinstance(w,Settings) and w.isVisible():w.raise_();w.activateWindow();return
        if self.window.isVisible():self.hide()
        else:self.reveal()
    def hide(self):self.close_popups();self.window.hide();self.canvas.images.cache.clear()
    def back(self):
        if self.canvas.current:self.canvas.navigate(self.library.find(self.canvas.current)['ParentId'])
        else:self.hide()
    def control(self,name):
        if name=='settings':self.manage()
        elif name=='hide':self.hide()
        elif name=='back':self.back() if self.canvas.current else self.canvas.navigate(None)
        else:self.close_popups();self.canvas.change_page(-1 if name=='previous' else 1)
    def manage(self):self.exec_dialog(Settings(self.window,self))
    def open(self,item_id):
        item=self.library.find(item_id)
        if not item:return
        if item['IsFolder']:self.close_popups();self.canvas.navigate(item_id);return
        try:
            process=start(item)
            # Catch immediate exec/desktop failures before hiding the launcher.
            QTimer.singleShot(250,lambda:self.report_launch(process))
            self.hide()
        except Exception as ex:
            self.modal+=1
            try:error(self.window,ex)
            finally:self.modal-=1
    def report_launch(self,process):
        code=process.poll()
        if code not in (None,0):
            self.modal+=1
            try:error(self.window,'앱 실행 명령이 실패했습니다. 실행 파일·인수·권한을 편집에서 확인해 주세요. 종료 코드: '+str(code))
            finally:self.modal-=1
    def move(self,ids,parent,anchor=None,after=False):
        if self.library.move(ids,parent,anchor,after):
            try:self.save();self.close_popups()
            except Exception as ex:error(self.window,ex)
    def import_files(self,paths,parent):
        errors=[]
        for path in paths:
            try:self.library.entries.append(import_path(path,parent))
            except Exception as ex:errors.append(str(ex))
        try:self.save()
        except Exception as ex:errors.append(str(ex))
        if errors:self.modal+=1;error(self.window,'\n'.join(errors));self.modal-=1
    def context(self,item_id,at):
        menu=QMenu(self.window);style(menu,self.library)
        menu.addAction('실행 / 폴더 열기',lambda:self.open(item_id));menu.addAction('편집',lambda:self.edit(item_id));menu.addAction('칸 배경 꾸미기',lambda:self.background(item_id))
        self.modal+=1
        try:menu.exec(at)
        finally:self.modal-=1
    def edit(self,item_id):
        d=Editor(self.window,self.store,self.library,self.library.find(item_id))
        if self.exec_dialog(d):self.library.find(item_id).update(d.result);self.save()
    def background(self,item_id):
        d=BackgroundDialog(self.window,self.store,self.library,item_id)
        if self.exec_dialog(d):d.apply_to(self.library.find(item_id));self.save()
    def hover(self,item_id,at):
        e=self.library.find(item_id)
        if e and e['IsFolder']:self.schedule_popup(e,at,0)
        else:self.schedule_close()
    def schedule_popup(self,folder,at,depth):
        self.close_timer.stop();self.pending_popup=(folder,at,depth);self.hover_timer.start()
    def show_pending_popup(self):
        if not self.pending_popup or not self.window.isVisible():return
        folder,at,depth=self.pending_popup
        if len(self.popups)>depth and self.popups[depth].folder_id==folder['Id']:self.close_popups(depth+1);return
        self.close_popups(depth);popup=FolderPopup(self,folder,at,depth);self.popups.append(popup);popup.show()
    def close_popups(self,depth=0):
        self.hover_timer.stop();self.close_timer.stop()
        while len(self.popups)>depth:self.popups.pop().close()
    def schedule_close(self):self.hover_timer.stop();self.close_timer.start()
    def maybe_close_popups(self):
        if not any(w.underMouse() for w in self.popups) and not self.canvas.underMouse():self.close_popups()
        elif self.canvas.underMouse() and not (self.library.find(self.canvas.hover_id) or {}).get('IsFolder'):self.close_popups()
    def open_data(self):
        self.store.directory.mkdir(parents=True,exist_ok=True)
        try:start(entry(Target=str(self.store.directory)))
        except Exception as ex:error(self.window,ex)
    def quit(self):
        self.exiting=True;self.close_popups();self.watcher.close()
        if self.tray:self.tray.hide()
        self.app.quit()

def load_fonts():
    for path in (ROOT/'Fonts').glob('*.ttf'):QFontDatabase.addApplicationFont(str(path))
    from .fonts import families
    families.cache_clear()

def demo():
    lib=Library()
    folder=entry('게임',True,Note='좋아하는 게임을 모아 둔 폴더')
    lib.entries.extend([folder,entry('작업',True,Note='작업에 필요한 앱'),entry('유틸리티',True,Note='가볍게 쓰는 도구'),entry('텍스트 편집기',Target='/System/Applications/TextEdit.app',Note='아이디어를 빠르게 기록'),entry('Finder',Target='/System/Library/CoreServices/Finder.app',Note='파일과 폴더 열기'),entry('계산기',Target='/System/Applications/Calculator.app',Note='간단한 계산'),entry('브라우저',Target='https://example.com',Note='자주 가는 페이지'),entry('터미널',Target='/System/Applications/Utilities/Terminal.app'),entry('음악',Target='/System/Applications/Music.app')])
    lib.entries.extend([entry('즐겨찾는 게임',parent=folder['Id']),entry('캐주얼 게임',parent=folder['Id'])]);return lib

def previews(app,store,out):
    from .dialogs import ThemeDialog,BackgroundDialog,FontDialog,Help,ActionSheet
    out=Path(out);out.mkdir(parents=True,exist_ok=True);lib=demo()
    colors={'classic':0,'cat':6,'neon':7,'paper':8,'modern':5,'space':9}
    for key,name in DESIGNS:
        lib.data['Theme']['Design']=key
        for k,v in zip(('Name','Background','Surface','Accent','Text'),COLORS[colors[key]]):lib.theme[k]=v
        lib.theme['Slots']=6 if key=='cat' else 8 if key=='paper' else 7
        w=Canvas(store,lib,interactive=False);w.resize(920,454);w.show();app.processEvents();w.grab().save(str(out/(key+'.png')));w.close()
    lib=demo();lib.theme['Design']='cat'
    for k,v in zip(('Name','Background','Surface','Accent','Text'),COLORS[6]):lib.theme[k]=v
    c=Controller(app,store,lib)
    dialogs=[('settings',Settings(c.window,c)),('themes',ThemeDialog(c.window,store,lib)),('fonts',FontDialog(c.window,store,lib)),('help',Help(c.window,lib))]
    for name,w in dialogs:w.show();app.processEvents();w.grab().save(str(out/(name+'.png')));w.close()
    c.window.show();c.schedule_popup(lib.entries[0],QPointF(400,250),0);c.show_pending_popup();app.processEvents()
    if c.popups:c.popups[0].grab().save(str(out/'folder.png'))
    c.exiting=True;c.close_popups();c.watcher.close();c.window.close()

def main(argv=None):
    parser=argparse.ArgumentParser(description='DeckLauncher macOS')
    parser.add_argument('--data-dir');parser.add_argument('--settings',action='store_true');parser.add_argument('--preview');parser.add_argument('--self-test',action='store_true')
    args=parser.parse_args(argv)
    app=QApplication(['DeckLauncher']);app.setQuitOnLastWindowClosed(False);app.setApplicationName('DeckLauncher');app.setApplicationDisplayName('나의 작은 런처');app.setOrganizationName('enedaze-sub')
    load_fonts()
    store=Store(args.data_dir)
    if args.self_test:
        import unittest
        tests=unittest.defaultTestLoader.discover(str(ROOT/'tests'))
        return 0 if unittest.TextTestRunner(verbosity=2).run(tests).wasSuccessful() else 1
    if args.preview:previews(app,store,args.preview);return 0
    key=hashlib.sha256(str(store.directory.resolve()).encode()).hexdigest()[:16]
    # macOS's per-user TMPDIR can be long; a short private socket directory keeps
    # the Unix socket path below sockaddr_un's size limit.
    uid=str(os.getuid() if hasattr(os,'getuid') else os.environ.get('USERNAME','user'))
    runtime=Path('/tmp' if sys.platform=='darwin' else tempfile.gettempdir())/('decklauncher-mac-'+uid)
    if runtime.is_symlink() or (runtime.exists() and os.name=='posix' and runtime.stat().st_uid!=os.getuid()):
        QMessageBox.critical(None,'DeckLauncher','임시 통신 폴더의 소유자를 확인할 수 없습니다: '+str(runtime));return 1
    runtime.mkdir(mode=0o700,parents=True,exist_ok=True)
    if os.name=='posix':runtime.chmod(0o700)
    lock=QLockFile(str(runtime/(key+'.lock')));lock.setStaleLockTime(0)
    name=str(runtime/(key+'.socket')) if os.name=='posix' else 'decklauncher-'+key
    if not lock.tryLock(0):
        for attempt in range(20):
            client=QLocalSocket();client.connectToServer(name)
            if client.waitForConnected(250):
                client.write(b'settings' if args.settings else b'toggle');client.waitForBytesWritten(500);client.disconnectFromServer();return 0
            from PySide6.QtCore import QThread
            QThread.msleep(100)
        QMessageBox.warning(None,'DeckLauncher','이미 실행 중인 런처와 연결하지 못했습니다. 다시 실행해 주세요.');return 1
    try:lib=store.load()
    except Exception as ex:QMessageBox.critical(None,'등록 정보를 읽지 못했습니다',str(ex)+'\n원본 library.json은 그대로 보존했습니다.');lock.unlock();return 1
    c=Controller(app,store,lib);server=QLocalServer();QLocalServer.removeServer(name)
    previous_hook=sys.excepthook
    def report_exception(kind,value,trace):
        previous_hook(kind,value,trace)
        if c.exiting:return
        c.modal+=1
        try:QMessageBox.warning(c.window,'문제가 발생했습니다',str(value)+'\n저장한 등록 정보와 백업은 유지됩니다.')
        finally:c.modal-=1
    sys.excepthook=report_exception
    if not server.listen(name):QMessageBox.critical(None,'DeckLauncher',server.errorString());lock.unlock();return 1
    connections=[]
    def connected():
        while server.hasPendingConnections():
            socket=server.nextPendingConnection();connections.append(socket)
            def read(s=socket):
                message=bytes(s.readAll())
                if message==b'settings':c.manage()
                else:c.toggle()
                s.disconnectFromServer()
            socket.readyRead.connect(read)
            if socket.bytesAvailable():read()
            def cleanup(s=socket):
                if s in connections:connections.remove(s)
                s.deleteLater()
            socket.disconnected.connect(cleanup)
    server.newConnection.connect(connected)
    c.reveal()
    if args.settings:QTimer.singleShot(0,c.manage)
    result=app.exec();server.close();QLocalServer.removeServer(name);lock.unlock();return result
