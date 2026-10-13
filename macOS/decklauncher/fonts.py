from functools import lru_cache
from PySide6.QtGui import QFontDatabase

@lru_cache(maxsize=1)
def families():return {name.casefold():name for name in QFontDatabase.families()}

def resolved(library,heading=False):
    preferred=library.data['Fonts'].get('Launcher' if heading else 'Interface','')
    return families().get(preferred.casefold(),'Jua' if heading else 'Gowun Dodum')
