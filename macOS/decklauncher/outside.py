"""Native Qt application deactivation hides the Mac launcher.

No global mouse hook, Accessibility or Input Monitoring permission is required.
Qt's Cocoa delegate delivers Dock reopens as forced ApplicationActive events.
"""
from PySide6.QtCore import QObject
class OutsideWatcher(QObject):
    def __init__(self,callback,parent=None):super().__init__(parent)
    def close(self):pass
