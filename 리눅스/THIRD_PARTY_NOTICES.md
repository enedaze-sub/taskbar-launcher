# Third-party components

The launcher source and project artwork follow the original project's LICENSE (CC BY-NC 4.0, enedaze-sub). That license does not replace the licenses of the following independent components.

## Qt for Python / PySide6-Essentials / shiboken6 6.12.0

Unmodified Linux wheels are included in `wheels/`. Qt is loaded dynamically from an ordinary Python virtual environment. Users can replace the wheels or installed Qt bindings. Reverse engineering needed to debug changes to LGPL components is permitted under the applicable LGPL terms.

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

Linux native app/file icons come from the user's installed desktop icon theme, desktop entry, or file icon provider. No third-party app icon collection is bundled.
