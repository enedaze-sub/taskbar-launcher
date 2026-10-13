import copy
import json
import os
from pathlib import Path,PurePosixPath
import tempfile
import unittest
from unittest.mock import patch
import zipfile
from urllib.parse import urlparse
from decklauncher.model import Library,Store,entry,background,COLORS,DESIGNS,windows_target
from decklauncher.launch import app_values,import_path,command,start,applications


class ModelTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.path=Path(self.tmp.name);self.store=Store(self.path/'data')
        self.lib=Library();self.a=entry('폴더',True);self.b=entry('하위 폴더',True,parent=self.a['Id'])
        self.c=entry('앱',parent=self.b['Id'],Target='/usr/bin/test',Note='간단한 메모');self.d=entry('밖의 앱')
        self.lib.entries.extend([self.a,self.b,self.c,self.d])
    def tearDown(self):self.tmp.cleanup()
    def test_01_default_wheel_off(self):self.assertFalse(self.lib.data['WheelDialEnabled'])
    def test_02_six_designs_ten_palettes(self):self.assertEqual(len(DESIGNS),6);self.assertEqual(len(COLORS),10)
    def test_03_removed_palettes(self):self.assertFalse({'오션 블루','글래스 스카이'} & {c[0] for c in COLORS})
    def test_04_cycle_rejected(self):self.assertFalse(self.lib.move([self.a['Id']],self.b['Id']))
    def test_05_self_parent_rejected(self):self.assertFalse(self.lib.move([self.a['Id']],self.a['Id']))
    def test_06_nonfolder_parent_rejected(self):self.assertFalse(self.lib.move([self.a['Id']],self.c['Id']))
    def test_07_out_of_folder(self):self.assertTrue(self.lib.move([self.c['Id']],None));self.assertIsNone(self.c['ParentId'])
    def test_08_drop_in_folder(self):self.assertTrue(self.lib.move([self.d['Id']],self.a['Id']));self.assertEqual(self.d['ParentId'],self.a['Id'])
    def test_09_reorder_before(self):self.assertTrue(self.lib.move([self.d['Id']],None,self.a['Id']));self.assertEqual(self.lib.children()[0]['Id'],self.d['Id'])
    def test_10_reorder_after(self):self.assertTrue(self.lib.move([self.a['Id']],None,self.d['Id'],True));self.assertEqual(self.lib.children()[-1]['Id'],self.a['Id'])
    def test_11_multiselect_order(self):
        x=entry('새 앱');self.lib.entries.append(x);self.lib.move([x['Id'],self.d['Id']],self.a['Id'])
        self.assertEqual([e['Name'] for e in self.lib.children(self.a['Id'])],['하위 폴더','밖의 앱','새 앱'])
    def test_12_invalid_move_unchanged(self):
        before=copy.deepcopy(self.lib.data);self.assertFalse(self.lib.move([self.d['Id'],'missing'],None));self.assertEqual(before,self.lib.data)
    def test_13_recursive_remove(self):self.lib.remove([self.a['Id']]);self.assertEqual(self.lib.entries,[self.d])
    def test_14_trail(self):self.assertEqual(self.lib.trail(self.b['Id']),'라이브러리 / 폴더 / 하위 폴더')
    def test_15_atomic_save_roundtrip(self):self.store.save(self.lib);self.assertEqual(self.store.load().data,self.lib.data)
    def test_16_previous_save_kept(self):
        self.store.save(self.lib);self.c['Name']='바뀜';self.store.save(self.lib)
        self.assertEqual(json.loads(Path(str(self.store.file)+'.bak').read_text(encoding='utf-8'))['Entries'][2]['Name'],'앱')
    def test_17_bad_save_does_not_replace(self):
        self.store.save(self.lib);before=self.store.file.read_bytes();self.c['ParentId']=self.c['Id']
        with self.assertRaises(ValueError):self.store.save(self.lib)
        self.assertEqual(before,self.store.file.read_bytes())
    def test_18_invalid_json_kept(self):
        self.store.directory.mkdir();self.store.file.write_text('{broken',encoding='utf-8')
        with self.assertRaises(json.JSONDecodeError):self.store.load()
        self.assertEqual(self.store.file.read_text(),'{broken')
    def test_19_duplicate_id(self):
        self.lib.entries.append(copy.deepcopy(self.c))
        with self.assertRaises(ValueError):self.lib.validate()
    def test_20_nonfinite_background(self):
        self.c['Background']['Zoom']=float('nan')
        with self.assertRaises(ValueError):self.lib.validate()
    def test_21_image_path_escape_rejected(self):
        for name in ('../bad','..\\bad','C:bad','/abs'):
            self.c['IconFile']=name
            with self.assertRaises(ValueError):self.lib.validate()
    def test_22_backup_restore_with_images(self):
        image=self.path/'picture.png';image.write_bytes(b'example image');name=self.store.copy_image(image)
        self.c['IconFile']=name;self.c['Background']['ImageFile']=name;self.lib.data['Background']['ImageFile']=name
        archive=self.path/'backup.zip';self.store.backup(archive,self.lib);other=Store(self.path/'other')
        restored=other.restore(archive);self.assertEqual(restored.data,self.lib.data);self.assertEqual((other.icons/name).read_bytes(),b'example image')
    def test_23_missing_image_backup_does_not_restore(self):
        self.store.save(self.lib);before=self.store.file.read_bytes();raw=copy.deepcopy(self.lib.data);raw['Entries'][2]['IconFile']='missing.png'
        zpath=self.path/'bad.zip'
        with zipfile.ZipFile(zpath,'w') as z:z.writestr('library.json',json.dumps(raw))
        with self.assertRaises(ValueError):self.store.restore(zpath)
        self.assertEqual(before,self.store.file.read_bytes())
    def test_24_zip_traversal_never_extracted(self):
        zpath=self.path/'bad.zip'
        with zipfile.ZipFile(zpath,'w') as z:z.writestr('library.json',json.dumps(self.lib.data));z.writestr('../escaped','bad')
        self.store.restore(zpath);self.assertFalse((self.path/'escaped').exists())
    def test_25_windows_backup_fields_preserved(self):
        self.c.update(Target=r'C:\Users\Person\App.exe',Arguments='--processStart App.exe',IconFile=None,WorkingDirectory=r'C:\Users\Person',Note='메모 유지')
        before=copy.deepcopy(self.lib.entries);restored=Library(self.lib.data);self.assertEqual(restored.entries,before)
    def test_26_custom_theme_and_fonts_preserved(self):
        self.lib.data['CustomThemes']=[dict(Id='custom',Name='내 색',Settings=copy.deepcopy(self.lib.theme))]
        self.lib.data['Fonts']=dict(Launcher='Jua',Interface='Noto Sans KR');self.store.save(self.lib);self.assertEqual(self.store.load().data,self.lib.data)
    def test_27_invalid_palette(self):
        self.lib.theme['Text']='red'
        with self.assertRaises(ValueError):self.lib.validate()
    def test_28_windows_paths_detected(self):
        self.assertTrue(windows_target(r'C:\A\App.exe'));self.assertTrue(windows_target('App.lnk'));self.assertFalse(windows_target('/usr/bin/firefox'))
