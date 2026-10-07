# VscodeSelector

[English](README.md)

開いている VS Code のウィンドウをプロジェクト名のボタンとして並べる Windows 用ツールです。ボタンを押すと対応するウィンドウを前面に出します。

| 縦長 | グリッド |
|---|---|
| ![縦長レイアウト](docs/images/screenshot-vertical.png) | ![グリッドレイアウト](docs/images/screenshot-grid.png) |

![横長レイアウト](docs/images/screenshot-horizontal.png)

## 機能

- VS Code / VS Code Insiders / VSCodium / Cursor / Antigravity IDE のウィンドウを検出
- ウィンドウタイトルから取り出したプロジェクト名と、エディション・リモート種別（例 `stable / wsl: ubuntu`）を表示
- プロジェクトごとのアイデンティコンと単語先頭文字の強調で、似た名前も見分けやすい
- 前面にある VS Code のボタンをもう一度押すと最小化（トグル）
- 同名プロジェクトはリモートラベル → エディション → `#N` の順で区別
- 枠なし・リサイズ可能。ドラッグ用バーは短辺側（横長なら左、縦長なら上）に配置
- ウィンドウの位置とサイズを記憶
- キー操作: `Ctrl+1`〜`Ctrl+9` で N 番目、`Enter` でフォーカス中のボタンを押下、`Esc` で自ウィンドウを最小化

## 動作環境

- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## インストール

[`src/VscodeSelector/bin/publish-fd/VscodeSelector.exe`](src/VscodeSelector/bin/publish-fd/VscodeSelector.exe) をダウンロードして実行するだけです。インストーラーはありません。

## ビルド

```sh
dotnet build src/VscodeSelector -c Release
dotnet test tests/VscodeSelector.Tests

# 単一ファイル・フレームワーク依存の exe
dotnet publish src/VscodeSelector -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o src/VscodeSelector/bin/publish-fd
```

## 設定

任意。`%APPDATA%\VsCodeSelector\settings.json`（コメント・末尾カンマ可）。無い・不正なキーは既定値を使います。

| キー | 型 | 既定 | 内容 |
|---|---|---|---|
| `titleSeparators` | 文字列の配列 | `[" - "]` | タイトルの区切り文字 |
| `titlePattern` | 文字列 / null | `null` | 名前付きグループ `project` を含む正規表現 |
| `extraProcessNames` | 文字列の配列 | `[]` | 追加で VS Code とみなす exe 名 |
| `sortOrder` | `"name"` / `"zorder"` | `"name"` | 並び順 |
| `topmost` | bool | `false` | 最前面に固定 |
| `hideOnActivate` | bool | `false` | 前面化後に自ウィンドウを最小化 |

ログは `%LOCALAPPDATA%\VsCodeSelector\logs\` に出力されます。

## ライセンス

[MIT](LICENSE)
