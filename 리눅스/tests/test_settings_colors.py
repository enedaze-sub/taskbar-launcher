import copy,tempfile,unittest
from pathlib import Path
from PySide6.QtWidgets import QApplication,QLabel
from decklauncher.app import demo,load_fonts
from decklauncher.model import Library,Store,COLORS
from decklauncher.view import contrast_text
from decklauncher.dialogs import SettingsColorsDialog,ThemeDialog,BackgroundDialog
class SettingsColorsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.app=QApplication.instance() or QApplication([]);load_fonts()
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.store=Store(Path(self.tmp.name));self.lib=demo()
    def tearDown(self):
        for w in self.app.topLevelWidgets():w.close()
        self.app.processEvents();self.tmp.cleanup()
    def test_01_legacy_colors_follow_launcher(self):
        self.assertFalse(self.lib.data['SettingsColors']['Separate']);self.lib.theme['Background']='#FFF5EC';self.assertEqual(self.lib.interface_theme['Background'],'#FFF5EC')
    def test_02_separate_palette_does_not_change_design_or_launcher(self):
        before=copy.deepcopy(self.lib.theme);d=SettingsColorsDialog(None,self.lib);d.separate.setChecked(True);d.palette(COLORS[2]);self.assertEqual(d.draft.theme,before);self.assertEqual(d.draft.interface_theme['Background'],COLORS[2][1]);self.assertEqual(self.lib.theme,before)
    def test_03_save_backup_preserves_settings_colors(self):
        self.lib.data['SettingsColors'].update(Separate=True,Initialized=True,Background='#FFF5EC');self.store.save(self.lib);self.assertEqual(self.store.load().interface_theme['Background'],'#FFF5EC');file=self.store.backup(Path(self.tmp.name)/'colors.zip',self.lib);self.assertEqual(self.store.restore(file).data['SettingsColors'],self.lib.data['SettingsColors'])
    def test_04_linked_again_keeps_custom_colors_for_next_time(self):
        self.lib.data['SettingsColors'].update(Separate=False,Initialized=True,Background='#FFF5EC');d=SettingsColorsDialog(None,self.lib);self.assertEqual(d.result['Background'],'#FFF5EC');d.separate.setChecked(True);self.assertEqual(d.draft.interface_theme['Background'],'#FFF5EC')
    def test_05_cancel_and_launcher_theme_changes_keep_settings_palette(self):
        before=copy.deepcopy(self.lib.data);d=SettingsColorsDialog(None,self.lib);d.separate.setChecked(True);d.palette(COLORS[2]);d.reject();self.assertEqual(self.lib.data,before)
        self.lib.data['SettingsColors'].update(Separate=True,Background='#FFF5EC');t=ThemeDialog(None,self.store,self.lib);t.palette(COLORS[4]);self.assertIn('#FFF5EC',t.styleSheet());self.assertEqual(t.draft.interface_theme['Background'],'#FFF5EC')
    def test_06_bad_colors_rejected_without_overwriting_file(self):
        self.store.save(self.lib);before=self.store.file.read_bytes();self.lib.data['SettingsColors']['Accent']='bad'
        with self.assertRaises(ValueError):self.store.save(self.lib)
        self.assertEqual(before,self.store.file.read_bytes())
    def test_07_badge_contrast_for_bright_and_dark_accents(self):
        def luma(c):
            from PySide6.QtGui import QColor
            c=QColor(c);f=lambda v:v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4
            return .2126*f(c.red())+.7152*f(c.green())+.0722*f(c.blue())
        for c in ['#FFFFFF','#FFFF00','#FFF5EC','#777777','#35F0FF','#F2879B','#111C2B']:
            a,b=sorted([luma(c),luma(contrast_text(c))]);self.assertGreaterEqual((b+.05)/(a+.05),4.5)
    def test_08_recovery_hint_is_in_overall_transparency_dialog(self):
        d=BackgroundDialog(None,self.store,self.lib);self.assertTrue(any('한 번 더' in w.text() and '설정창' in w.text() for w in d.findChildren(QLabel)))
