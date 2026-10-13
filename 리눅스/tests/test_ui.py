import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from PySide6.QtCore import Qt,QPointF,QMimeData,QEvent
from PySide6.QtGui import QFontDatabase,QPixmap,QImage,QColor,QDropEvent
from PySide6.QtWidgets import QApplication,QDialog
from PySide6.QtTest import QTest
from decklauncher.app import Controller,demo,load_fonts
from decklauncher.model import Store,Library,DESIGNS,COLORS,entry
from decklauncher.view import Canvas,shell,slots,MIME
from decklauncher.dialogs import ThemeDialog,BackgroundDialog,FontDialog,Editor,Settings,Help,ActionSheet

class QtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.app=QApplication.instance() or QApplication([]);cls.app.setQuitOnLastWindowClosed(False);load_fonts()
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.store=Store(Path(self.tmp.name)/'data');self.lib=demo();self.c=Controller(self.app,self.store,self.lib)
    def tearDown(self):
        self.c.exiting=True;self.c.close_popups();self.c.watcher.close();self.c.window.close()
        for w in QApplication.topLevelWidgets():w.close()
        QApplication.processEvents();self.tmp.cleanup()
    def test_01_bundled_fonts_resolve(self):self.assertIn('Jua',QFontDatabase.families());self.assertIn('Gowun Dodum',QFontDatabase.families())
    def test_02_six_shells_different(self):
        self.assertEqual(len({shell(k).toFillPolygon().size() for k,n in DESIGNS})>=4,True)
        for key,name in DESIGNS:self.assertTrue(shell(key).contains(QPointF(460,429)),key)
    def test_03_hit_all_slot_centers(self):
        for key,name in DESIGNS:
            for count in range(5,10):
                self.lib.theme['Design']=key
                for shape,point in slots(key,count,self.lib.theme):
                    self.assertTrue(shape.contains(point),f'{key} {count}');self.assertTrue(shell(key).contains(point),f'{key} clip')
    def test_04_normal_page_buttons_wrap(self):
        self.assertEqual(len(self.c.canvas.visible()),7);self.c.canvas.change_page(1);self.assertEqual(len(self.c.canvas.visible()),2)
        self.c.canvas.change_page(1);self.assertEqual(self.c.canvas.page,0)
    def test_05_wheel_disabled_is_noop(self):
        before=[x['Id'] for x in self.c.canvas.visible()];self.c.canvas.turn(1);self.assertIsNone(self.c.canvas.dial);self.assertEqual(before,[x['Id'] for x in self.c.canvas.visible()])
    def test_06_wheel_cycles_one_item(self):
        self.lib.data['WheelDialEnabled']=True;before=self.c.canvas.visible()[1]['Id'];self.c.canvas.turn(1);QTest.qWait(210)
        self.assertEqual(self.c.canvas.dial,1);self.assertEqual(self.c.canvas.visible()[0]['Id'],before)
    def test_07_wheel_reverse_wrap(self):
        self.lib.data['WheelDialEnabled']=True;self.c.canvas.turn(-1);QTest.qWait(210);self.assertEqual(self.c.canvas.dial,8)
    def test_08_folder_navigation(self):
        folder=self.lib.entries[0];self.c.open(folder['Id']);self.assertEqual(len(self.c.canvas.visible()),2);self.c.back();self.assertIsNone(self.c.canvas.current)
    def test_09_successful_launch_hides(self):
        self.c.window.show();self.lib.data['HideAfterLaunch']=False
        with patch('decklauncher.app.start') as start:
            start.return_value.poll.return_value=None;self.c.open(self.lib.entries[3]['Id'])
        self.assertFalse(self.c.window.isVisible())
    def test_10_focus_loss_hides(self):
        self.c.window.show()
        with patch.object(self.app,'applicationState',return_value=Qt.ApplicationInactive):self.c.focus_loss()
        self.assertFalse(self.c.window.isVisible())
    def test_11_modal_dialog_protects_launcher(self):
        self.c.window.show();self.c.modal=1
        with patch.object(self.app,'applicationState',return_value=Qt.ApplicationInactive):self.c.focus_loss()
        self.assertTrue(self.c.window.isVisible());self.c.modal=0
    def test_12_theme_color_keeps_design(self):
        self.lib.theme['Design']='cat';d=ThemeDialog(self.c.window,self.store,self.lib);d.palette(COLORS[9]);self.assertEqual(d.draft.theme['Design'],'cat');d.close()
    def test_13_design_keeps_colors(self):
        d=ThemeDialog(self.c.window,self.store,self.lib);before=d.draft.theme['Accent'];d.design('neon');self.assertEqual(d.draft.theme['Accent'],before);d.close()
    def test_14_theme_cancel_isolated(self):
        before=copy.deepcopy(self.lib.data);d=ThemeDialog(self.c.window,self.store,self.lib);d.palette(COLORS[9]);d.design('cat');d.reject();self.assertEqual(self.lib.data,before)
    def test_15_custom_themes_separate(self):
        d=ThemeDialog(self.c.window,self.store,self.lib);d.custom_name.setText('내 테마');d.save_custom();self.assertEqual(len(d.draft.data['CustomThemes']),1);self.assertEqual(len(COLORS),10);d.close()
    def test_16_save_load_custom_composition(self):
        d=ThemeDialog(self.c.window,self.store,self.lib);d.design('space');d.slot_count.setValue(9);d.custom_name.setText('별');d.save_custom();d.design('modern');d.load_custom();self.assertEqual(d.draft.theme['Design'],'space');self.assertEqual(d.draft.theme['Slots'],9);d.close()
    def test_17_background_preview_actual_image(self):
        image=Path(self.tmp.name)/'image.png';pix=QPixmap(200,100);pix.fill(QColor('#EF00FF'));pix.save(str(image));name=self.store.copy_image(image)
        d=BackgroundDialog(self.c.window,self.store,self.lib);d.result['ImageFile']=name;d.sliders['Zoom'].setValue(200)
        self.assertEqual(d.result['Zoom'],2);self.assertFalse(d.canvas.images.image(name).isNull());self.assertIsNone(self.lib.data['Background']['ImageFile']);d.close()
    def test_18_per_slot_background_isolated(self):
        e=self.lib.entries[3];d=BackgroundDialog(self.c.window,self.store,self.lib,e['Id']);d.sliders['Darkness'].setValue(70)
        self.assertEqual(d.result['Darkness'],.7);self.assertEqual(self.lib.data['Background']['Darkness'],.42);self.assertEqual(e['Background']['Darkness'],.42);d.close()
    def test_19_image_cache_is_bounded(self):
        for i in range(100):self.c.canvas.images.remember(('test',i),lambda:i)
        self.assertEqual(len(self.c.canvas.images.cache),64)
    def test_20_editor_preserves_id_note_icon(self):
        e=self.lib.entries[3];d=Editor(self.c.window,self.store,self.lib,e);d.name.setText('이름 바꿈');d.accept();self.assertEqual(d.result['Id'],e['Id']);self.assertEqual(d.result['Note'],e['Note']);self.assertEqual(d.result['IconFile'],e['IconFile']);d.close()
    def test_21_settings_theme_applied(self):
        self.lib.theme['Background']='#FFF5EC';d=Settings(self.c.window,self.c);self.assertIn('#FFF5EC',d.styleSheet());self.assertEqual(d.tree.topLevelItemCount(),9);d.close()
    def test_22_help_linux_instructions(self):
        d=Help(self.c.window,self.lib);self.assertIn('리눅스',d.windowTitle());self.assertGreater(d.body.count(),5);d.close()
    def test_23_hover_folder_popup_children(self):
        self.c.window.show();folder=self.lib.entries[0];self.c.schedule_popup(folder,QPointF(400,250),0);self.c.show_pending_popup();self.assertEqual(len(self.c.popups),1);self.assertEqual(self.c.popups[0].folder_id,folder['Id'])
    def test_24_launcher_drag_into_folder_and_out(self):
        folder=self.lib.entries[0];e=self.lib.entries[3];self.c.move([e['Id']],folder['Id']);self.assertEqual(e['ParentId'],folder['Id']);self.c.move([e['Id']],None);self.assertIsNone(e['ParentId'])
    def test_25_no_topmost_windows(self):self.assertFalse(bool(self.c.window.windowFlags() & Qt.WindowStaysOnTopHint))
    def test_26_six_designs_render(self):
        for key,name in DESIGNS:
            self.lib.theme['Design']=key;w=Canvas(self.store,self.lib,interactive=False);w.resize(920,454);w.show();self.app.processEvents();pix=w.grab();self.assertFalse(pix.isNull());self.assertEqual(pix.width(),920);w.close()
    def test_27_root_drag_drop_signal(self):
        from decklauncher.dialogs import RootDrop
        from PySide6.QtGui import QDropEvent
        target=RootDrop();mime=QMimeData();mime.setData(MIME,self.lib.entries[3]['Id'].encode());seen=[];target.dropped.connect(seen.append)
        ev=QDropEvent(QPointF(10,10),Qt.MoveAction,mime,Qt.LeftButton,Qt.NoModifier);target.dropEvent(ev);self.assertEqual(seen,[[self.lib.entries[3]['Id']]])
    def test_28_canvas_actual_drop_reorders(self):
        canvas=self.c.canvas;canvas.resize(920,454);visible=canvas.visible();target=visible[4];at=canvas.geometry()[4][1]
        # A drop into a non-folder uses the visible sibling as the reorder anchor.
        before=[x['Id'] for x in self.lib.children()];mime=QMimeData();mime.setData(MIME,visible[6]['Id'].encode());ev=QDropEvent(at,Qt.MoveAction,mime,Qt.LeftButton,Qt.NoModifier);canvas.dropEvent(ev)
        after=[x['Id'] for x in self.lib.children()];self.assertNotEqual(before,after);self.assertEqual(abs(after.index(target['Id'])-after.index(visible[6]['Id'])),1)
    def test_29_font_dialog_draft(self):
        before=copy.deepcopy(self.lib.data['Fonts']);d=FontDialog(self.c.window,self.store,self.lib);d.changed('Launcher','Gowun Dodum');self.assertEqual(self.lib.data['Fonts'],before);self.assertEqual(d.result['Launcher'],'Gowun Dodum');d.close()
    def test_30_scaled_canvas_hit(self):
        self.c.canvas.resize(460,227);pt=self.c.canvas.geometry()[0][1];self.assertEqual(self.c.canvas.hit(self.c.canvas.point(pt*.5)),self.c.canvas.visible()[0]['Id'])
    def test_31_outside_screen_click_hides(self):
        self.c.window.move(0,0);self.c.window.show();self.c.outside_click(QPointF(1500,900).toPoint());self.assertFalse(self.c.window.isVisible())
    def test_32_inside_screen_click_stays(self):
        self.c.window.move(0,0);self.c.window.show();self.c.outside_click(QPointF(460,429).toPoint());self.assertTrue(self.c.window.isVisible())
    def test_33_window_placement_four_edges(self):
        for edge in ('left','right','top','bottom','auto'):
            self.lib.data['LinuxPlacement']['Edge']=edge;self.c.reveal();self.assertTrue(self.c.window.isVisible());self.assertGreater(self.c.window.width(),0)
    def test_34_missing_windows_font_uses_bundled(self):
        from decklauncher.fonts import resolved
        self.lib.data['Fonts']=dict(Launcher='Nonexistent Windows Font',Interface='Another Missing Font');self.assertEqual(resolved(self.lib,True),'Jua');self.assertEqual(resolved(self.lib),'Gowun Dodum')
    def test_35_wheel_disable_returns_to_pages(self):
        self.lib.data['WheelDialEnabled']=True;self.c.canvas.turn(1);QTest.qWait(210);self.lib.data['WheelDialEnabled']=False;self.c.save();self.assertIsNone(self.c.canvas.dial)
    def test_36_saved_custom_toggles_restore(self):
        d=ThemeDialog(self.c.window,self.store,self.lib);d.toggles['ShowNotes'].setChecked(False);d.custom_name.setText('메모 없이');d.save_custom();d.toggles['ShowNotes'].setChecked(True);d.load_custom();self.assertFalse(d.toggles['ShowNotes'].isChecked());self.assertFalse(d.draft.theme['ShowNotes']);d.close()
    def test_37_tree_drop_into_folder(self):
        d=Settings(self.c.window,self.c);d.show();self.app.processEvents();folder=self.lib.entries[0];moving=self.lib.entries[3]
        at=d.tree.visualItemRect(d.tree.topLevelItem(0)).center();mime=QMimeData();mime.setData(MIME,moving['Id'].encode());event=QDropEvent(QPointF(at),Qt.MoveAction,mime,Qt.LeftButton,Qt.NoModifier)
        d.tree.dropEvent(event);self.assertEqual(moving['ParentId'],folder['Id']);d.close()
    def test_38_closed_dialogs_release_owned_previews(self):
        from PySide6.QtCore import QTimer,QCoreApplication
        for _ in range(4):
            d=Help(self.c.window,self.lib);QTimer.singleShot(0,d.accept);self.c.exec_dialog(d)
        QCoreApplication.sendPostedEvents(None,QEvent.DeferredDelete)
        self.assertEqual(len(self.c.window.findChildren(QDialog)),0)
