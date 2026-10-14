import copy,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
from PySide6.QtWidgets import QApplication
from PySide6.QtCore import Qt
from PySide6.QtGui import QImage,QPainter,QColor
from decklauncher.model import Library,Store
from decklauncher.app import Controller,demo,load_fonts
from decklauncher.dialogs import BackgroundDialog
from decklauncher.view import Canvas

class FullTransparencyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.app=QApplication.instance() or QApplication([]);cls.app.setQuitOnLastWindowClosed(False);load_fonts()
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.store=Store(Path(self.tmp.name));self.lib=demo()
    def tearDown(self):
        for w in self.app.topLevelWidgets():w.close()
        self.app.processEvents();self.tmp.cleanup()
    def test_01_zero_alpha_save_backup_restore(self):
        self.lib.data['Appearance']['Opacity']=0;self.lib.entries[0]['Opacity']=0;self.store.save(self.lib)
        self.assertEqual(self.store.load().data,self.lib.data);file=self.store.backup(Path(self.tmp.name)/'zero.zip',self.lib);self.assertEqual(self.store.restore(file).data,self.lib.data)
    def test_02_individual_and_bulk_sliders_reach_100(self):
        for options in ({'item_id':self.lib.entries[0]['Id']},{'bulk':True},{}):
            d=BackgroundDialog(None,self.store,self.lib,**options);self.assertEqual(d.opacity.maximum(),100);d.opacity.setValue(100);self.assertEqual(d.result_opacity,0);d.close()
    def test_03_bulk_zero_alpha_only_preserves_images(self):
        before=copy.deepcopy(self.lib.entries);ids=[e['Id'] for e in self.lib.entries];self.lib.apply_backgrounds(ids,opacity=0)
        for old,new in zip(before,self.lib.entries):self.assertEqual(old['Background'],new['Background']);self.assertEqual(new['Opacity'],0)
    def test_04_six_designs_zero_slots_and_independent_foreground(self):
        for design in ('classic','cat','neon','paper','modern','space'):
            with self.subTest(design=design):
                self.lib.theme['Design']=design;canvas=Canvas(self.store,self.lib);e=canvas.visible()[0];shape,pt=canvas.geometry()[0];colors=[QColor(self.lib.theme[k]) for k in ('Background','Surface','Accent','Text')]
                def render(separate):
                    e.update(Opacity=0,SeparateOpacity=separate,IconOpacity=1,TextOpacity=1);img=QImage(920,454,QImage.Format_ARGB32_Premultiplied);img.fill(Qt.transparent);p=QPainter(img);canvas.draw_slot(p,e,0,shape,pt,*colors);p.end();return img
                def maximum(img):return max(img.pixelColor(x,y).alpha() for x in range(0,920,3) for y in range(0,454,3))
                self.assertEqual(maximum(render(False)),0);self.assertGreater(maximum(render(True)),240)
                self.assertEqual(canvas.hit(pt),e['Id'])
    def test_05_whole_launcher_renders_all_pixels_transparent(self):
        canvas=Canvas(self.store,self.lib);self.lib.data['Appearance']['Opacity']=0;img=QImage(920,454,QImage.Format_ARGB32_Premultiplied);img.fill(Qt.transparent);p=QPainter(img);canvas.composite(p,0,canvas.draw_content);p.end()
        self.assertEqual(max(img.pixelColor(x,y).alpha() for x in range(0,920,3) for y in range(0,454,3)),0)
    def test_06_hidden_launcher_reopens_settings_on_activation(self):
        self.lib.data['Appearance']['Opacity']=0;c=Controller(self.app,self.store,self.lib)
        try:
            with patch.object(c,'manage') as manage:c.reveal();c.toggle();self.assertEqual(manage.call_count,2)
        finally:c.exiting=True;c.watcher.close();c.window.close()
