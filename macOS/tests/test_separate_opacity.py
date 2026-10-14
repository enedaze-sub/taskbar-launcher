import copy,tempfile,unittest
from pathlib import Path
from PySide6.QtWidgets import QApplication
from PySide6.QtCore import Qt
from PySide6.QtGui import QImage,QPainter,QColor,QIcon,QPixmap
from decklauncher.model import Library,Store,entry
from decklauncher.app import demo,load_fonts
from decklauncher.dialogs import BackgroundDialog,Editor
from decklauncher.view import Canvas

class SeparateOpacityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.app=QApplication.instance() or QApplication([]);load_fonts()
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.store=Store(Path(self.tmp.name));self.lib=demo()
    def tearDown(self):
        for w in self.app.topLevelWidgets():w.close()
        self.app.processEvents();self.tmp.cleanup()
    def test_01_old_data_remains_linked(self):
        e=entry('기존',Opacity=.3)
        for k in ('SeparateOpacity','IconOpacity','TextOpacity'):e.pop(k)
        loaded=Library(dict(Entries=[e]));self.assertFalse(loaded.entries[0]['SeparateOpacity']);self.assertEqual(loaded.entries[0]['Opacity'],.3)
    def test_02_save_backup_restore_keeps_mode_and_values(self):
        e=self.lib.entries[0];e.update(SeparateOpacity=True,Opacity=1.75,IconOpacity=.4,TextOpacity=.7)
        self.store.save(self.lib);self.assertEqual(self.store.load().find(e['Id']),e)
        file=self.store.backup(Path(self.tmp.name)/'test.zip',self.lib);self.assertEqual(self.store.restore(file).find(e['Id']),e)
    def test_03_invalid_independent_alpha_rejected_before_batch(self):
        before=copy.deepcopy(self.lib.data)
        for value in (-.01,1.01,float('nan'),True):
            with self.assertRaises(ValueError):self.lib.apply_backgrounds([self.lib.entries[0]['Id']],opacity=.4,icon_opacity=value)
        self.assertEqual(before,self.lib.data)
    def test_04_dialog_live_preview_and_cancel(self):
        e=self.lib.entries[3];before=copy.deepcopy(e);d=BackgroundDialog(None,self.store,self.lib,e['Id'])
        self.assertFalse(d.separate.isChecked());self.assertFalse(d.icon_opacity.isEnabled())
        d.separate.setChecked(True);d.opacity.setValue(65);d.icon_opacity.setValue(10);d.text_opacity.setValue(35)
        self.assertEqual(d.draft.find(e['Id'])['IconOpacity'],.9);self.assertEqual(d.draft.find(e['Id'])['TextOpacity'],.65)
        d.reject();self.assertEqual(e,before)
    def test_05_relink_uses_whole_group_but_keeps_separate_values(self):
        d=BackgroundDialog(None,self.store,self.lib,self.lib.entries[3]['Id']);d.separate.setChecked(True);d.icon_opacity.setValue(33);d.separate.setChecked(False)
        self.assertFalse(d.opacity_values['SeparateOpacity']);self.assertEqual(d.opacity_values['IconOpacity'],.67);self.assertFalse(d.icon_opacity.isEnabled())
    def test_06_batch_only_image_preserves_modes(self):
        e=self.lib.entries[0];e.update(SeparateOpacity=True,IconOpacity=.5,TextOpacity=.2)
        d=BackgroundDialog(None,self.store,self.lib,bulk=True);d.apply_opacity.setChecked(False);d.apply_image.setChecked(True)
        self.assertEqual(d.draft.find(e['Id']),e)
    def test_07_bulk_scope_applies_same_independent_values(self):
        folder=self.lib.entries[0];d=BackgroundDialog(None,self.store,self.lib,bulk=True,parent_id=folder['Id']);d.scope.setCurrentIndex(1);d.separate.setChecked(True);d.icon_opacity.setValue(28);d.text_opacity.setValue(40)
        self.lib.apply_backgrounds(d.target_ids(),opacity=d.result_opacity,**d.opacity_args())
        for e in self.lib.children(folder['Id']):self.assertTrue(e['SeparateOpacity']);self.assertEqual(e['IconOpacity'],.72);self.assertEqual(e['TextOpacity'],.6)
        self.assertFalse(folder['SeparateOpacity'])
    def test_08_single_apply_retains_target_and_notes(self):
        e=self.lib.entries[3];before=copy.deepcopy(e);d=BackgroundDialog(None,self.store,self.lib,e['Id']);d.separate.setChecked(True);d.text_opacity.setValue(60);d.apply_to(e)
        self.assertEqual(e['TextOpacity'],.4)
        for k in ('Id','Name','Target','Arguments','Note'):self.assertEqual(e[k],before[k])
    def test_09_real_render_independent_alpha_all_six_designs(self):
        for design in ('classic','cat','neon','paper','modern','space'):
            with self.subTest(design=design):
                self.lib.theme['Design']=design;canvas=Canvas(self.store,self.lib);e=canvas.visible()[0];shape,pt=canvas.geometry()[0];colors=[QColor(self.lib.theme[k]) for k in ('Background','Surface','Accent','Text')]
                def image(opacity,icon,text,linked=False):
                    e.update(Opacity=opacity,SeparateOpacity=not linked,IconOpacity=icon,TextOpacity=text)
                    img=QImage(920,454,QImage.Format_ARGB32_Premultiplied);img.fill(Qt.transparent);p=QPainter(img);p.setRenderHint(QPainter.Antialiasing);canvas.draw_slot(p,e,0,shape,pt,*colors);p.end();return img
                full=image(.15,1,1);hidden=image(.15,0,0);low=image(.15,.1,.1);linked=image(.15,1,1,True)
                self.assertNotEqual(full,hidden);self.assertNotEqual(full,low)
                # Opaque foreground pixels remain visible above faint backgrounds.
                alphas=lambda img:max(img.pixelColor(x,y).alpha() for x in range(max(0,int(shape.boundingRect().left())),min(919,int(shape.boundingRect().right()))+1,2) for y in range(max(0,int(shape.boundingRect().top())),min(453,int(shape.boundingRect().bottom()))+1,2))
                self.assertGreater(alphas(full),240);self.assertLess(alphas(low),90);self.assertLess(alphas(linked),45)
    def test_10_whole_launcher_composes_after_independent_layers(self):
        canvas=Canvas(self.store,self.lib);self.lib.entries[0].update(SeparateOpacity=True,Opacity=.2,IconOpacity=1,TextOpacity=1)
        def render(opacity):
            self.lib.data['Appearance']['Opacity']=opacity;img=QImage(920,454,QImage.Format_ARGB32_Premultiplied);img.fill(Qt.transparent);p=QPainter(img);canvas.composite(p,opacity,canvas.draw_content);p.end();return img
        full=render(1);faint=render(.4);self.assertGreater(max(full.pixelColor(x,y).alpha() for x in range(0,920,10) for y in range(0,454,10)),240);self.assertLess(max(faint.pixelColor(x,y).alpha() for x in range(0,920,10) for y in range(0,454,10)),105)
    def test_11_dialog_small_screen_keeps_footer_reachable(self):
        from PySide6.QtWidgets import QDialogButtonBox,QScrollArea
        d=BackgroundDialog(None,self.store,self.lib,bulk=True);d.resize(900,720);d.show();self.app.processEvents()
        self.assertLessEqual(d.height(),720);footer=d.findChild(QDialogButtonBox);self.assertLessEqual(footer.mapTo(d,footer.rect().bottomRight()).y(),d.height());self.assertIsNotNone(d.findChild(QScrollArea))
