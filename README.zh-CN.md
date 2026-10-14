# 任务栏启动器 (DeckLauncher)

[한국어](README.md) · [English](README.en.md) · [日本語](README.ja.md) · **简体中文**

点击任务栏图标，即可展开扇形或所选设计的应用启动器。将常用程序按文件夹归类，并自定义图标、备注、背景图片、颜色和字体。提供Windows版本，以及基于Qt/Python的Linux和macOS版本。

## 主题预览

**配色与UI设计可以分别选择。** 以下图片由实际Windows启动器渲染，每种设计使用一套代表配色。点击图片可放大查看。应用界面目前为韩语；上方的语言链接用于切换README译文。

| 经典扇形 · 薄荷之夜 | 猫咪口袋 · 蜜桃奶油 |
|:---:|:---:|
| ![经典扇形 · 薄荷之夜](미리보기/themes/classic.png) | ![猫咪口袋 · 蜜桃奶油](미리보기/themes/cat.png) |
| **赛博HUD · 霓虹薄荷** | **手帐拼贴 · 奶油纸张** |
| ![赛博HUD · 霓虹薄荷](미리보기/themes/neon.png) | ![手帐拼贴 · 奶油纸张](미리보기/themes/paper.png) |
| **玻璃Dock · 云朵白** | **星光轨道 · 星光紫** |
| ![玻璃Dock · 云朵白](미리보기/themes/modern.png) | ![星光轨道 · 星光紫](미리보기/themes/space.png) |

![启动器设置](미리보기/런처설정.png)

## 介绍视频

[![介绍视频预览](미리보기/소개영상.gif)](https://enedaze-sub.github.io/taskbar-launcher/)

视频展示启动器展开、文件夹展开、快捷方式管理、配色/UI设计选择和背景设置。启动器外的桌面已做模糊处理。

## 安装

**Windows运行要求：** Windows 10/11（x64）、[.NET 6 Desktop Runtime（x64）](https://dotnet.microsoft.com/download/dotnet/6.0)。

### 方式1 · 下载ZIP

1. 从[最新发布](https://github.com/enedaze-sub/taskbar-launcher/releases/latest)下载 `taskbar-launcher-v8.3.zip` 并解压。
2. 运行 `실행파일\DeckLauncher.exe`。
3. 右键点击任务栏图标，选择**固定到任务栏**。

### 方式2 · 使用git安装

```bash
git clone https://github.com/enedaze-sub/taskbar-launcher.git
```

运行 `taskbar-launcher\실행파일\DeckLauncher.exe`。使用 `git pull` 获取更新。快捷方式数据保存在 `실행파일\Data` 中，git不会修改该目录。

更新时请先退出启动器，再将新文件解压并覆盖到原安装目录。发布包不包含个人Data文件夹，因此现有快捷方式会保留。

## 文字、背景与透明度 (v8.3)

- **… → 修改中央文字（중앙 문구 수정）：** 自定义“我的快捷方式”标题和中央提示文字。
- **个性化（꾸미기）→ 启动器背景图片（전체 배경 이미지）：** 调整图片位置、缩放和启动器整体透明度，范围为 **0～100%**。
- **右键点击格子 → 此格子的背景图片（이 칸의 배경 이미지）：** 调整图片位置和格子透明度，范围为 **-100～100%**。0%保留原设计，负值使底色更厚实，100%为完全透明。
- 默认整体调节格子的透明度。勾选**分别调节背景、图标和文字的透明度（배경 · 아이콘 · 글자 투명도를 따로 조절）**后，图标和文字可分别设置为 **0～100%**。例如背景100%、图标和文字0%，只隐藏格子背景，图标和文字仍清晰显示。启动器整体透明度仍会叠加应用于这些图层。
- 如果将启动器整体设为100%透明，点击任务栏/Dock图标会打开设置窗口，可再次调整。
- **个性化 → 批量设置格子背景（칸 배경 일괄 설정）：** 选择全部格子、当前文件夹，或当前文件夹及其子文件夹。图片/位置与透明度可分别应用。
- 备份文件名包含日期、时间和秒。如果文件名重复，会追加 `_2`、`_3` 等编号，保留之前的文件。

自动检查全部通过：Windows **217项**、Linux **132项**、macOS **150项**。[v8.3更新说明](docs/v8.3.md) · [背景、批量设置与独立透明度指南](docs/v8.2.md)（韩语）。

![Windows独立透明度设置](미리보기/독립-투명도.png)

## 使用概要

- **注册应用：** 将可执行文件（.exe）或快捷方式拖入启动器设置。仅固定在任务栏上的应用，可在**直接注册（직접 등록）**中粘贴快捷方式属性里的“目标”内容。
- **文件夹：** 按用途归类，通过拖拽移入或移出文件夹。鼠标悬停在文件夹上时，其内部图标会在旁边展开。
- **备注和图标：** 悬停可查看已保存的备注。使用文件默认图标，也可换成自选图片。
- **个性化：** 批量格子背景／配色与UI设计／整体背景图片／字体。提供6种设计、10套配色，以及单独保存的自定义主题。
- **选项（…）：** 修改中央文字、滚轮拨盘、备份/恢复、打开数据目录、退出。默认通过上一页/下一页按钮切换；滚轮拨盘需要自行启用。
- **快捷键：** `Home` 返回根目录 · `Esc` 上级文件夹/隐藏 · `PageUp/PageDown` 翻页 · `Ctrl+F` 设置。
- 启动应用或点击启动器外部时，启动器会隐藏。设置窗口可像普通应用一样移到其他窗口后方。

基本操作请参阅 [사용안내.txt](사용안내.txt)（韩语）。当前版本的透明度范围以上方说明为准。

## Linux与macOS版本

Qt/Python版本也支持文件夹、拖拽排序、备注、6种设计、10套配色、自定义主题、整体/单格背景预览、可选滚轮拨盘、中央文字修改、透明度和备份。

| 平台 | 安装包 | 指南 |
|---|---|---|
| Ubuntu普通桌面PC，x86_64 | [下载Linux版](https://github.com/enedaze-sub/taskbar-launcher/releases/download/v8.3/DeckLauncher-Linux-x86_64-v8.3.tar.gz) | [Linux指南](리눅스/README.md) |
| macOS 14.4及以上，Apple Silicon/Intel | [下载macOS版](https://github.com/enedaze-sub/taskbar-launcher/releases/download/v8.3/DeckLauncher-macOS-v8.3.tar.gz) | [Mac指南](macOS/README.md) |

[v8.3发布](https://github.com/enedaze-sub/taskbar-launcher/releases/tag/v8.3)包含三种系统的安装包。**Linux/macOS仍为预览版。** 自动检查和Qt渲染已在Windows上验证，尚未在真实Ubuntu/Mac设备上验证安装和Dock行为。

Linux：解压安装包并运行 `bash install.sh --with-system-deps`。

Mac首次构建需要Python 3.10～3.15和Apple Command Line Tools。运行 `install.command`，生成 `~/Applications/DeckLauncher.app`。生成后的应用无需单独安装Python即可运行。不包含Apple Developer ID签名或公证。

可以导入Windows/Linux备份中的文件夹、备注和图片。来自其他操作系统的启动目标需要重新关联到当前系统的应用。Linux/macOS的位置以鼠标和桌面可用区域为基准，而非Dock图标的精确坐标。

### macOS的6种UI设计

![macOS的6种UI设计](macOS/screenshots/designs.png)

[查看10套配色](macOS/screenshots/colors.png) · [Mac设置窗口](macOS/screenshots/settings.png) · [Linux的6种UI设计](리눅스/screenshots/designs.png)

上方的Mac/Linux图片由对应版本源码通过Qt渲染。默认应用图标取决于实际安装的应用和操作系统。

## 从源码构建

安装.NET 6 SDK后，在Windows PowerShell中运行：

```powershell
.\소스\빌드.ps1            # 先退出正在运行的启动器
.\소스\빌드.ps1 D:\out     # 构建到其他目录
```

自动检查: `DeckLauncher.exe --self-test <结果目录>` · 界面预览: `DeckLauncher.exe --preview <目录>` · 内存测量: `DeckLauncher.exe --memtest <Data副本> <结果目录> [software]`

## 分支与版本

| 名称 | 内容 |
|---|---|
| `main` | 最新源码和可执行文件 |
| `backup/v1` … `backup/vN` | 各版本的备份分支（与标签 `v1` … `vN` 对应的提交相同） |
| Releases | 各版本安装包（不包含个人数据） |

## 许可证

[![CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc/4.0/)

本项目采用[知识共享署名–非商业性使用4.0国际许可协议（CC BY-NC 4.0）](https://creativecommons.org/licenses/by-nc/4.0/deed.zh-hans)。注明作者（enedaze）和来源后，可以复制、分发和修改，但**不得用于商业目的**。详情请参阅 [LICENSE](LICENSE)。

### 附带字体

附带的 [Jua（주아）](https://fonts.google.com/specimen/Jua) 和 [Gowun Dodum（고운돋움）](https://fonts.google.com/specimen/Gowun+Dodum) 遵循SIL Open Font License（`소스/Fonts/OFL.txt`）。카페24 써라운드等电脑上已安装的字体不随包提供；如果不可用，将使用附带字体。
