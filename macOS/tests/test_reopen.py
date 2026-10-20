import tempfile, unittest
from pathlib import Path
from PySide6.QtWidgets import QApplication
from decklauncher.app import Controller,demo,load_fonts
from decklauncher.model import Store

class ReopenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.app=QApplication.instance() or QApplication([])
        cls.app.setQuitOnLastWindowClosed(False)
        load_fonts()
    def test_reopen_resets_folder_page_dial_and_hover(self):
        with tempfile.TemporaryDirectory() as tmp:
            lib=demo();c=Controller(self.app,Store(Path(tmp)),lib)
            try:
                folder=next(e for e in lib.entries if e['IsFolder'])
                c.canvas.navigate(folder['Id']);c.canvas.page=3;c.canvas.dial=2;c.canvas.hover_id=folder['Id']
                c.hide();c.reveal()
                self.assertIsNone(c.canvas.current)
                self.assertEqual(c.canvas.page,0)
                self.assertIsNone(c.canvas.dial)
                self.assertIsNone(c.canvas.hover_id)
            finally:
                c.watcher.close();c.exiting=True;c.window.close()
                if c.tray:c.tray.hide()
                self.app.processEvents()
