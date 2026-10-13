# Third-party components

The launcher source and project artwork follow the original project's LICENSE (CC BY-NC 4.0, enedaze-sub). That license does not replace the licenses of the following independent components.

## Qt for Python / PySide6-Essentials / shiboken6 6.12.0

Unmodified macOS universal2 wheels are included in `wheels/`. Qt is loaded dynamically from a Python virtual environment or from separate frameworks/libraries in the generated `.app`. Full launcher source, build/installation scripts, fixed requirements and original wheels are included so users can replace Qt/PySide and rebuild the application. Reverse engineering needed to debug changes to LGPL components is permitted under the applicable LGPL terms.

PySide/Qt components are distributed under their original LGPL/GPL/commercial and third-party licenses, as described in the wheel metadata and upstream license notices. This launcher uses the LGPL option for its dynamically linked Qt components. Original wheel metadata, LGPL/GPL texts, and the upstream Qt for Python third-party attribution page are in `third_party_licenses/`. Installation preserves their `dist-info` and Qt distribution files. LGPL terms apply independently of the launcher's noncommercial source license.

- [Qt for Python 6.12 licenses](https://doc.qt.io/qtforpython-6/licenses.html)
- [Qt 6.12 licensing](https://doc.qt.io/qt-6/licensing.html)
- [LGPL obligations](https://www.qt.io/development/open-source-lgpl-obligations)
- [PySide 6.12.0 source](https://download.qt.io/official_releases/QtForPython/pyside6/PySide6-6.12.0-src/)
- [Qt 6.12.0 source repositories](https://code.qt.io/cgit/qt/qtbase.git/tree/?h=v6.12.0)
- [PyPI PySide6-Essentials 6.12.0](https://pypi.org/project/PySide6-Essentials/6.12.0/)
- [PyPI shiboken6 6.12.0](https://pypi.org/project/shiboken6/6.12.0/)

## Fonts

Jua and Gowun Dodum: SIL Open Font License 1.1. The fonts and original license are in `Fonts/`. Fonts installed on the user's computer are not bundled or redistributed.

- [Jua](https://fonts.google.com/specimen/Jua)
- [Gowun Dodum](https://fonts.google.com/specimen/Gowun+Dodum)

Mac native app/file icons come from installed `.app` bundles or the OS file icon provider. No third-party app icon collection is bundled.

## Build tools and pure Python dependencies

PyInstaller 6.22.3 is GPLv2-or-later with its original exception allowing distribution of programs built with its bootloader. pyinstaller-hooks-contrib 2026.8 uses GPL-2.0-or-later for build hooks and Apache-2.0 for runtime hooks. altgraph 0.17.5 and macholib 1.16.4 use MIT; packaging 26.3 uses Apache-2.0 OR BSD-2-Clause; setuptools 84.0.0 uses MIT. The unmodified wheels, original license files and wheel metadata are included. Build tools are installed into a private build environment on the Mac; they are not a replacement for Apple's code signing or notarization.

See each package directory in `third_party_licenses/` and `wheels-SHA256.json` for original license notices, version, upstream download URL and checksum. No Python interpreter or Apple Command Line Tools is redistributed in this installer kit; the destination Mac supplies them for the build. A generated `.app` embeds that Mac's Python runtime subject to its original PSF and third-party licenses.
