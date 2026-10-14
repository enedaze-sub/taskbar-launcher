
import copy,json,tempfile,unittest
from pathlib import Path
from PySide6.QtCore import Qt,QRectF
from PySide6.QtGui import QImage,QPainter,QColor
from PySide6.QtWidgets import QApplication
from decklauncher.model import Library,Store,entry,appearance,DESIGNS
from decklauncher.app import demo,load_fonts
from decklauncher.view import Canvas
from decklauncher.dialogs import AppearanceDialog,Editor

class AppearanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.app=QApplication.instance() or QApplication([]);load_fonts()
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.store=Store(Path(self.tmp.name));self.lib=demo()
    def tearDown(self):
        for w in QApplication.topLevelWidgets():w.close()
        self.app.processEvents();self.tmp.cleanup()
    def test_01_legacy_defaults(self):
        lib=Library(dict(Entries=[dict(Id='old',Name='예전 앱')]))
        self.assertEqual(lib.data['Appearance'],appearance());self.assertEqual(lib.entries[0]['Opacity'],1)
    def test_02_new_fields_saved_and_backed_up(self):
        self.lib.data['Appearance']=dict(Title='우리집 런처',Note='오늘도 즐겁게\n차근차근',Opacity=.55);self.lib.entries[0]['Opacity']=.25
        self.store.save(self.lib);self.assertEqual(self.store.load().data,self.lib.data)
        backup=Path(self.tmp.name)/'backup.zip';self.store.backup(backup,self.lib);self.assertEqual(self.store.restore(backup).data,self.lib.data)
    def test_03_invalid_global_opacity_rejected(self):
        for value in (0,1.1,float('nan'),True,'0.5'):
            data=copy.deepcopy(self.lib.data);data['Appearance']['Opacity']=value
            with self.assertRaises(ValueError):Library(data)
    def test_04_invalid_slot_opacity_rejected(self):
        for value in (.1,2.1,float('inf'),False):
            data=copy.deepcopy(self.lib.data);data['Entries'][0]['Opacity']=value
            with self.assertRaises(ValueError):Library(data)
    def test_05_text_limits_and_empty_supported(self):
        self.lib.data['Appearance'].update(Title='',Note='');self.lib.validate()
        for key,length in [('Title',61),('Note',301)]:
            data=copy.deepcopy(self.lib.data);data['Appearance'][key]='가'*length
            with self.assertRaises(ValueError):Library(data)
    def test_06_custom_root_note_and_hover_restore(self):
        self.lib.data['Appearance'].update(Title='내 공간',Note='내가 쓴 문구');canvas=Canvas(self.store,self.lib)
        self.assertEqual(canvas.central_text(),('내 공간','내가 쓴 문구'));canvas.hover_id=self.lib.entries[3]['Id'];self.assertEqual(canvas.central_text()[1],self.lib.entries[3]['Note'])
        canvas.hover_id=None;self.assertEqual(canvas.central_text()[1],'내가 쓴 문구')
    def test_07_folder_name_and_custom_empty_library(self):
        self.lib.data['Appearance'].update(Title='내 공간',Note='문구');canvas=Canvas(self.store,self.lib);canvas.navigate(self.lib.entries[0]['Id']);self.assertEqual(canvas.central_text()[0],'게임')
        empty=Library();empty.data['Appearance'].update(Note='비어 있어도 내 문구');self.assertEqual(Canvas(self.store,empty).central_text()[1],'비어 있어도 내 문구')
    def test_08_dialog_cancel_keeps_original(self):
        original=copy.deepcopy(self.lib.data);d=AppearanceDialog(None,self.store,self.lib);d.title.setText('바꾼 제목');d.note.setPlainText('바꾼 문구');d.reject();self.assertEqual(self.lib.data,original)
    def test_09_dialog_draft_live_preview(self):
        d=AppearanceDialog(None,self.store,self.lib);d.title.setText('내 공간');d.note.setPlainText('직접 쓴 메모');self.assertEqual(d.canvas.central_text(),('내 공간','직접 쓴 메모'));d.close()
    def test_10_editor_preserves_existing_density(self):
        e=self.lib.entries[3];e['Opacity']=1.6;d=Editor(None,self.store,self.lib,e);d.accept();self.assertEqual(d.result['Opacity'],1.6);self.assertEqual(d.result['Note'],e['Note']);d.close()
    def test_11_real_render_global_alpha_six_designs(self):
        for design,name in DESIGNS:
            self.lib.theme['Design']=design;self.lib.data['Appearance']['Opacity']=.4;canvas=Canvas(self.store,self.lib)
            image=QImage(920,454,QImage.Format_ARGB32_Premultiplied);image.fill(Qt.transparent);p=QPainter(image);canvas.draw(p);p.end()
            self.assertLessEqual(image.pixelColor(460,440).alpha(),103);self.assertGreater(image.pixelColor(460,440).alpha(),90)
    def test_12_slot_group_alpha_does_not_accumulate(self):
        canvas=Canvas(self.store,self.lib);e=canvas.visible()[0];e['Opacity']=.5;shape,point=canvas.geometry()[0];colors=[QColor(self.lib.theme[k]) for k in ('Background','Surface','Accent','Text')]
        image=QImage(920,454,QImage.Format_ARGB32_Premultiplied);image.fill(Qt.transparent);p=QPainter(image);canvas.draw_slot(p,e,0,shape,point,*colors);p.end()
        self.assertLessEqual(image.pixelColor(round(point.x()),round(point.y())).alpha(),129);self.assertGreater(image.pixelColor(round(point.x()),round(point.y())).alpha(),120)
    def test_13_composite_restores_painter_opacity(self):
        image=QImage(920,454,QImage.Format_ARGB32_Premultiplied);image.fill(Qt.transparent);p=QPainter(image);p.setOpacity(.6)
        Canvas(self.store,self.lib).composite(p,.5,lambda layer:layer.fillRect(QRectF(0,0,80,80),QColor('#FF0000')));self.assertAlmostEqual(p.opacity(),.6);p.end();self.assertAlmostEqual(image.pixelColor(20,20).alpha(),76,delta=2)
    def test_14_custom_wording_kept_when_colors_change(self):
        self.lib.data['Appearance']['Title']='내 런처';self.lib.entries[0]['Opacity']=.35;self.lib.theme['Design']='space';self.lib.validate();self.assertEqual(self.lib.data['Appearance']['Title'],'내 런처');self.assertEqual(self.lib.entries[0]['Opacity'],.35)

    def test_15_high_dpi_composition(self):
        self.lib.data['Appearance']['Opacity']=.4;canvas=Canvas(self.store,self.lib)
        image=QImage(1840,908,QImage.Format_ARGB32_Premultiplied);image.setDevicePixelRatio(2);image.fill(Qt.transparent);p=QPainter(image);canvas.draw(p);p.end()
        self.assertAlmostEqual(image.pixelColor(920,880).alpha(),102,delta=2)
