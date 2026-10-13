#!/usr/bin/env python3
import os
import sys

# Ubuntu GNOME Wayland: use XWayland so the shell can be positioned near the dock.
# The user may override this explicitly with QT_QPA_PLATFORM=wayland.
if sys.platform.startswith('linux') and os.environ.get('DISPLAY'):
    os.environ.setdefault('QT_QPA_PLATFORM','xcb')

from decklauncher.app import main
if __name__=='__main__':raise SystemExit(main())
