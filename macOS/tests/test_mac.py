"""Mac command/bundle contracts; native Cocoa integration is tested on a Mac."""
import copy
import os
from pathlib import Path
import plistlib
import tempfile
import unittest
from unittest.mock import patch
from decklauncher.launch import app_values,import_path,applications,command,start
from decklauncher.model import entry,Library,Store
from installer import BUNDLE_ID,MIN_MAC,mac_version,verify_bundle,install

class MacLaunchTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        # Windows fixture paths are not real Windows targets on this host-only QA.
        from decklauncher.model import windows_target
        self.path_patch=patch('decklauncher.launch.windows_target',side_effect=lambda t:False if t.startswith(str(self.root)) else windows_target(t));self.path_patch.start()
        from urllib.parse import urlparse
        self.url_patch=patch('decklauncher.launch.urlparse',side_effect=lambda t:urlparse(t)._replace(scheme='') if os.name=='nt' and t.startswith(str(self.root)) else urlparse(t));self.url_patch.start()
        self.app=self.make_app(self.root/'즐겨 쓰는 앱.app')
    def tearDown(self):self.path_patch.stop();self.url_patch.stop();self.tmp.cleanup()
    def make_app(self,path,fmt=plistlib.FMT_XML,**values):
        (path/'Contents/Resources').mkdir(parents=True);(path/'Contents/Resources/AppIcon.icns').write_bytes(b'icon')
        info=dict(CFBundleName='Sample',CFBundleDisplayName='작업 도구',CFBundlePackageType='APPL',CFBundleIdentifier='com.example.tool',CFBundleIconFile='AppIcon');info.update(values)
        (path/'Contents/Info.plist').write_bytes(plistlib.dumps(info,fmt=fmt));return path
    def test_01_app_display_name_and_icon(self):
        v=app_values(self.app);self.assertEqual(v['Name'],'작업 도구');self.assertTrue(v['Icon'].endswith('AppIcon.icns'));self.assertEqual(v['BundleId'],'com.example.tool')
    def test_02_binary_plist_supported(self):self.assertEqual(app_values(self.make_app(self.root/'Binary.app',plistlib.FMT_BINARY))['Name'],'작업 도구')
    def test_03_name_fallback(self):self.assertEqual(app_values(self.make_app(self.root/'Fallback.app',CFBundleDisplayName='',CFBundleName=''))['Name'],'Fallback')
    def test_04_bad_bundle_rejected(self):
        with self.assertRaises(ValueError):app_values(self.root/'Missing.app')
        with self.assertRaises(ValueError):app_values(self.make_app(self.root/'Plugin.app',CFBundlePackageType='BNDL'))
    def test_05_import_app_preserves_parent(self):
        e=import_path(self.app,'folder');self.assertEqual(e['ParentId'],'folder');self.assertEqual(e['Name'],'작업 도구');self.assertEqual(e['BundleId'],'com.example.tool')
    def test_06_discovery_skips_bundled_helpers(self):
        self.make_app(self.app/'Contents/Helpers/Hidden.app');self.make_app(self.root/'Utilities/Other.app',CFBundleDisplayName='다른 도구')
        self.assertEqual(len(applications([self.root,self.root])),2)
    def test_07_app_open_quoted_arguments(self):
        cmd,cwd=command(entry(Target=str(self.app),Arguments='--profile "내 프로필"'));self.assertEqual(cmd,['/usr/bin/open','-a',str(self.app),'--args','--profile','내 프로필']);self.assertIsNone(cwd)
    def test_08_app_open_without_args(self):self.assertEqual(command(entry(Target=str(self.app)))[0],['/usr/bin/open','-a',str(self.app)])
    def test_09_url_uses_launch_services(self):self.assertEqual(command(entry(Target='https://example.com/한글?q=a&b=x'))[0],['/usr/bin/open','https://example.com/한글?q=a&b=x'])
    def test_10_custom_url_scheme(self):self.assertEqual(command(entry(Target='steam://rungameid/123'))[0][0],'/usr/bin/open')
    def test_11_url_arguments_rejected(self):
        with self.assertRaises(ValueError):command(entry(Target='https://example.com',Arguments='--flag'))
    def test_12_document_open(self):
        path=self.root/'내 문서.txt';path.write_text('메모',encoding='utf-8')
        with patch('decklauncher.launch.os.access',return_value=False):self.assertEqual(command(entry(Target=str(path)))[0],['/usr/bin/open',str(path)])
    def test_13_folder_open(self):self.assertEqual(command(entry(Target=str(self.root)))[0],['/usr/bin/open',str(self.root)])
    def test_14_file_url_decode(self):
        # Use a POSIX path that is represented consistently by the host's Path.
        with patch('decklauncher.launch.Path') as paths:
            fake=paths.return_value;fake.suffix='.txt';fake.exists.return_value=True;fake.is_file.return_value=False;fake.absolute.return_value='/tmp/내 메모.txt'
            self.assertEqual(command(entry(Target='file:///tmp/%EB%82%B4%20%EB%A9%94%EB%AA%A8.txt'))[0],['/usr/bin/open','/tmp/내 메모.txt']);paths.assert_called_with('/tmp/내 메모.txt')
    def test_15_remote_file_url_rejected(self):
        with self.assertRaises(ValueError):command(entry(Target='file://foreign/path'))
    def test_16_executable_is_direct_no_shell(self):
        path=self.root/'my-tool';path.write_bytes(b'#!/bin/sh\n')
        with patch('decklauncher.launch.os.access',return_value=True):
            cmd,cwd=command(entry(Target=str(path),Arguments='"one two" ";touch /tmp/never"'));self.assertEqual(cmd,[str(path),'one two',';touch /tmp/never']);self.assertEqual(cwd,str(self.root))
    def test_17_command_name_resolution(self):
        with patch('decklauncher.launch.shutil.which',return_value='/usr/bin/say'):self.assertEqual(command(entry(Target='say',Arguments='"안녕하세요"'))[0],['/usr/bin/say','안녕하세요'])
    def test_18_windows_target_rejected(self):
        with self.assertRaisesRegex(ValueError,'Windows'):command(entry(Target=r'C:\Apps\test.exe'))
    def test_19_linux_bundle_rejected(self):
        for target in ('/opt/test.desktop','/opt/test.AppImage'):
            with self.assertRaisesRegex(ValueError,'리눅스'):command(entry(Target=target))
    def test_20_missing_target_rejected(self):
        with self.assertRaises(FileNotFoundError):command(entry(Target=str(self.root/'Missing.app')))
    def test_21_start_never_uses_shell(self):
        with patch('decklauncher.launch.subprocess.Popen') as popen:start(entry(Target=str(self.app)));popen.assert_called_once_with(['/usr/bin/open','-a',str(self.app)],cwd=None,start_new_session=True)
    def test_22_bad_working_directory(self):
        with self.assertRaises(FileNotFoundError):start(entry(Target=str(self.app),WorkingDirectory=str(self.root/'missing')))
    def test_23_shared_backup_keeps_fields_mac_placement_separate(self):
        lib=Library(dict(Entries=[entry('Windows 앱',Target=r'C:\Apps\x.exe',Note='메모')],LinuxPlacement=dict(Edge='left',Align='center',Offset=50)))
        self.assertEqual(lib.data['LinuxPlacement']['Edge'],'left');self.assertEqual(lib.data['MacPlacement']['Edge'],'auto');self.assertEqual(lib.entries[0]['Note'],'메모')
    def test_24_mac_data_default(self):
        with patch('decklauncher.model.Path.home',return_value=self.root):self.assertEqual(Store().directory,self.root/'Library/Application Support/DeckLauncher')

class MacInstallerTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.app=Path(self.tmp.name)/'DeckLauncher.app'
        (self.app/'Contents/MacOS').mkdir(parents=True);(self.app/'Contents/Resources/Fonts').mkdir(parents=True)
        self.info=dict(CFBundleIdentifier=BUNDLE_ID,CFBundlePackageType='APPL',CFBundleExecutable='DeckLauncher')
        self.plist=self.app/'Contents/Info.plist';self.plist.write_bytes(plistlib.dumps(self.info));(self.app/'Contents/MacOS/DeckLauncher').write_bytes(b'\xcf\xfa\xed\xfe'+b'fixture');(self.app/'Contents/Resources/Fonts/Jua-Regular.ttf').write_bytes(b'fixture')
    def tearDown(self):self.tmp.cleanup()
    def test_01_version_boundary(self):self.assertLess(mac_version('14.3.1'),MIN_MAC);self.assertEqual(mac_version('14.4.0'),MIN_MAC);self.assertGreater(mac_version('26.0'),MIN_MAC)
    def test_02_invalid_version(self):self.assertEqual(mac_version(''),(0,0));self.assertEqual(mac_version('unknown'),(0,0))
    def test_03_mac_header_validated(self):self.assertEqual(verify_bundle(self.app)['CFBundleIdentifier'],BUNDLE_ID)
    def test_04_wrong_bundle_id_rejected(self):
        self.info['CFBundleIdentifier']='another.app';self.plist.write_bytes(plistlib.dumps(self.info))
        with self.assertRaises(RuntimeError):verify_bundle(self.app)
    def test_05_executable_escape_rejected(self):
        self.info['CFBundleExecutable']='../wrong';self.plist.write_bytes(plistlib.dumps(self.info))
        with self.assertRaises(RuntimeError):verify_bundle(self.app)
    def test_06_windows_executable_rejected(self):
        (self.app/'Contents/MacOS/DeckLauncher').write_bytes(b'MZ00')
        with self.assertRaisesRegex(RuntimeError,'Mach-O'):verify_bundle(self.app)
    def test_07_missing_bundled_font(self):
        (self.app/'Contents/Resources/Fonts/Jua-Regular.ttf').unlink()
        with self.assertRaises(RuntimeError):verify_bundle(self.app)
    def test_08_windows_host_cannot_make_fake_app(self):
        with patch('installer.sys.platform','win32'):
            with self.assertRaisesRegex(RuntimeError,'Mac에서'):install()
    def test_09_old_mac_rejected_before_changes(self):
        with patch('installer.sys.platform','darwin'),patch('installer.platform.mac_ver',return_value=('14.3',('', '', ''),'arm64')):
            with self.assertRaisesRegex(RuntimeError,'14.4'):install()
