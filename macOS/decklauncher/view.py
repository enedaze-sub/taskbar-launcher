from __future__ import annotations
import math
import random
from collections import OrderedDict
from pathlib import Path
from PySide6.QtCore import Qt, QPointF, QRectF, Signal, QMimeData, QVariantAnimation
from PySide6.QtGui import (QColor,QPainter,QPainterPath,QPen,QBrush,QLinearGradient,QRadialGradient,
                           QPixmap,QIcon,QFont,QFontMetrics,QDrag,QPolygonF,QTransform,QImageReader)
from PySide6.QtWidgets import QWidget,QFileIconProvider,QApplication,QToolTip
from PySide6.QtCore import QFileInfo
from .fonts import resolved

MIME='application/x-decklauncher-items'
BASE=QRectF(0,0,920,454)

def mix(first,second,amount):
    a,b=QColor(first),QColor(second)
    return QColor(*(round(x+(y-x)*amount) for x,y in zip(a.getRgb(),b.getRgb())))
def alpha(color,amount):
    c=QColor(color); c.setAlphaF(amount); return c
def rounded(x,y,w,h,r):
    p=QPainterPath(); p.addRoundedRect(QRectF(x,y,w,h),r,r); return p
def poly(points):
    p=QPainterPath(); p.addPolygon(QPolygonF([QPointF(*x) for x in points])); p.closeSubpath(); return p
def sector(inner,outer,start,end):
    p=QPainterPath()
    for i,a in enumerate([start+(end-start)*k/48 for k in range(49)]):
        at=polar(outer,a)
        if i==0:p.moveTo(at)
        else:p.lineTo(at)
    for a in [end-(end-start)*k/48 for k in range(49)]: p.lineTo(polar(inner,a))
    p.closeSubpath();return p
def polar(r,a):
    return QPointF(460+r*math.cos(math.radians(a)),453+r*math.sin(math.radians(a)))

def shell(design):
    if design=='modern': return rounded(250,204,420,84,42).united(rounded(56,300,808,154,36))
    if design=='paper': return rounded(126,100,668,354,16)
    if design=='neon': return poly([(70,125),(125,70),(795,70),(850,125),(850,454),(70,454)])
    if design=='space':
        p=QPainterPath();p.moveTo(40,454);p.lineTo(40,330);p.arcTo(QRectF(40,80,840,500),180,-180);p.lineTo(880,454);p.closeSubpath();return p
    if design=='cat':
        head=rounded(112,134,696,320,92)
        ear=QPainterPath();ear.moveTo(140,214);ear.cubicTo(132,150,150,86,184,56);ear.quadTo(198,44,214,54);ear.cubicTo(254,82,296,118,322,152);ear.closeSubpath()
        mirror=QTransform(-1,0,0,1,920,0).map(ear)
        return head.united(ear).united(mirror).united(rounded(112,365,696,89,36))
    return sector(.01,390,-180,0)

def slots(design,count,t):
    result=[]
    for i in range(count):
        if design in ('neon','space'):
            angle=-90 if count==1 else (-165+150*i/(count-1) if design=='space' else -153+126*i/(count-1))
            pt=QPointF(460+350*math.cos(math.radians(angle)),456+260*math.sin(math.radians(angle))) if design=='space' else polar(t['Radius'],angle)
            if design=='neon': shape=poly([(pt.x()+42*math.cos(math.radians(-90+k*60)),pt.y()-3+42*math.sin(math.radians(-90+k*60))) for k in range(6)])
            else: shape=QPainterPath();shape.addEllipse(QPointF(pt.x(),pt.y()-3),38,38)
            shape=shape.united(rounded(pt.x()-49,pt.y()+40,98,22,4 if design=='neon' else 11))
        elif design=='modern':
            w=min(96,(760-14*(count-1))/max(1,count));left=460-(w*count+14*(count-1))/2
            shape=rounded(left+i*(w+14),310,w,84,20);pt=shape.boundingRect().center()
        elif design=='cat':
            cols=max(1,count) if count<=3 else 3 if count<=6 else 4 if count<=8 else 5
            rows=math.ceil(count/cols); w=(600-10*(cols-1))/cols; top=314 if rows==1 else 284
            shape=rounded(160+(i%cols)*(w+10),top+(i//cols)*60,w,52,26);pt=shape.boundingRect().center()
        elif design=='paper':
            cols=min(4,max(1,count));rows=math.ceil(count/cols);w=min(140,(600-14*(cols-1))/cols);h=62 if rows>2 else 92;top=196 if rows>2 else 200
            left=460-(cols*w+14*(cols-1))/2
            shape=rounded(left+(i%cols)*(w+14),top+(i//cols)*(h+8),w,h,4);pt=shape.boundingRect().center()
        else:
            step=178/max(5,count);left=-179+(178-count*step)/2+i*step
            shape=sector(216,380,left+t['Gap']/2,left+step-t['Gap']/2);pt=polar(t['Radius'],left+step/2)
        result.append((shape,pt))
    return result

class Images:
    def __init__(self,store):self.store=store;self.cache=OrderedDict();self.provider=QFileIconProvider()
    def remember(self,key,make):
        if key not in self.cache:self.cache[key]=make()
        self.cache.move_to_end(key)
        while len(self.cache)>64:self.cache.popitem(last=False)
        return self.cache[key]
    def image(self,name):
        if not name:return QPixmap()
        def make():
            reader=QImageReader(str(self.store.icons/name));reader.setAutoTransform(True)
            size=reader.size()
            if size.isValid() and max(size.width(),size.height())>1600:
                reader.setScaledSize(size.scaled(1600,1600,Qt.KeepAspectRatio))
            return QPixmap.fromImage(reader.read())
        value=self.remember(('img',name),make)
        image_keys=[k for k in self.cache if k[0]=='img']
        for key in image_keys[:-12]:self.cache.pop(key,None)
        return value
    def icon(self,e):
        if e.get('IconFile'):
            pix=self.image(e['IconFile'])
            if not pix.isNull():return QIcon(pix)
        key=('icon',e['Target'],e.get('NativeIcon'),e['IsFolder'])
        def make():
            native=e.get('NativeIcon','')
            if native:
                icon=QIcon(native) if Path(native).is_absolute() else QIcon.fromTheme(native)
                if not icon.isNull():return icon
            name='folder' if e['IsFolder'] else 'application-x-executable'
            themed=QIcon.fromTheme(name)
            if e['IsFolder']:return themed if not themed.isNull() else self.provider.icon(QFileIconProvider.Folder)
            if Path(e['Target']).exists():return self.provider.icon(QFileInfo(e['Target']))
            return themed if not themed.isNull() else self.provider.icon(QFileIconProvider.File)
        return self.remember(key,make)

def draw_background(painter,images,bg,path):
    pix=images.image(bg.get('ImageFile'))
    if pix.isNull():return
    bounds=path.boundingRect(); zoom=bg.get('Zoom',1)
    ratio=max(bounds.width()/pix.width(),bounds.height()/pix.height())*zoom
    w,h=pix.width()*ratio,pix.height()*ratio
    rect=QRectF(bounds.x()+(bounds.width()-w)/2+bg.get('OffsetX',0)*(w-bounds.width())/2,
                bounds.y()+(bounds.height()-h)/2+bg.get('OffsetY',0)*(h-bounds.height())/2,w,h)
    painter.save();painter.setClipPath(path);painter.drawPixmap(rect,pix,QRectF(pix.rect()))
    painter.fillPath(path,alpha('#071421',bg.get('Darkness',.42)));painter.restore()

class Canvas(QWidget):
    opened=Signal(str); hovered=Signal(object,QPointF); control=Signal(str)
    rearrange=Signal(list,object,bool); filesDropped=Signal(list,object); contextRequested=Signal(str,object)
    def __init__(self,store,library,parent=None,interactive=True):
        super().__init__(parent);self.store=store;self.library=library;self.images=Images(store)
        self.interactive=interactive;self.current=None;self.page=0;self.dial=None;self.hover_id=None;self.press=None
        self.transition=0;self.anim=None;self.pending=None;self.dragging=False
        self.setMouseTracking(True);self.setAcceptDrops(interactive);self.setMinimumSize(460,227)
        self.setFocusPolicy(Qt.StrongFocus);self.setAttribute(Qt.WA_TranslucentBackground)
    def scale(self):return min(self.width()/920,self.height()/454)
    def point(self,pos):
        s=self.scale();return QPointF((pos.x()-(self.width()-920*s)/2)/s,(pos.y()-(self.height()-454*s)/2)/s)
    def visible(self):
        all_items=self.library.children(self.current);n=int(self.library.theme['Slots'])
        if not all_items:return []
        if self.dial is not None:return [all_items[(self.dial+i)%len(all_items)] for i in range(min(n,len(all_items)))]
        self.page=min(self.page,max(0,math.ceil(len(all_items)/n)-1));return all_items[self.page*n:(self.page+1)*n]
    def geometry(self):return slots(self.library.theme['Design'],len(self.visible()),self.library.theme)
    def refresh(self):
        if self.current and not self.library.find(self.current):self.current=None
        if not self.library.data['WheelDialEnabled']:
            if self.anim:self.anim.stop()
            self.dial=None;self.transition=0
        self.hover_id=None;self.images.cache.clear();self.update()
    def navigate(self,parent):
        if self.anim:self.anim.stop()
        self.transition=0;self.current=parent;self.page=0;self.dial=None;self.hover_id=None;self.update()
    def turn(self,delta):
        if self.anim and self.anim.state()==QVariantAnimation.Running:return
        all_items=self.library.children(self.current)
        if not all_items:return
        n=int(self.library.theme['Slots'])
        if not self.library.data['WheelDialEnabled']:return
        self.pending=((self.dial if self.dial is not None else self.page*n)+delta)%len(all_items)
        self.anim=QVariantAnimation(self);self.anim.setDuration(160);self.anim.setStartValue(0.0);self.anim.setEndValue(float(delta))
        self.anim.valueChanged.connect(self._transition);self.anim.finished.connect(self._finish_turn);self.anim.start()
    def _transition(self,value):self.transition=value;self.update()
    def _finish_turn(self):self.dial=self.pending;self.transition=0;self.hover_id=None;self.update()
    def change_page(self,delta):
        if self.anim:self.anim.stop()
        self.transition=0
        pages=max(1,math.ceil(len(self.library.children(self.current))/self.library.theme['Slots']))
        self.dial=None;self.page=(self.page+delta)%pages;self.hover_id=None;self.update()
    def controls(self):
        return [('previous',QPointF(316,429)),('back',QPointF(374,429)),('settings',QPointF(460,429)),('hide',QPointF(546,429)),('next',QPointF(604,429))]
    def hit(self,at):
        if self.transition:return None
        for e,(shape,pt) in zip(self.visible(),self.geometry()):
            if shape.contains(at):return e['Id']
        return None
    def paintEvent(self,event):
        p=QPainter(self);p.setRenderHint(QPainter.Antialiasing);p.setRenderHint(QPainter.SmoothPixmapTransform)
        s=self.scale();p.translate((self.width()-920*s)/2,(self.height()-454*s)/2);p.scale(s,s)
        self.draw(p);p.end()
    def draw(self,p):
        t=self.library.theme;d=t['Design'];bg,sf,ac,tx=[QColor(t[k]) for k in ('Background','Surface','Accent','Text')]
        skin=shell(d)
        if d=='cat':
            tail=QPainterPath();tail.moveTo(792,412);tail.cubicTo(852,416,888,372,880,320);tail.cubicTo(876,286,848,268,830,288)
            p.setPen(QPen(mix(ac,bg,.45),21,Qt.SolidLine,Qt.RoundCap));p.drawPath(tail)
            p.setPen(QPen(mix(bg,sf,.3),15,Qt.SolidLine,Qt.RoundCap));p.drawPath(tail)
        gradient=QLinearGradient(0,70,0,454);gradient.setColorAt(0,mix(bg,sf,.3));gradient.setColorAt(1,bg)
        if d=='modern':gradient.setColorAt(0,alpha(mix(sf,'#FFFFFF',.5),.94));gradient.setColorAt(1,alpha(bg,.96))
        if d=='paper':gradient.setColorAt(0,mix(bg,sf,.75));gradient.setColorAt(1,mix(bg,sf,.75))
        p.setPen(QPen(ac if d=='neon' else mix(bg,ac,.4),1.5));p.setBrush(gradient);p.drawPath(skin)
        draw_background(p,self.images,self.library.data['Background'],skin)
        p.save();p.setClipPath(skin)
        self.decorations(p,d,bg,sf,ac,tx)
        entries=self.visible();geometry=self.geometry()
        if d in ('neon','space') and geometry:
            p.setPen(QPen(alpha(ac,.2),1));line=QPainterPath();line.moveTo(geometry[0][1])
            for g,pt in geometry[1:]:line.lineTo(pt)
            p.drawPath(line)
        p.save()
        if self.transition:
            if d in ('classic','neon','space'):
                p.translate(460,453);p.rotate(-self.transition*178/max(5,len(entries)));p.translate(-460,-453)
            else:p.translate(-self.transition*35,0)
            p.setOpacity(1-abs(self.transition)*.45)
        for i,(e,(shape,pt)) in enumerate(zip(entries,geometry)):self.draw_slot(p,e,i,shape,pt,bg,sf,ac,tx)
        p.restore()
        if d=='classic':hub=sector(.01,205,-180,0)
        elif d=='neon':hub=poly([(160,454),(160,420),(182,398),(738,398),(760,420),(760,454)])
        elif d=='space':hub=QPainterPath();hub.addEllipse(QPointF(460,640),300,250)
        else:hub=rounded(146 if d=='cat' else 140 if d=='paper' else 80,402,628 if d=='cat' else 640 if d=='paper' else 760,52,24)
        p.setBrush(mix(bg,sf,.35));p.setPen(QPen(alpha(ac,.4),1));p.drawPath(hub)
        if d=='cat':
            for x in (210,710):
                p.setBrush(mix(bg,'#FFFFFF',.12));p.drawRoundedRect(QRectF(x-31,394,62,34),17,17)
                for dx in (-10,10):p.drawLine(QPointF(x+dx,402),QPointF(x+dx,413))
        if d=='space':p.setPen(QPen(alpha(ac,.55),3));p.setBrush(Qt.NoBrush);p.drawEllipse(QRectF(210,410,500,36))
        title=self.library.find(self.current)['Name'] if self.current else '나의 바로가기'
        item=self.library.find(self.hover_id)
        note=(item['Note'] if item and item['Note'] else item['Name']) if item else '자주 쓰는 것들을 한 번에. 실행할 아이콘을 선택하세요.'
        if not entries:note='설정에서 앱이나 폴더를 등록해 보세요.'
        x,y,w=(250,212,420) if d in ('modern','cat') else (190,124,540) if d=='paper' else (310,286,300) if d=='space' else (305,278,310)
        self.text(p,QRectF(x,y,w,30),title,23,tx,heading=True)
        if t['ShowNotes']:self.text(p,QRectF(x,y+33,w,50),note,14.5,tx,wrap=True,heading=True)
        if d=='neon':self.text(p,QRectF(290,98,340,30),'DECK / READY TO LAUNCH',11,ac)
        for name,pt in self.controls():
            r=24 if name=='settings' else 19
            p.setPen(QPen(alpha(ac,.45),1));p.setBrush(ac if name=='settings' else mix(bg,sf,.6));p.drawEllipse(pt,r,r)
            ink=bg if name=='settings' else tx
            self.draw_control(p,name,pt,ink)
        all_count=len(self.library.children(self.current));pages=max(1,math.ceil(all_count/t['Slots']))
        self.text(p,QRectF(656,418,85,25),f'{self.dial+1} / {all_count}' if self.dial is not None else f'{self.page+1} / {pages}',11,alpha(tx,.7))
        p.restore()
    def draw_control(self,p,name,pt,ink):
        p.save();p.translate(pt);p.setPen(QPen(ink,1.7,Qt.SolidLine,Qt.RoundCap,Qt.RoundJoin));p.setBrush(Qt.NoBrush)
        if name in ('previous','next'):
            direction=1 if name=='next' else -1
            path=QPainterPath();path.moveTo(-3*direction,-6);path.lineTo(3*direction,0);path.lineTo(-3*direction,6);p.drawPath(path)
        elif name=='hide':p.drawLine(-5,-5,5,5);p.drawLine(-5,5,5,-5)
        elif name=='back':
            if self.current:p.drawLine(-7,0,7,0);p.drawLine(-7,0,-1,-6);p.drawLine(-7,0,-1,6)
            else:
                path=QPainterPath();path.moveTo(-8,0);path.lineTo(0,-7);path.lineTo(8,0);path.moveTo(-6,-1);path.lineTo(-6,7);path.lineTo(6,7);path.lineTo(6,-1);path.moveTo(-2,7);path.lineTo(-2,2);path.lineTo(2,2);path.lineTo(2,7);p.drawPath(path)
        else:
            path=QPainterPath()
            for i in range(32):
                angle=i*math.pi/16;radius=10 if i%4 in (1,2) else 7.5;x,y=radius*math.cos(angle),radius*math.sin(angle)
                if i==0:path.moveTo(x,y)
                else:path.lineTo(x,y)
            path.closeSubpath();p.drawPath(path);p.drawEllipse(QPointF(0,0),3.2,3.2)
        p.restore()
    def text(self,p,rect,text,size,color,wrap=False,heading=False,align=Qt.AlignCenter):
        family=resolved(self.library,heading)
        font=QFont(family);font.setPixelSize(round(size));p.setFont(font);p.setPen(color)
        if wrap:p.drawText(rect,int(align|Qt.TextWordWrap),text)
        else:p.drawText(rect,int(align),QFontMetrics(font).elidedText(text,Qt.ElideRight,int(rect.width())))
    def draw_slot(self,p,e,i,shape,pt,bg,sf,ac,tx):
        t=self.library.theme;d=t['Design'];bounds=shape.boundingRect();active=e['Id']==self.hover_id
        palette=['#FFF0A6','#FFD3DC','#CDEFD9','#D3E6FF','#FFE0C2'] if d=='paper' else ['#FFB86B','#7FD1FF','#C59BFF','#7CF2B5','#FF8FB1','#FFE27A']
        fill=mix(palette[i%len(palette)],sf,.35) if d in ('paper','space') else mix(sf,ac,.2) if active else sf
        if d=='modern':fill=alpha(ac,.2 if active else .015)
        gradient=QLinearGradient(bounds.topLeft(),bounds.bottomLeft());gradient.setColorAt(0,mix(fill,'#FFFFFF',.12));gradient.setColorAt(1,fill)
        p.setPen(QPen(ac if active or d=='neon' else alpha(ac,.25),1.7 if active else 1));p.setBrush(gradient);p.drawPath(shape)
        draw_background(p,self.images,e['Background'],shape)
        if d=='paper':
            p.fillRect(QRectF(bounds.center().x()-23,bounds.top()-5,46,13),alpha(mix(ac,'#FFFFFF',.5),.55))
            p.setPen(Qt.NoPen);p.setBrush(mix(fill,tx,.15));p.drawPath(poly([(bounds.right()-14,bounds.bottom()),(bounds.right(),bounds.bottom()-14),(bounds.right()-14,bounds.bottom()-14)]))
        size=t['IconSize'];center=QPointF(pt)
        if d=='cat':center=QPointF(bounds.left()+33,bounds.center().y());size=min(size,36)
        elif d=='paper':center.setY(bounds.top()+(24 if bounds.height()<70 else 33));size=min(size,32 if bounds.height()<70 else 40)
        elif d=='modern':center.setY(bounds.top()+31)
        if d=='classic' and t['Frame']!='minimal' or d=='cat':
            p.setBrush(mix(bg,sf,.5));p.setPen(QPen(alpha(ac,.35),1));r=QRectF(center.x()-size/2-6,center.y()-size/2-6,size+12,size+12)
            p.drawRoundedRect(r,(size+12)/2 if t['Frame']=='circle' or d=='cat' else 12,(size+12)/2 if t['Frame']=='circle' or d=='cat' else 12)
        self.images.icon(e).paint(p,QRectF(center.x()-size/2,center.y()-size/2,size,size).toRect())
        if t['ShowLabels']:
            rect=QRectF(bounds.left()+62,bounds.top()+10,bounds.width()-78,32) if d=='cat' else QRectF(bounds.left()+8,bounds.bottom()-25,bounds.width()-16,22) if d in ('paper','modern') else QRectF(pt.x()-48,pt.y()+40,96,22) if d in ('neon','space') else QRectF(pt.x()-64,pt.y()+size/2+12,128,25)
            if e['Background'].get('ImageFile'):p.fillRect(rect,alpha(bg,.85))
            self.text(p,rect,e['Name'],12 if d=='cat' else 11.5,tx,heading=True,align=Qt.AlignVCenter|Qt.AlignLeft if d=='cat' else Qt.AlignCenter)
        if e['IsFolder']:
            count=str(len(self.library.children(e['Id'])));mark=QRectF(bounds.right()-28,bounds.top()+8,18,18) if d in ('cat','paper','modern') else QRectF(center.x()+14,center.y()+10,18,18)
            p.setBrush(ac);p.setPen(Qt.NoPen);p.drawEllipse(mark);self.text(p,mark,count,10,bg)
    def decorations(self,p,d,bg,sf,ac,tx):
        p.setPen(QPen(alpha(ac,.22),1));p.setBrush(Qt.NoBrush)
        if d=='cat':
            ear=poly([(168,188),(196,84),(212,84),(284,158)])
            p.setPen(Qt.NoPen);p.setBrush(alpha(ac,.45));p.drawPath(ear);p.drawPath(QTransform(-1,0,0,1,920,0).map(ear))
            for side in (-1,1):
                p.setPen(QPen(alpha(ac,.65),2,Qt.SolidLine,Qt.RoundCap))
                for dy in (-10,0,10):p.drawLine(QPointF(460+side*280,218+dy),QPointF(460+side*320,215+dy*1.8))
            p.setBrush(alpha(ac,.8));p.setPen(Qt.NoPen);p.drawPath(poly([(450,181),(470,181),(460,190)]))
        if d=='paper':
            p.setPen(QPen(alpha(tx,.09),1))
            for y in range(164,390,24):p.drawLine(146,y,774,y)
            for x in range(148,780,20):p.setPen(Qt.NoPen);p.setBrush(alpha(tx,.28));p.drawEllipse(QPointF(x,110),2,2)
        if d=='neon':
            p.setPen(QPen(alpha(ac,.06),1))
            for x in range(90,850,28):p.drawLine(x,80,x,398)
            for y in range(94,400,28):p.drawLine(75,y,845,y)
            p.setPen(QPen(alpha(ac,.6),2));p.drawLine(160,397,760,397)
        if d=='space':
            r=random.Random(8);p.setPen(Qt.NoPen)
            for i in range(90):
                p.setBrush(alpha(tx,.25+r.random()*.5));p.drawEllipse(QPointF(50+r.random()*820,86+r.random()*300),1,1)
            p.setPen(QPen(alpha(ac,.28),1,Qt.DashLine));p.setBrush(Qt.NoBrush);p.drawArc(QRectF(110,196,700,520),0,180*16)
    def mouseMoveEvent(self,event):
        if not self.interactive:return
        if self.press and (event.position()-self.press[1]).manhattanLength()>QApplication.startDragDistance() and self.press[0]:
            drag=QDrag(self);mime=QMimeData();mime.setData(MIME,self.press[0].encode());drag.setMimeData(mime)
            e=self.library.find(self.press[0]);drag.setPixmap(self.images.icon(e).pixmap(42,42));self.press=None;self.dragging=True
            drag.exec(Qt.MoveAction);self.dragging=False;return
        hit=self.hit(self.point(event.position()))
        if hit!=self.hover_id:
            self.hover_id=hit;self.update();self.hovered.emit(hit,QPointF(event.globalPosition()))
            e=self.library.find(hit)
            if e and e['Note'] and self.library.theme['ShowNotes']:QToolTip.showText(event.globalPosition().toPoint(),e['Note'],self)
            else:QToolTip.hideText()
        self.setCursor(Qt.PointingHandCursor if hit or any((self.point(event.position())-pt).manhattanLength()<32 for n,pt in self.controls()) else Qt.ArrowCursor)
    def leaveEvent(self,event):self.hovered.emit(None,QPointF());QToolTip.hideText()
    def mousePressEvent(self,event):
        if not self.interactive:return
        hit=self.hit(self.point(event.position()))
        if event.button()==Qt.RightButton:
            if hit:self.contextRequested.emit(hit,event.globalPosition().toPoint())
        elif event.button()==Qt.LeftButton:self.press=(hit,event.position())
    def mouseReleaseEvent(self,event):
        if not self.interactive or event.button()!=Qt.LeftButton:return
        at=self.point(event.position());hit=self.hit(at)
        if self.press and hit and self.press[0]==hit:self.opened.emit(hit)
        else:
            for name,pt in self.controls():
                if (at-pt).manhattanLength()<30:self.control.emit(name);break
        self.press=None
    def wheelEvent(self,event):
        if self.interactive and self.library.data['WheelDialEnabled']:self.turn(1 if event.angleDelta().y()<0 else -1)
        event.accept()
    def dragEnterEvent(self,event):
        if event.mimeData().hasFormat(MIME) or event.mimeData().hasUrls():event.acceptProposedAction()
    def dragMoveEvent(self,event):event.acceptProposedAction()
    def dropEvent(self,event):
        at=self.point(event.position());hit=self.hit(at);e=self.library.find(hit)
        if event.mimeData().hasFormat(MIME):
            ids=bytes(event.mimeData().data(MIME)).decode().split('\n')
            parent=e['Id'] if e and e['IsFolder'] and hit not in ids else self.current
            after=False;anchor=None
            if e:
                shape=self.geometry()[[x['Id'] for x in self.visible()].index(hit)][0];bounds=shape.boundingRect()
                if not e['IsFolder'] or at.x()<bounds.left()+bounds.width()*.23 or at.x()>bounds.right()-bounds.width()*.23:
                    parent=e['ParentId'];anchor=hit;after=at.x()>bounds.center().x()
            elif any(name=='back' and (at-pt).manhattanLength()<35 for name,pt in self.controls()):
                current=self.library.find(self.current);parent=current['ParentId'] if current else None
            self.rearrange.emit(ids,(parent,anchor),after)
        else:self.filesDropped.emit([u.toLocalFile() for u in event.mimeData().urls() if u.isLocalFile()],e['Id'] if e and e['IsFolder'] else self.current)
        event.acceptProposedAction()
