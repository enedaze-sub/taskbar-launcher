
import json,tempfile,unittest,zipfile
from pathlib import Path
from datetime import datetime
from concurrent.futures import ThreadPoolExecutor
from unittest.mock import patch
from PySide6.QtWidgets import QFileDialog
from decklauncher.model import Store,Library,entry,backup_name
from decklauncher.dialogs import choose_save

class BackupNameTests(unittest.TestCase):
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);self.store=Store(self.root/'data');self.lib=Library();self.lib.entries.append(entry('처음'))
    def tearDown(self):self.tmp.cleanup()
    def test_01_timestamp(self):self.assertEqual(backup_name(datetime(2026,10,14,14,15,30)),'DeckLauncher-backup-20261014-141530.zip')
    def test_02_same_second_backups_preserve_first(self):
        path=self.root/backup_name(datetime(2026,10,14,14,15,30));first=Path(self.store.backup(path,self.lib));before=first.read_bytes();self.lib.entries[0]['Name']='새 내용';second=Path(self.store.backup(path,self.lib))
        self.assertEqual(second.name,'DeckLauncher-backup-20261014-141530_2.zip');self.assertEqual(first.read_bytes(),before)
        with zipfile.ZipFile(second) as z:self.assertEqual(json.loads(z.read('library.json'))['Entries'][0]['Name'],'새 내용')
    def test_03_skip_existing_number_and_directory(self):
        path=self.root/'backup.zip';self.store.backup(path,self.lib);(self.root/'backup_2.zip').mkdir();self.assertEqual(Path(self.store.backup(path,self.lib)).name,'backup_3.zip')
    def test_04_concurrent_creation_does_not_overwrite(self):
        path=self.root/'parallel.zip'
        with ThreadPoolExecutor(2) as pool:paths=list(pool.map(lambda _:self.store.backup(path,self.lib),range(2)))
        self.assertEqual(len(set(paths)),2)
        for p in paths:
            with zipfile.ZipFile(p) as z:self.assertIsNone(z.testzip())
    def test_05_save_dialog_does_not_ask_to_overwrite(self):
        with patch.object(QFileDialog,'getSaveFileName',return_value=('', '')) as chooser:
            choose_save(None,'백업','sample.zip','ZIP (*.zip)');self.assertTrue(chooser.call_args.kwargs['options'] & QFileDialog.DontConfirmOverwrite)
