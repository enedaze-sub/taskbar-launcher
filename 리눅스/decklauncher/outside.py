"""Optional X11 raw-button observer; never grabs, blocks or logs input.

Under XWayland, focus-loss still handles clicks on native Wayland windows.
Pure Wayland uses focus-loss only because global pointer access is restricted.
"""
import ctypes as C
import ctypes.util
import os
import sys
from PySide6.QtCore import QObject,QTimer
from PySide6.QtGui import QCursor

class Mask(C.Structure):
    _fields_=[('deviceid',C.c_int),('mask_len',C.c_int),('mask',C.POINTER(C.c_ubyte))]
class Cookie(C.Structure):
    _fields_=[('type',C.c_int),('serial',C.c_ulong),('send_event',C.c_int),('display',C.c_void_p),
              ('extension',C.c_int),('evtype',C.c_int),('cookie',C.c_uint),('data',C.c_void_p)]
class Event(C.Union):
    _fields_=[('cookie',Cookie),('pad',C.c_long*24)]

class OutsideWatcher(QObject):
    def __init__(self,callback,parent=None):
        super().__init__(parent);self.display=None;self.timer=QTimer(self);self.timer.setInterval(35);self.callback=callback
        if not sys.platform.startswith('linux') or not os.environ.get('DISPLAY') or os.environ.get('QT_QPA_PLATFORM')=='wayland':return
        try:
            self.x=C.CDLL(ctypes.util.find_library('X11') or 'libX11.so.6')
            self.xi=C.CDLL(ctypes.util.find_library('Xi') or 'libXi.so.6')
            self.x.XOpenDisplay.argtypes=[C.c_char_p];self.x.XOpenDisplay.restype=C.c_void_p
            self.x.XDefaultRootWindow.argtypes=[C.c_void_p];self.x.XDefaultRootWindow.restype=C.c_ulong
            self.x.XQueryExtension.argtypes=[C.c_void_p,C.c_char_p,C.POINTER(C.c_int),C.POINTER(C.c_int),C.POINTER(C.c_int)]
            self.x.XPending.argtypes=[C.c_void_p];self.x.XNextEvent.argtypes=[C.c_void_p,C.POINTER(Event)]
            self.x.XGetEventData.argtypes=[C.c_void_p,C.POINTER(Cookie)];self.x.XFreeEventData.argtypes=[C.c_void_p,C.POINTER(Cookie)]
            self.x.XFlush.argtypes=[C.c_void_p];self.x.XCloseDisplay.argtypes=[C.c_void_p]
            self.xi.XIQueryVersion.argtypes=[C.c_void_p,C.POINTER(C.c_int),C.POINTER(C.c_int)]
            self.xi.XISelectEvents.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(Mask),C.c_int]
            self.display=self.x.XOpenDisplay(None)
            if not self.display:return
            self.opcode=C.c_int();a=C.c_int();b=C.c_int()
            if not self.x.XQueryExtension(self.display,b'XInputExtension',C.byref(self.opcode),C.byref(a),C.byref(b)):self.close();return
            major,minor=C.c_int(2),C.c_int(0)
            if self.xi.XIQueryVersion(self.display,C.byref(major),C.byref(minor))!=0:self.close();return
            bits=(C.c_ubyte*2)(0,128) # XI_RawButtonPress = 15; all master devices.
            mask=Mask(1,2,bits)
            self.xi.XISelectEvents(self.display,self.x.XDefaultRootWindow(self.display),C.byref(mask),1);self.x.XFlush(self.display)
            self.timer.timeout.connect(self.poll);self.timer.start()
        except (OSError,AttributeError):self.close()
    def poll(self):
        if not self.display:return
        while self.x.XPending(self.display):
            event=Event();self.x.XNextEvent(self.display,C.byref(event));cookie=event.cookie
            if cookie.type==35 and cookie.extension==self.opcode.value:
                if self.x.XGetEventData(self.display,C.byref(cookie)):
                    try:
                        if cookie.evtype==15:self.callback(QCursor.pos())
                    finally:self.x.XFreeEventData(self.display,C.byref(cookie))
    def close(self):
        self.timer.stop()
        if self.display:self.x.XCloseDisplay(self.display);self.display=None
