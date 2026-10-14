# タスクバーランチャー (DeckLauncher)

[한국어](README.md) · [English](README.en.md) · **日本語** · [简体中文](README.zh-CN.md)

タスクバーのアイコンをクリックすると、扇形または選択したデザインで開くアプリランチャーです。よく使うアプリをフォルダーにまとめ、アイコン・メモ・背景画像・色・フォントを好みに合わせて変更できます。Windows版と、Qt/Pythonを使用したLinux・macOS版があります。

## テーマ別プレビュー

**配色とUIデザインは別々に選択できます。** 以下は、各デザインの代表的な配色を使って実際のWindowsランチャーを描画した画像です。クリックすると拡大できます。アプリのUIは現在韓国語です。上の言語リンクはREADMEの翻訳を切り替えます。

| 基本の扇形 · ミントナイト | にゃんこポケット · ピーチクリーム |
|:---:|:---:|
| ![基本の扇形 · ミントナイト](미리보기/themes/classic.png) | ![にゃんこポケット · ピーチクリーム](미리보기/themes/cat.png) |
| **サイバーHUD · ネオンミント** | **ダイアリースクラップ · クリームペーパー** |
| ![サイバーHUD · ネオンミント](미리보기/themes/neon.png) | ![ダイアリースクラップ · クリームペーパー](미리보기/themes/paper.png) |
| **グラスドック · クラウドホワイト** | **星空の軌道 · スターライトパープル** |
| ![グラスドック · クラウドホワイト](미리보기/themes/modern.png) | ![星空の軌道 · スターライトパープル](미리보기/themes/space.png) |

![ランチャー設定](미리보기/런처설정.png)

## 紹介動画

[![紹介動画のプレビュー](미리보기/소개영상.gif)](https://enedaze-sub.github.io/taskbar-launcher/)

ランチャーとフォルダーの展開、ショートカットの管理、配色・UIデザインの選択、背景の変更を紹介します。ランチャーの外側はぼかしています。

## インストール

**Windowsの必要環境:** Windows 10/11 (x64)、[.NET 6 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/6.0)。

### 方法1 · ZIPをダウンロード

1. [最新リリース](https://github.com/enedaze-sub/taskbar-launcher/releases/latest)から `taskbar-launcher-v8.3.zip` をダウンロードして展開します。
2. `실행파일\DeckLauncher.exe` を実行します。
3. タスクバーのアイコンを右クリックし、**タスクバーにピン留めする**を選択します。

### 方法2 · gitでインストール

```bash
git clone https://github.com/enedaze-sub/taskbar-launcher.git
```

`taskbar-launcher\실행파일\DeckLauncher.exe` を実行します。更新は `git pull` で取得できます。登録情報は `실행파일\Data` に保存され、gitによって変更されません。

更新時はランチャーを終了してから、新しいファイルを既存のインストール先に上書きしてください。配布ファイルには個人用のDataフォルダーが含まれないため、登録済みのショートカットは保持されます。

## 文言・背景・透明度 (v8.3)

- **… → 中央の文言を編集（중앙 문구 수정）:** 「自分のショートカット」のタイトルと中央の案内文を自由に設定します。
- **カスタマイズ（꾸미기）→ 全体の背景画像（전체 배경 이미지）:** 画像の位置・拡大率と、ランチャー全体の透明度 **0～100%** を調整します。
- **タイルを右クリック → このタイルの背景画像（이 칸의 배경 이미지）:** 画像の位置とタイルの透明度 **-100～100%** を調整します。0%は元のデザイン、負の値は下地を濃くし、100%は完全に透明にします。
- 初期設定ではタイル全体をまとめて調整します。**背景・アイコン・文字の透明度を個別に調整（배경 · 아이콘 · 글자 투명도를 따로 조절）**を有効にすると、アイコンと文字をそれぞれ **0～100%** に設定できます。例えば背景100%、アイコン・文字0%なら、背景だけを隠してアイコンと文字を鮮明に表示します。ランチャー全体の透明度はこれらの値にも適用されます。
- ランチャー全体を100%で非表示にした場合、タスクバー/Dockのアイコンをクリックすると設定画面が開き、再調整できます。
- **カスタマイズ → タイル背景の一括設定（칸 배경 일괄 설정）:** 全タイル／現在のフォルダー／現在のフォルダーとサブフォルダーを選択します。画像・位置と透明度は別々に適用できます。
- バックアップ名には日付・時刻・秒を含めます。同名のファイルがある場合は `_2`、`_3` などを付け、既存のバックアップを保持します。

自動検証に合格: Windows **217件**・Linux **132件**・macOS **150件**。[v8.3変更案内](docs/v8.3.md)・[背景・一括設定・個別透明度の使い方](docs/v8.2.md)（韓国語）。

![Windowsの個別透明度設定](미리보기/독립-투명도.png)

## 使い方の概要

- **アプリ登録:** 実行ファイル（.exe）やショートカットをランチャー設定画面にドラッグします。タスクバーにのみ登録されたアプリは、**直接登録（직접 등록）**でショートカットのプロパティにある「リンク先」を貼り付けてください。
- **フォルダー:** 用途別にまとめ、ドラッグでフォルダーに入れたり外へ出したりできます。フォルダーにマウスを重ねると、隣に中のアイコンが表示されます。
- **メモ・アイコン:** マウスを重ねると保存したメモが表示されます。ファイルの標準アイコンを使うか、好きな画像に変更できます。
- **カスタマイズ:** タイル背景の一括設定／配色・UIデザイン／全体の背景画像／フォント。6種類のデザイン、10種類の配色、別途保存できるカスタムテーマがあります。
- **オプション（…）:** 中央の文言編集、ホイールダイヤル、バックアップ・復元、保存フォルダーを開く、終了。初期設定は前へ／次へボタンです。ホイールダイヤルは任意で有効にします。
- **ショートカットキー:** `Home` 最上位へ・`Esc` 親フォルダー／非表示・`PageUp/PageDown` ページ切替・`Ctrl+F` 設定。
- アプリの起動やランチャー外のクリックでランチャーが隠れます。設定画面は通常のアプリと同様に他のウィンドウの背後へ移動できます。

基本的な使い方は [사용안내.txt](사용안내.txt)（韓国語）をご覧ください。現在の透明度の範囲は上記の説明に従ってください。

## Linux・macOS版

Qt/Python版でも、フォルダー、ドラッグ並べ替え、メモ、6種類のデザイン・10種類の配色・カスタムテーマ、全体／タイル別の背景プレビュー、任意のホイールダイヤル、中央の文言編集、透明度、バックアップを利用できます。

| 対象 | インストールパッケージ | 使い方 |
|---|---|---|
| UbuntuデスクトップPC、x86_64 | [Linuxをダウンロード](https://github.com/enedaze-sub/taskbar-launcher/releases/download/v8.3/DeckLauncher-Linux-x86_64-v8.3.tar.gz) | [Linuxガイド](리눅스/README.md) |
| macOS 14.4以降、Apple Silicon/Intel | [macOSをダウンロード](https://github.com/enedaze-sub/taskbar-launcher/releases/download/v8.3/DeckLauncher-macOS-v8.3.tar.gz) | [Macガイド](macOS/README.md) |

[v8.3リリース](https://github.com/enedaze-sub/taskbar-launcher/releases/tag/v8.3)に3つのOS向けパッケージが含まれます。**Linux・macOS版はプレビュー版です。** 自動検証とQtによる描画はWindows上で確認しました。実際のUbuntu/MacへのインストールとDockの動作は実機未検証です。

Linuxではパッケージを展開し、`bash install.sh --with-system-deps` を実行します。

Macでの初回ビルドにはPython 3.10～3.15とApple Command Line Toolsが必要です。`install.command` を実行すると `~/Applications/DeckLauncher.app` が作成されます。完成したアプリはPythonを別途インストールせずに実行できます。Apple Developer IDによる署名・公証は含まれません。

Windows/Linuxのバックアップからフォルダー・メモ・画像を取り込めます。他のOS用の実行先は、現在のOSのアプリに再設定してください。Linux/macOSの配置は、Dockアイコンの正確な座標ではなく、カーソルとデスクトップの作業領域を基準にします。

### macOSのUIデザイン6種類

![macOSのUIデザイン6種類](macOS/screenshots/designs.png)

[配色10種類を見る](macOS/screenshots/colors.png) · [Mac設定画面](macOS/screenshots/settings.png) · [LinuxのUIデザイン6種類](리눅스/screenshots/designs.png)

上のMac/Linux画像は各版のソースをQtで描画したプレビューです。標準アプリアイコンは実際にインストールされているアプリとOSによって変わります。

## ソースからビルド

.NET 6 SDKをインストールした環境で、Windows PowerShellから実行してください。

```powershell
.\소스\빌드.ps1            # 実行中のランチャーを先に終了
.\소스\빌드.ps1 D:\out     # 別のフォルダーにビルド
```

自動検証: `DeckLauncher.exe --self-test <結果フォルダー>` · 画面プレビュー: `DeckLauncher.exe --preview <フォルダー>` · メモリ測定: `DeckLauncher.exe --memtest <Dataのコピー> <結果フォルダー> [software]`

## ブランチとバージョン

| 名前 | 内容 |
|---|---|
| `main` | 最新のソースと実行ファイル |
| `backup/v1` … `backup/vN` | 各バージョンのバックアップブランチ（タグ `v1` … `vN` と同じコミット） |
| Releases | バージョンごとのインストールパッケージ（個人データを含まない） |

## ライセンス

[![CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc/4.0/)

このプロジェクトには [クリエイティブ・コモンズ 表示–非営利 4.0 国際ライセンス（CC BY-NC 4.0）](https://creativecommons.org/licenses/by-nc/4.0/deed.ja)が適用されます。作者（enedaze）と出典を明示すれば複製・配布・改変できますが、**営利目的では利用できません。** 詳細は [LICENSE](LICENSE) をご覧ください。

### 同梱フォント

同梱の [Jua（주아）](https://fonts.google.com/specimen/Jua)・[Gowun Dodum（고운돋움）](https://fonts.google.com/specimen/Gowun+Dodum) はSIL Open Font Licenseに従います（`소스/Fonts/OFL.txt`）。카페24 써라운드などPCにインストールされたフォントは同梱されておらず、利用できない場合は同梱フォントに切り替わります。
