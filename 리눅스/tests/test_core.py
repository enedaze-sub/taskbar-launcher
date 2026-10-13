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
from decklauncher.launch import desktop_values,desktop_command,import_path,command,start,applications
from installer import desktop_text

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

class LaunchTests(unittest.TestCase):
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);self.file=self.root/'app.desktop'
    def tearDown(self):self.tmp.cleanup()
    def desktop(self,exe,extra=''):
        self.file.write_text('[Desktop Entry]\nType=Application\nName=테스트 앱\nIcon=test-icon\nExec='+exe+'\n'+extra,encoding='utf-8');return self.file
    def test_01_name_and_icon(self):
        p=self.desktop('/usr/bin/test');e=import_path(p);self.assertEqual(e['Name'],'테스트 앱');self.assertEqual(e['NativeIcon'],'test-icon')
    def test_02_exec_quotes(self):
        p=self.desktop('"/opt/my app/bin" --name "hello world" %U');cmd,cwd=desktop_command(p);self.assertEqual(cmd,['/opt/my app/bin','--name','hello world'])
    def test_03_exec_icon_name_file_codes(self):
        p=self.desktop('/bin/test %i %c %k %% %f');cmd,cwd=desktop_command(p)
        self.assertEqual(cmd[:4],['/bin/test','--icon','test-icon','테스트 앱']);self.assertEqual(cmd[-1],'%')
    def test_04_unknown_field_code(self):
        p=self.desktop('/bin/test %Z')
        with self.assertRaises(ValueError):desktop_command(p)
    def test_05_extra_args_split(self):
        cmd,cwd=desktop_command(self.desktop('/bin/test'),'--name "with space"');self.assertEqual(cmd[-2:],['--name','with space'])
    def test_06_desktop_path(self):
        cmd,cwd=desktop_command(self.desktop('/bin/test','Path=/tmp\n'));self.assertEqual(cwd,'/tmp')
    def test_07_terminal(self):
        p=self.desktop('/bin/test','Terminal=true\n')
        with patch('decklauncher.launch.shutil.which',return_value='/usr/bin/x-terminal-emulator'):cmd,cwd=desktop_command(p)
        self.assertEqual(cmd[:2],['/usr/bin/x-terminal-emulator','-e'])
    def test_08_localized_name(self):
        p=self.desktop('/bin/test','Name[ko]=한글 이름\n')
        with patch.dict(os.environ,{'LANG':'ko_KR.UTF-8'}):self.assertEqual(desktop_values(p)['Name'],'한글 이름')
    def test_09_uri(self):self.assertEqual(command(entry(Target='https://example.com/a?q=x'))[0],['xdg-open','https://example.com/a?q=x'])
    def test_10_document_open(self):
        p=self.root/'test.txt';p.write_text('hi')
        # Host fixture paths are Windows paths; exercise the Linux-local-file branch.
        with patch('decklauncher.launch.windows_target',return_value=False),patch('decklauncher.launch.urlparse',return_value=urlparse('/fixture')),patch('decklauncher.launch.os.access',return_value=False):self.assertEqual(command(entry(Target=str(p)))[0][0],'xdg-open')
    def test_11_windows_exe_blocked(self):
        with self.assertRaises(ValueError):command(entry(Target=r'C:\Apps\App.exe'))
    def test_12_missing_target(self):
        with self.assertRaises(FileNotFoundError):command(entry(Target='/totally/missing/program'))
    def test_13_no_shell_interpretation(self):
        p=self.desktop('/bin/test')
        with patch('decklauncher.launch.windows_target',return_value=False),patch('decklauncher.launch.urlparse',return_value=urlparse('/fixture')),patch('decklauncher.launch.shutil.which',return_value=None),patch('decklauncher.launch.subprocess.Popen') as popen:
            start(entry(Target=str(p),Arguments='"$(touch bad)" "; rm -rf bad"'))
            args,kwargs=popen.call_args;self.assertEqual(args[0][-2:],['$(touch bad)','; rm -rf bad']);self.assertNotIn('shell',kwargs)
    def test_14_installer_path_quoting(self):
        target=PurePosixPath('/home/test/a b/한글$`"%')
        self.file.write_text(desktop_text(target),encoding='utf-8');cmd,cwd=desktop_command(self.file)
        self.assertEqual(cmd,['/bin/bash',str(target/'run.sh')])
    def test_15_gio_launch_prefers_desktop_integration(self):
        p=self.desktop('/bin/test')
        with patch('decklauncher.launch.windows_target',return_value=False),patch('decklauncher.launch.urlparse',return_value=urlparse('/fixture')),patch('decklauncher.launch.shutil.which',return_value='/usr/bin/gio'):self.assertEqual(command(entry(Target=str(p)))[0],['gio','launch',str(p)])
    def test_16_empty_exec(self):
        p=self.desktop('')
        with self.assertRaises(ValueError):desktop_command(p)
    def test_17_single_quotes_literal_in_desktop_spec(self):
        p=self.desktop('/bin/test "a\'b"');cmd,cwd=desktop_command(p);self.assertEqual(cmd[-1],"a'b")
    def test_18_hidden_user_entry_overrides_system(self):
        user=self.root/'user';system=self.root/'system';(user/'applications').mkdir(parents=True);(system/'applications').mkdir(parents=True)
        (user/'applications/test.desktop').write_text('[Desktop Entry]\nName=User\nExec=/bin/test\nHidden=true\n')
        (system/'applications/test.desktop').write_text('[Desktop Entry]\nName=System\nExec=/bin/test\n')
        with patch.dict(os.environ,{'XDG_DATA_HOME':str(user),'XDG_DATA_DIRS':str(system)}):
            self.assertFalse(any(e['Name']=='System' for e in applications()))
