
import copy,tempfile,unittest
from pathlib import Path
from PySide6.QtWidgets import QApplication,QComboBox,QSlider
from PySide6.QtCore import Qt
from PySide6.QtGui import QImage,QPainter,QColor
from decklauncher.model import Library,Store,entry,background
from decklauncher.app import demo,load_fonts
from decklauncher.view import Canvas
from decklauncher.dialogs import BackgroundDialog,AppearanceDialog
class BackgroundBulkTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.app=QApplication.instance() or QApplication([]);load_fonts()
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.store=Store(Path(self.tmp.name));self.lib=demo()
    def tearDown(self):
        for w in self.app.topLevelWidgets():w.close()
        self.app.processEvents();self.tmp.cleanup()
    def test_01_negative_density_saved(self):
        self.lib.entries[0]['Opacity']=2;self.store.save(self.lib);self.assertEqual(self.store.load().entries[0]['Opacity'],2)
    def test_02_scopes(self):
        folder=self.lib.entries[0];nested=entry('하위',True,parent=folder['Id']);child=entry('깊은 앱',parent=nested['Id']);self.lib.entries.extend([nested,child])
        self.assertEqual(len(self.lib.background_targets(None,'all')),13);self.assertEqual(len(self.lib.background_targets(folder['Id'],'level')),3);self.assertEqual(len(self.lib.background_targets(folder['Id'],'tree')),4)
    def test_03_opacity_only_preserves_images_and_names(self):
        for i,e in enumerate(self.lib.entries):e['Background']['ImageFile']='before-'+str(i)+'.png'
        before=copy.deepcopy(self.lib.entries);ids=[e['Id'] for e in self.lib.entries];self.lib.apply_backgrounds(ids,opacity=1.75)
        for old,new in zip(before,self.lib.entries):self.assertEqual(old['Background'],new['Background']);self.assertEqual(old['Name'],new['Name']);self.assertEqual(new['Opacity'],1.75)
    def test_04_image_only_preserves_opacity_and_independent_copy(self):
        self.lib.entries[0]['Opacity']=.4;self.lib.apply_backgrounds([e['Id'] for e in self.lib.entries],dict(background(),ImageFile='shared.png'))
        self.assertEqual(self.lib.entries[0]['Opacity'],.4);self.lib.entries[0]['Background']['Zoom']=2;self.assertEqual(self.lib.entries[1]['Background']['Zoom'],1)
    def test_05_failed_batch_is_atomic(self):
        before=copy.deepcopy(self.lib.data)
        with self.assertRaises(ValueError):self.lib.apply_backgrounds([self.lib.entries[0]['Id'],'bad'],opacity=1.5)
        self.assertEqual(before,self.lib.data)
    def test_06_background_slot_slider_negative_preview_cancel(self):
        e=self.lib.entries[3];d=BackgroundDialog(None,self.store,self.lib,e['Id']);d.opacity.setValue(-70);self.assertEqual(d.result_opacity,1.7);self.assertEqual(d.draft.find(e['Id'])['Opacity'],1.7);d.reject();self.assertEqual(e['Opacity'],1)
    def test_07_global_transparency_in_background(self):
        d=BackgroundDialog(None,self.store,self.lib);self.assertEqual(d.opacity.minimum(),0);d.opacity.setValue(55);self.assertEqual(d.draft.data['Appearance']['Opacity'],.45);d.close()
    def test_08_bulk_draft_scope_and_selective_flags(self):
        before=copy.deepcopy(self.lib.data);d=BackgroundDialog(None,self.store,self.lib,bulk=True,parent_id=self.lib.entries[0]['Id']);d.scope.setCurrentIndex(1);d.opacity.setValue(-60)
        self.assertEqual(len(d.target_ids()),2);self.assertFalse(d.apply_image.isChecked());self.assertEqual(d.draft.find(d.target_ids()[0])['Opacity'],1.6);self.assertEqual(self.lib.data,before);d.close()
    def test_09_text_dialog_no_slot_list_or_opacity_controls(self):
        d=AppearanceDialog(None,self.store,self.lib);self.assertEqual(len(d.findChildren(QComboBox)),0);self.assertEqual(len(d.findChildren(QSlider)),0);d.title.setText('문구만');self.assertEqual(d.result['Opacity'],1);d.close()
    def test_10_negative_density_really_covers_underlay(self):
        self.lib.theme['Design']='modern';canvas=Canvas(self.store,self.lib);e=canvas.visible()[0];shape,pt=canvas.geometry()[0];bounds=shape.boundingRect();colors=[QColor(self.lib.theme[k]) for k in ('Background','Surface','Accent','Text')]
        def sample(opacity):
            e['Opacity']=opacity;image=QImage(920,454,QImage.Format_ARGB32_Premultiplied);image.fill(Qt.transparent);p=QPainter(image);canvas.draw_slot(p,e,0,shape,pt,*colors);p.end();return image.pixelColor(round(bounds.left()+15),round(bounds.center().y())).alpha()
        self.assertLess(sample(1),100);self.assertGreater(sample(2),250)
    def test_11_invalid_density_rejected(self):
        with self.assertRaises(ValueError):self.lib.apply_backgrounds([self.lib.entries[0]['Id']],opacity=2.1)
