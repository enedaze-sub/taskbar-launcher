# 작업표시줄 런처 (DeckLauncher)

**한국어** · [English](README.en.md) · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)

작업표시줄 아이콘을 누르면 부채꼴 또는 선택한 디자인으로 펼쳐지는 앱 런처입니다. 자주 쓰는 프로그램을 폴더로 묶고, 아이콘·메모·배경 이미지·색상·글꼴을 내 취향으로 꾸밀 수 있습니다. Windows 버전과 Qt/Python 기반 Linux·macOS 버전을 제공합니다.

## 테마별 미리보기

**색상과 UI 디자인은 각각 선택할 수 있습니다.** 아래는 실제 Windows 런처를 디자인별 대표색으로 렌더링한 모습입니다. 각 이미지를 누르면 크게 볼 수 있습니다. 앱 화면의 언어는 현재 한국어이며, 상단 언어 선택은 README 번역본입니다.

| 기본 부채꼴 · 민트 나이트 | 냥냥 포켓 · 피치 크림 |
|:---:|:---:|
| ![기본 부채꼴 · 민트 나이트](미리보기/themes/classic.png) | ![냥냥 포켓 · 피치 크림](미리보기/themes/cat.png) |
| **사이버 HUD · 네온 민트** | **다이어리 스크랩 · 크림 페이퍼** |
| ![사이버 HUD · 네온 민트](미리보기/themes/neon.png) | ![다이어리 스크랩 · 크림 페이퍼](미리보기/themes/paper.png) |
| **글래스 도크 · 클라우드 화이트** | **별빛 궤도 · 별빛 보라** |
| ![글래스 도크 · 클라우드 화이트](미리보기/themes/modern.png) | ![별빛 궤도 · 별빛 보라](미리보기/themes/space.png) |

![런처 설정](미리보기/런처설정.png)

## 소개 영상

[![소개 영상 미리보기](미리보기/소개영상.gif)](https://enedaze-sub.github.io/taskbar-launcher/)

런처 펼치기 · 폴더 펼침 · 런처 설정 · 색상/UI 디자인 · 배경 꾸미기를 보여 줍니다. 런처 바깥 화면은 블러 처리했습니다.

## 설치

**Windows 필요 환경:** Windows 10/11 (x64), [.NET 6 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/6.0).

### 방법 1 · ZIP 내려받기

1. [최신 릴리즈](https://github.com/enedaze-sub/taskbar-launcher/releases/latest)에서 [`taskbar-launcher-v8.4.3.zip`](https://github.com/enedaze-sub/taskbar-launcher/releases/download/v8.4.3/taskbar-launcher-v8.4.3.zip)을 내려받아 압축을 풉니다.
2. `실행파일\DeckLauncher.exe`를 실행합니다.
3. 작업표시줄 아이콘을 우클릭 → **작업 표시줄에 고정**을 선택합니다.

릴리즈의 **Assets**에서 **`taskbar-launcher-v8.4.3.zip`**을 선택하세요. 이 파일은 Windows 실행파일·소스·번역 README가 포함된 배포 ZIP입니다.

### 방법 2 · git으로 설치

```bash
git clone https://github.com/enedaze-sub/taskbar-launcher.git
```

`taskbar-launcher\실행파일\DeckLauncher.exe`를 실행합니다. 새 버전은 `git pull`로 받습니다. 등록 정보는 `실행파일\Data`에 저장되며 git이 건드리지 않습니다.

업데이트할 때는 런처를 종료한 뒤 기존 폴더에 압축 내용을 덮어쓰세요. 배포 파일에는 개인 Data 폴더가 포함되지 않아 기존 등록 정보는 유지됩니다.

## 문구 · 배경 · 투명도 (v8.3)

- **꾸미기 → 설정창 색상:** 기본은 런처 색상과 연동됩니다. **런처와 설정창 색상을 따로 사용**을 켜면 설정창의 바탕·패널·강조·글자 색을 별도로 고를 수 있습니다. 다시 연동해도 선택했던 색은 보관됩니다.
- 폴더 개수 숫자는 강조색에 따라 검정 또는 흰색으로 표시하여 밝은 색에서도 읽기 쉽게 개선했습니다.
- **… → 중앙 문구 수정:** ‘나의 바로가기’ 제목과 중앙 안내를 직접 작성합니다.
- **꾸미기 → 전체 배경 이미지:** 이미지 위치·확대와 전체 런처 투명도 **0~100%**를 조절합니다.
- **칸 우클릭 → 이 칸의 배경 이미지:** 이미지 위치와 칸 투명도 **-100~100%**를 조절합니다. 0%는 원래 디자인, 음수는 바탕을 더 진하게, 100%는 완전히 투명하게 만듭니다.
- 기본은 칸 전체를 함께 조절합니다. **배경 · 아이콘 · 글자 투명도를 따로 조절**을 켜면 아이콘과 글자를 각각 **0~100%**로 조절합니다. 예: 배경 100%, 아이콘·글자 0%는 칸 배경만 숨기고 아이콘·글자는 선명하게 표시합니다. 전체 런처 투명도는 이 값들 위에도 함께 적용됩니다.
- 전체 런처를 100%로 숨긴 경우 작업표시줄/Dock 아이콘을 한 번 더 누르면 설정창이 열려 다시 조절할 수 있습니다.
- **꾸미기 → 칸 배경 일괄 설정:** 전체 / 현재 폴더 / 하위 폴더 포함 범위를 선택합니다. 이미지·위치와 투명도를 각각 적용할 수 있습니다.
- 백업 이름에 날짜·시간·초를 포함하며, 같은 이름이 있으면 `_2`, `_3`을 붙여 기존 파일을 보존합니다.

자동 검사 Windows **271개** · Linux **141개** · macOS **159개** 통과. [v8.3 변경 안내](docs/v8.3.md) · [배경·일괄·독립 투명도 사용 안내](docs/v8.2.md).

![Windows 별도 투명도 설정](미리보기/독립-투명도.png)

## 사용법 요약

- **Windows 작업표시줄:** 런처를 숨기면 실행 중 항목도 사라집니다. 직접 고정한 아이콘은 남습니다. 설정 버튼 옆의 작은 **핀 버튼**을 누르면 지원되는 Windows에서 고정 확인 창을 엽니다. 고정 해제·구버전 Windows는 핀 버튼의 안내 창을 열어 둔 채 작업표시줄 아이콘을 우클릭하세요. 안내 창이 열린 동안에는 자동으로 숨겨지지 않습니다.
- **다시 열기:** 런처를 숨기거나 앱을 실행한 뒤 다시 열면 라이브러리 첫 페이지부터 시작합니다.
- **Windows 다중 모니터:** 클릭한 모니터에서 표시 위치를 먼저 정하고, 표시 중 중복 위치 검색과 메모리 정리로 인한 지연을 줄였습니다.
- **WindowsApps 앱:** Claude·그림판 등 버전 번호가 포함된 경로는 실행할 때 현재 설치된 같은 앱의 경로를 찾습니다. 기존 등록·인수·메모는 유지되며 경로를 매번 수정할 필요가 없습니다.

- **등록:** 실행 파일(.exe)이나 바로가기를 런처 설정창에 끌어다 놓습니다. 작업표시줄에만 있는 앱은 **직접 등록**에서 바로가기 속성의 ‘대상’을 붙여 넣으세요.
- **폴더:** 용도별로 묶고 드래그로 폴더에 넣거나 밖으로 꺼낼 수 있습니다. 폴더에 마우스를 올리면 옆에 내부 아이콘이 펼쳐집니다.
- **메모·아이콘:** 마우스를 올리면 등록한 메모가 보입니다. 파일의 기본 아이콘을 사용하거나 원하는 이미지로 바꿀 수 있습니다.
- **꾸미기:** 칸 배경 일괄 설정 / 색상 · UI 디자인 / 전체 배경 이미지 / 글꼴. 디자인 6종, 색상 10종, 별도로 저장하는 커스텀 테마를 제공합니다.
- **옵션(…):** 중앙 문구 수정, 휠 다이얼, 백업·복원, 저장 폴더 열기, 런처 종료. 기본은 이전/다음 버튼이며, 휠 다이얼은 선택해서 켭니다.
- **단축키:** `Home` 처음으로 · `Esc` 상위 폴더/숨기기 · `PageUp/PageDown` 페이지 · `Ctrl+F` 설정.
- 앱을 실행하거나 런처 바깥을 클릭하면 숨겨집니다. 설정창은 일반 앱처럼 다른 창 뒤로 갈 수 있습니다.

기본 사용법은 [사용안내.txt](사용안내.txt)를 참고하세요. 최신 투명도 범위는 위 설명을 따릅니다.

## Linux · macOS 버전

Qt/Python 버전도 폴더와 드래그 정렬, 메모, 디자인 6종·색상 10종·커스텀 테마, 전체/칸별 배경 미리보기, 선택형 휠 다이얼, 문구 수정, 투명도, 백업을 지원합니다.

| 대상 | 설치 패키지 | 사용안내 |
|---|---|---|
| Ubuntu 일반 PC, x86_64 | [Linux 내려받기](https://github.com/enedaze-sub/taskbar-launcher/releases/download/v8.4/DeckLauncher-Linux-x86_64-v8.4.tar.gz) | [리눅스 안내](리눅스/README.md) |
| macOS 14.4+, Apple Silicon/Intel | [macOS 내려받기](https://github.com/enedaze-sub/taskbar-launcher/releases/download/v8.4/DeckLauncher-macOS-v8.4.tar.gz) | [Mac 안내](macOS/README.md) |

[v8.3 릴리즈](https://github.com/enedaze-sub/taskbar-launcher/releases/tag/v8.4)에 세 버전의 설치 파일이 포함됩니다. **Linux/macOS는 미리보기 버전**입니다. 자동 검사와 Qt 렌더링은 Windows에서 확인했으며, 실제 Ubuntu/Mac 설치와 Dock 동작은 실기 검증 전입니다.

Linux는 패키지를 풀고 `bash install.sh --with-system-deps`를 실행합니다.

Mac은 최초 빌드에 Python 3.10~3.15와 Apple Command Line Tools가 필요합니다. `install.command`로 `~/Applications/DeckLauncher.app`을 만듭니다. 완성된 앱은 Python 설치 없이 실행됩니다. Apple Developer ID 서명·공증은 포함하지 않습니다.

Windows/Linux 백업의 폴더·메모·이미지를 가져올 수 있습니다. 다른 운영체제의 실행 대상은 해당 OS 앱으로 다시 연결해야 합니다. Linux/macOS의 배치는 정확한 Dock 아이콘 좌표 대신 커서와 작업 영역을 기준으로 합니다.

### macOS UI 디자인 6종

![macOS UI 디자인 6종](macOS/screenshots/designs.png)

[색상 테마 10종 보기](macOS/screenshots/colors.png) · [Mac 설정창](macOS/screenshots/settings.png) · [Linux UI 디자인 6종](리눅스/screenshots/designs.png)

위 Mac/Linux 이미지는 해당 버전의 소스를 Qt로 렌더링한 미리보기입니다. 기본 앱 아이콘은 실제 설치된 앱과 OS에 따라 달라집니다.

## 직접 빌드

.NET 6 SDK가 있으면 Windows PowerShell에서 실행합니다.

```powershell
.\소스\빌드.ps1            # 실행 중인 런처를 먼저 종료
.\소스\빌드.ps1 D:\out     # 다른 폴더로 빌드
```

자동 검사: `DeckLauncher.exe --self-test <결과폴더>` · 화면 미리보기: `DeckLauncher.exe --preview <폴더>` · 메모리 측정: `DeckLauncher.exe --memtest <Data 복사본> <결과폴더> [software]`

## 브랜치와 버전

| 이름 | 내용 |
|---|---|
| `main` | 최신 소스·실행파일 |
| `backup/v1` … `backup/vN` | 각 버전 시점의 백업 브랜치 (태그 `v1` … `vN`과 같은 지점) |
| Releases | 버전별 설치 패키지 (개인 데이터 미포함) |

## 라이선스

[![CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc/4.0/)

이 프로젝트는 [크리에이티브 커먼즈 저작자표시-비영리 4.0 국제 라이선스(CC BY-NC 4.0)](https://creativecommons.org/licenses/by-nc/4.0/deed.ko)를 따릅니다. 저작자(enedaze)와 출처를 표시하면 복제·배포·수정할 수 있지만, **영리 목적으로는 이용할 수 없습니다.** 자세한 내용은 [LICENSE](LICENSE)를 보세요.

### 함께 들어 있는 글꼴

동봉 글꼴 [주아](https://fonts.google.com/specimen/Jua)·[고운돋움](https://fonts.google.com/specimen/Gowun+Dodum)은 SIL Open Font License를 따릅니다(`소스/Fonts/OFL.txt`). 카페24 써라운드 등 PC에 설치된 글꼴은 동봉하지 않으며, 없으면 동봉 글꼴로 대체됩니다.
