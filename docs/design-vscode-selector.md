# VS Code Selector 設計書

## 1. 目的とスコープ

Windows で VS Code を複数ウィンドウ開くと、タスクバー上で区別しづらい。本アプリは開いている VS Code ウィンドウを検知し、プロジェクトのフォルダ名をボタンで並べ、押下でそのウィンドウを前面化する。

- G1: Code.exe / Code - Insiders.exe / VSCodium.exe のトップレベルウィンドウを列挙する。
- G2: タイトルからプロジェクト名 (フォルダ名 / .code-workspace 名) を抽出してボタン表示する。
- G3: 押下で前面化する (最小化なら復元)。
- G4: ウィンドウの開閉・タイトル変更に自動追従する。

非スコープは末尾の 1 行リスト (§13) に集約する。

## 2. 技術選定

| 候補 | 長所 | 短所 | 判定 |
|---|---|---|---|
| C# / .NET 8 + WPF + CsWin32 | Win32 を型付きで呼べる。PerMonitorV2 DPI 対応。ロジックを UI から分離して xUnit で試験可能 | .NET ランタイム必須 | 採用 |
| Electron | UI が容易 | Win32 に native addon 必要。常駐メモリ大 | 却下 |
| AutoHotkey | 最短で動く | 型安全性・単体テストが弱く、タイトル解析の保守が難しい | 却下 |
| Rust + windows-rs | 軽量 | UI ライブラリ選定コストが高い | 却下 |

## 3. アーキテクチャ

```
MainWindow(WPF, WrapPanel) <- MainViewModel(ObservableCollection<ProjectEntry>)
   MainViewModel -> IWindowEnumerator / ITitleParser / IWindowActivator / IChangeNotifier
```

Win32 呼び出しは `IWindowEnumerator` / `IWindowActivator` / `IChangeNotifier` の背後に隔離し、`TitleParser`・`Disambiguator`・ViewModel は純粋ロジックとして単体テストする。

ファイル配置 (T006/T007): 分岐ロジック `WindowActivator` (`WindowActivator.cs`) は Win32 呼び出しを `IForegroundApi` (`IForegroundApi.cs`) 経由で行い、fake API で `ActivatorTests.cs` が決定的に検証する (T006)。実 API 実装 `Win32ForegroundApi` は `Win32ForegroundApi.cs` に置き、`ActivatorIntegrationTests.cs` で実機検証する (T007)。`IsWindow` は `IForegroundApi.IsWindow` として公開し、`WindowActivator` が手順 1 の WindowGone 判定に使う。

## 4. インタフェースとデータモデル

```csharp
public enum VsCodeEdition { Stable, Insiders, VSCodium }
public enum RemoteKind { None, Wsl, Ssh, DevContainer, Codespaces, Other }

public readonly record struct RawWindow(
    nint Hwnd, uint ProcessId, string ProcessName /* 昇格で取得不可なら "" */,
    string ClassName, string Title, bool IsMinimized);

public sealed record ParsedTitle(
    string ProjectName,      // 解析不能なら Title 全文を MaxLabelChars で切詰め、フォルダ無しなら "(No Folder)"
    RemoteKind Remote, string? RemoteLabel,   // 例 "WSL: Ubuntu"
    bool IsNoFolder, bool IsWorkspaceFile);

public sealed record ProjectEntry(
    nint Hwnd, uint ProcessId, VsCodeEdition Edition,
    string DisplayName,      // Disambiguator 適用後
    string FullTitle,        // ツールチップ・AutomationProperties.Name
    ParsedTitle Parsed, bool IsMinimized);

/// EnumWindows が 0 を返した場合に投げる。Win32Error (GetLastError の値) を保持する。
public sealed class EnumerationException(int win32Error) : Exception { public int Win32Error { get; } = win32Error; }

public enum ActivationResult { Success, WindowGone, ForegroundBlocked }

public interface IWindowEnumerator {
    /// 入力なし。出力: フィルタ済み RawWindow (Z オーダー順)。
    /// エラー: EnumWindows 失敗時 EnumerationException を投げる (ViewModel が捕捉し前回一覧を維持)。
    IReadOnlyList<RawWindow> Enumerate();
}
public interface ITitleParser {
    /// 入力: Title, 設定 Options。出力: ParsedTitle。例外を投げない (解析不能は全文フォールバック)。
    ParsedTitle Parse(string title, Options options);
}
public interface IWindowActivator {
    /// 入力: RawWindow 相当の (Hwnd, ProcessId, ClassName)。出力: ActivationResult。
    /// WindowGone: IsWindow が false、または PID/ClassName が不一致。
    /// ForegroundBlocked: §7 の ActivateMaxAttempts 回の試行すべて失敗 (FlashWindowEx 済み)。例外は投げない。
    ActivationResult Activate(nint hwnd, uint expectedPid, string expectedClassName);
}
public interface IChangeNotifier : IDisposable {
    /// Changed はスレッドプール上で発火する (UI スレッドではない)。購読側が Dispatcher へマーシャリングする。
    /// Start 失敗 (SetWinEventHook が 0 を返す) 時は例外を投げず HookActive=false にしポーリングのみで動作する。
    event Action Changed;
    bool HookActive { get; }
    void Start();
}
```

```csharp
public sealed record Options(
    string[] TitleSeparators,      // 既定 [" - "]
    string? TitlePattern,          // 既定 null
    string[] ExtraProcessNames,    // 既定 []
    SortOrder SortOrder,           // 既定 Name
    bool Topmost,                  // 既定 false
    bool HideOnActivate);          // 既定 false
public enum SortOrder { Name, ZOrder }  // JSON 上は "name" / "zorder"
```

settings.json の JSON スキーマ (全キー任意。未指定は既定値):
```json
{"type":"object","additionalProperties":true,"properties":{
 "titleSeparators":{"type":"array","items":{"type":"string","minLength":1},"minItems":1},
 "titlePattern":{"type":["string","null"]},
 "extraProcessNames":{"type":"array","items":{"type":"string"}},
 "sortOrder":{"enum":["name","zorder"]},
 "topmost":{"type":"boolean"},
 "hideOnActivate":{"type":"boolean"}}}
```
型違反のキーは当該キーのみ既定値に戻しログに警告する。

### 4.1 設定 `Options` (`%APPDATA%\VsCodeSelector\settings.json`)

| キー | 型 | 既定 | 備考・既定の理由 |
|---|---|---|---|
| titleSeparators | string[] | [" - "] | VS Code 既定の window.titleSeparator と一致。先頭から順に試す |
| titlePattern | string? (正規表現, 名前付きグループ `project` 必須) | null | 設定時は §5.2 の P1〜P4 より優先。不正な正規表現、または名前付きグループ `project` を含まない正規表現は、起動時に警告ログを出し null 扱い。既定 null は VS Code 既定テンプレートで P1〜P4 が成立するため |
| extraProcessNames | string[] | [] | 追加が無ければ標準 3 種のみ (誤検出を避ける)。ポータブル版等の exe 名 (拡張子なし) |
| sortOrder | "name" \| "zorder" | "name" | 並びが安定し探しやすい |
| topmost | bool | false | 常時最前面は作業を妨げるため既定オフ |
| hideOnActivate | bool | false | 続けて別窓へ切替できるよう表示維持 |

ファイル不在は全既定値で起動。JSON 破損は全既定値で起動しログに記録 (ファイルは上書きしない)。未知キーは無視。

## 5. ウィンドウ列挙

### 5.1 フィルタ (EnumWindows)
以下をすべて満たすウィンドウのみ採用する。
1. `IsWindowVisible`、`GetWindow(GW_OWNER)==0`、タイトル長 > 0、`WS_EX_TOOLWINDOW` でない。
2. `DwmGetWindowAttribute(DWMWA_CLOAKED)==0` (別仮想デスクトップ等のウィンドウは除外する。NG4 と整合)。
3. クラス名が `Chrome_WidgetWin_1`。
4. exe ベース名 (`QueryFullProcessImageName`) が `Code.exe` / `Code - Insiders.exe` / `VSCodium.exe` / extraProcessNames のいずれか (大文字小文字無視)。
5. 手順 4 で `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` が失敗 (昇格プロセス) した場合のみ、exe 名判定を省略し、タイトル末尾が §5.2 P1 の APP_RE に一致すれば採用する (ProcessName は "")。
最小化中 (`IsIconic`) も採用し `IsMinimized` に記録する。

### 5.2 タイトル解析 (TitleParser) — 規則はここで 1 回だけ定義する
VS Code 既定テンプレートは `${dirty}${activeEditorShort} - ${rootName} - ${appName}`。
- P0: titlePattern が設定済みなら、一致時は `project` グループを ProjectName とし終了。不一致なら P1 へ。
- P1: `APP_RE = \s-\s(Visual Studio Code(?: - Insiders)?|VSCodium|Code - OSS)$` を除去。タイトルが appName 名そのもの (例 `Visual Studio Code`) なら IsNoFolder。APP_RE に不一致で appName 名そのものでもない場合はカスタム title とみなし P5 へ。
- P2: 先頭の `[Administrator] ` と dirty マーク (`● ` / `* `) を除去。
- P3: titleSeparators の最初に、残りを分割できる区切りで分割し、末尾トークンを rootName とする (1 トークンならそれ自体)。
- P4: rootName 末尾の `\s\[([^\]]+)\]$` を remote ラベルとして抽出 (接頭辞 `WSL`→Wsl, `SSH`→Ssh, `Dev Container`→DevContainer, `Codespaces`→Codespaces, 他→Other)。末尾の `\s\(Workspace\)$` は IsWorkspaceFile=true として除去。P3 の結果が 1 トークンで `^(Welcome|Get Started|Settings|Extension: .*)$` に一致する場合は IsNoFolder (ProjectName="(No Folder)")。
- P5 (フォールバック): 全文を MaxLabelChars で切詰めて ProjectName とする (切詰め規則: 文字数 > MaxLabelChars なら先頭 MaxLabelChars-1 文字 + `…`)。
既知の限界: フォルダ名自体が区切り文字を含む場合 (例 `my - app`) は P3 が `app` と誤抽出する。titlePattern で回避する (拒否も警告もせず、限界として明記)。

### 5.3 同名の区別 (Disambiguator)
シグネチャ: `IReadOnlyList<ProjectEntry> Disambiguator.Apply(IReadOnlyList<ProjectEntry> entries, IDictionary<nint,int> sequenceMap)` (入力と同じ順序・件数を返す純関数。例外を投げない。sequenceMap は #N 採番の保持用で呼び出し側 ViewModel が所有)。ViewModel の公開操作は `void OnChanged()` (背景スレッドから呼ばれ、列挙→解析→Apply→差分適用を行う) と `void Activate(ProjectEntry e)` (ActivationResult に応じ §7-6 の UI 処理を行う)。
同一 ProjectName が複数ある場合のみ適用。
1. Remote ラベルがあれば ` [WSL: Ubuntu]` を付加。
2. なお同名が残れば Edition が Stable 以外のものに ` (Insiders)` / ` (VSCodium)` を付加。
3. なお残れば Hwnd 昇順で 2 件目以降に ` #2`, ` #3` を付加。割当は `Dictionary<nint,int>` で保持し、再列挙でボタンの番号が入れ替わらないようにする。
FullTitle は常にツールチップに表示する。

## 6. 更新戦略
- 主: `SetWinEventHook` で `EVENT_OBJECT_SHOW` / `EVENT_OBJECT_DESTROY` / `EVENT_OBJECT_NAMECHANGE` (idObject==OBJID_WINDOW) を監視。`WINEVENT_OUTOFCONTEXT`。
- 副: PollIntervalMs ごとに全列挙し、差分があれば更新 (フックの取りこぼし対策)。このアプリのウィンドウが最小化中は PollIntervalHiddenMs を使う。
- デバウンス: DebounceMs 以内の連続イベントは 1 回にまとめる。
- 差分更新: Hwnd をキーに ObservableCollection を Add/Remove/Update し、ボタンの再生成を避ける。列挙は背景スレッド、UI 反映のみ Dispatcher 経由。
- 列挙 1 回の予算は EnumBudgetMs (ウィンドウ EnumBudgetWindows 個時)。超過時は警告ログのみ。
- フック登録に失敗した場合 (例外)、`WinEventChangeNotifier` は例外を投げず `HookActive=false` とし、`ILogger.Warn` で `WinEvent hook registration failed: <型>: <メッセージ>` を記録してポーリングのみで継続する (T10)。購読側ハンドラの例外も Warn を記録し通知経路は止めない。
- 検出遅延 DetectLatencyMax (2300ms) の厳密な値は、仮想時計の `UpdateSchedulerTests.WorstCase_VisibleReflectedWithin2300` で決定的に表明する。実時計の `EndToEndTests` は `OsJitterToleranceMs=50` の許容を加えて判定する。原因は `SystemClock.Schedule` の `Timer` 2 段 (デバウンス + ポーリング) が各々 Windows のタイマ粒度約 15.6ms を持つことと、テスト側の `Sleep(10)` ポーリング間隔である。

## 7. アクティブ化 (IWindowActivator)
1. `IsWindow(hwnd)` が false、または `GetWindowThreadProcessId` の PID / `GetClassName` が引数と不一致 → WindowGone (HWND 再利用対策)。
   - `IsWindow` は `IForegroundApi.IsWindow` (実装 `Win32ForegroundApi` は `NativeMethods.txt` 登録の CsWin32 `PInvoke` ではなく手書き `DllImport` の `user32!IsWindow`) 経由で呼ぶ。列挙側 (`Win32WindowEnumerator`) も同関数を使う。
2. `IsIconic` なら `ShowWindow(SW_RESTORE)` (最小化でなければ呼ばず最大化状態を保つ)。
3. `SetForegroundWindow` → 成功なら Success。
4. 失敗時、次を順に試す: (a) `AllowSetForegroundWindow(ASFW_ANY)` 後に再試行、(b) Alt キー単独の押下/解放 (`keybd_event(VK_MENU)`) を挟み再試行、(c) `AttachThreadInput` で前景スレッドに接続し `BringWindowToTop`+`SetForegroundWindow`、finally で必ずデタッチ。
5. 試行は最大 ActivateMaxAttempts 回、間隔 ActivateRetryDelayMs。全失敗なら `FlashWindowEx` し ForegroundBlocked を返す。UI は ToastMs の間ステータスバーに案内を出す。
6. WindowGone の場合 UI は該当ボタンを即削除して再列挙する。成功後、hideOnActivate が true ならこのアプリを最小化する。

## 8. UI
- 単一ウィンドウ。既定サイズ `WindowWidth x WindowHeight`、最小 `MinWidth x MinHeight`。いずれもクライアント領域 (枠・タイトルバーを除く) の DIP 値で、下端に高さ StatusBarHeight のステータスバーを置き、残りをボタン領域 (ListHeight) とする。
- `WrapPanel`、ボタン寸法 ButtonWidth x ButtonHeight、各ボタン外側余白 ButtonMargin (四辺)。幅に応じ自動折返し。
- 表示: 1 行目 DisplayName (太字、MaxLabelChars 超は `…`)、2 行目 Edition/Remote ラベル (小文字)。最小化中は灰色。
- 並び順: sortOrder="name" は `StringComparer.CurrentCultureIgnoreCase` 昇順 (安定ソート)、"zorder" は列挙順。
- オーバーフロー: 縦 ScrollViewer (自動表示)。ボタン数の上限なし。
- キー操作: 先頭 9 個に Ctrl+1〜9、Tab/矢印移動、Enter 実行、Esc で最小化。
- DPI: `app.manifest` で PerMonitorV2。DIP 単位で寸法を指定する。
- 空状態: 「開いている VS Code がありません」を表示。
- ログ: `%LOCALAPPDATA%\VsCodeSelector\logs\app-YYYYMMDD.log` に日次ファイルで出力する (消去は行わない)。

## 9. 定数・既定値表 (正本。他節は名前でのみ参照する)

| 名前 | 既定値 | 理由 |
|---|---|---|
| PollIntervalMs | 2000 | 体感で遅れを感じにくく、列挙 EnumBudgetMs と比べ CPU 負荷が無視できる |
| PollIntervalHiddenMs | 5000 | 非表示中は即時性が不要。CPU 節約 |
| DebounceMs | 250 | タイトル変更の連続イベント (保存・ブランチ切替) を 1 回にまとめ、かつ人間が遅延を感じにくい上限 |
| EnumBudgetMs | 50 | UI の 1 フレーム (約 16 ms) の 3 倍程度で、200 窓の EnumWindows+GetWindowText が実測で収まる上限。T15 で検証 |
| EnumBudgetWindows | 200 | 通常の VS Code 窓数 (数個) と他アプリ窓を合わせた数十個を十分上回る余裕を取った値 |
| ActivateMaxAttempts | 4 | 初回の SetForegroundWindow 1 回 + (a)(b)(c) の各 1 回 |
| ActivateRetryDelayMs | 50 | 前景遷移の完了待ちとして十分で、体感遅延にならない |
| ToastMs | 3000 | 1 文のメッセージを読み切れる長さ |
| ButtonWidth / ButtonHeight / ButtonMargin | 150 / 48 / 6 (DIP) | 40 文字ラベルを 2 行目と合わせ収める最小幅 |
| ButtonCellWidth (導出) | ButtonWidth + 2*ButtonMargin | 余白込みの 1 セル幅 |
| ScrollBarWidth | 17 (DIP) | WPF 既定。DPI 非依存の DIP 値で、テーマ差で細くなる場合は列数に余裕が増えるだけで安全側 |
| MaxLabelChars | 40 | ButtonWidth 150 DIP の内側 (150-16=134 DIP) に 12pt 全角混在で約 20 文字/行 x 2 行で収まる上限 |
| WindowWidth / WindowHeight | 520 / 384 (DIP, クライアント領域) | 3 列と 6 行を縦スクロールなしで収める (§10)。ListHeight = WindowHeight - StatusBarHeight |
| StatusBarHeight | 24 (DIP) | 1 行の 12pt 文字 (約 16 DIP) と上下余白 4 DIP ずつ |
| MinWidth / MinHeight | 240 / 120 (DIP, クライアント領域) | 1 列 (§10: 240-17=223 ≥ 162) と 1 行 (120-24=96 ≥ 60) が収まる最小 |
| Options 各既定値 | §4.1 の表 | 変更しない場合に標準的な VS Code 挙動と一致させる |

## 10. 検算 (数値・規則)

- 検出遅延の最悪値 = PollIntervalMs + DebounceMs + EnumBudgetMs = 2000 + 250 + 50 = 2300 ms (DetectLatencyMax と呼ぶ)。非表示時 DetectLatencyMaxHidden = PollIntervalHiddenMs + DebounceMs + EnumBudgetMs = 5000 + 250 + 50 = 5300 ms。境界: 2300 ms ちょうどで反映なら合格、2301 ms は不合格 (T8)。
- 列数 = floor((WindowWidth - ScrollBarWidth) / ButtonCellWidth)。ButtonCellWidth = 150 + 12 = 162。(520-17)/162 = 3.10 → 3 列。境界: 4 列には 4*162 = 648 > 503 で不足、3 列は 3*162 = 486 ≤ 503 で成立。最小幅 240 では (240-17)/162 = 1.38 → 1 列。
- 縦表示行数 = floor(ListHeight / (ButtonHeight + 2*ButtonMargin)) = floor((384 - 24) / 60) = floor(360/60) = 6 行 → スクロールなしで 3 x 6 = 18 ボタン。19 個目でスクロール。WindowHeight が 383 なら floor(359/60) = 5 行 (境界)。幅は WindowWidth がクライアント幅のため枠分を含まない: 余裕 = (520-17) - 486 = 17 DIP。
- 切詰め: 長さ 40 は無変更、長さ 41 は先頭 39 文字 + `…` = 40 文字。往復: 切詰め結果の長さ ≤ MaxLabelChars が常に成立。
- 再試行の最大待機 = (ActivateMaxAttempts - 1) * ActivateRetryDelayMs = 3*50 = 150 ms (最初の試行は待機なし)。
- 往復 (入力 → ProjectName → 埋め戻し): T2b `● main.ts - my-project - Visual Studio Code - Insiders` → `my-project` → `● main.ts - my-project - Visual Studio Code - Insiders` と一致。T2d → `app` + `[WSL: Ubuntu]` を `a.ts - app [WSL: Ubuntu] - Visual Studio Code` に戻して一致。T2e → `team` + ` (Workspace)` を戻して `x - team (Workspace) - Visual Studio Code` と一致。
- 状態遷移の往復シミュレーション (一覧 L を時刻 t[ms] で追う): t=0 L={A,B}。t=1000 窓 B を閉じ EVENT_OBJECT_DESTROY 発火。t=1250 デバウンス満了、t=1250〜1300 列挙で L={A} (遅延 300 ≤ DetectLatencyMax)。フック失敗時は t=2000 のポーリングで列挙開始、t=2250 デバウンス相当待機後、t=2300 に L={A} (遅延 1300 ≤ DetectLatencyMax)。最悪ケースは t=1 に閉じて次のポーリング t=2000 を待つ場合で、2000+250+50 = 2300 が DetectLatencyMax と一致し、2301 を超える遅延は設計上起こらない。往復: L={A} の状態で窓 B を再度開くと、Hwnd が新規のため Add され L={A,B'} に戻る (§6 の Hwnd キー差分)。
- DPI 検算: 寸法は DIP 単位のため、DPI 100/150/200% での物理 px は 520, 780, 1040 (WindowWidth x 倍率) だが、DIP 換算の列数は (520-17)/162 = 3.10 → 3 列で不変。ScrollBarWidth は DIP 固定値として扱う。
- タイトル解析の往復例: `main.ts - my-project - Visual Studio Code` → P1 除去 `main.ts - my-project` → P3 `[main.ts, my-project]` → ProjectName `my-project`。これを再度 `{file} - {project} - Visual Studio Code` に埋め戻すと元のタイトルに一致する。

## 11. 失敗モード・機構・トレードオフ対応表 (1 対 1)

決定欄は「拒否 / 警告 / 記録」のどれにするかを示す。

| ID | 失敗モード | 機構 | 失敗させる正当ケース | 代替案 | 決定 | 試験 |
|---|---|---|---|---|---|---|
| F1 | VS Code 以外の Electron ウィンドウの混入 | §5.1 の exe 名 + クラス名フィルタ | ポータブル版など改名 exe が表示されない | extraProcessNames で追加可能。クラス名だけの判定は誤検出が多く却下 | 拒否 (非表示) | T1 |
| F2 | 昇格プロセスで exe 名が取れない | §5.1-5 のタイトル末尾フォールバック | 偶然タイトルが APP_RE に一致する非 VS Code ウィンドウを表示しうる | 昇格ウィンドウを一律非表示にするとユーザーが気付けない | 記録 (ログ) し表示 | T9 |
| F3 | 押下前にウィンドウが閉じた / HWND 再利用 | §7-1 の IsWindow + PID + クラス再検証 | 同一 PID で別ウィンドウに HWND が再利用された稀なケースは通過する (限界) | 作成時刻比較は API 追加コストに見合わず却下 | 拒否 + 即削除 | T6 |
| F4 | 前景ロックで SetForegroundWindow が失敗 | §7-4 の (a)(b)(c) と FlashWindowEx。(c) の AttachThreadInput は入力キューを共有し前景アプリの入力状態を乱しうるため最終手段 | (b) の Alt 押下が前景アプリのメニューを誘発しうる | Alt を使わない (c) のみでは成功率が下がる | 警告 (トースト + 点滅) | T7, T16 |
| F5 | フォルダ名に区切り文字を含み誤抽出 | titlePattern による上書き | 正規表現の設定が必要 | ウィンドウのコマンドラインからの取得は窓単位で取れず不可 | 限界として明記 (自動では守れない) | T4 |
| F6 | カスタム window.title で解析不能 | §5.2-P5 の全文フォールバック | 長いタイトルが切詰められる | 非表示にすると窓が見えなくなる | 記録して表示 | T5 |
| F7 | 同名フォルダでボタンが区別できない | §5.3 Disambiguator | 付加文字列でラベルが長くなる | 常時パス表示は幅を圧迫。#N 採番は Hwnd 順で固定するため、窓を閉じて開き直すと番号が変わりうる (限界。代替のパス表示は幅の理由で却下) | 表示 (区別付加) | T3 |
| F8 | WinEvent の取りこぼし / フック失敗 | PollIntervalMs のポーリング | CPU を常時わずかに使う | ポーリング無しは取りこぼしで一覧が古いまま | 警告ログ (HookActive=false) | T10 |
| F9 | イベント嵐による再描画の連打 | DebounceMs と Hwnd キー差分更新 | 更新が最大 DebounceMs 遅れる | 即時反映はちらつき・負荷増 | 記録 (デバッグログのみ) | T8 |
| F10 | EnumWindows 失敗 | 前回一覧を維持して次回ポーリングで再試行 | 一時的に古い一覧を見せる | 空表示にすると操作不能になる | 警告 (ステータスバー) | T11 |
| F11 | 別仮想デスクトップのウィンドウ | §5.1-2 で除外 (NG4) | そのウィンドウへ切替できない | 切替には仮想デスクトップ COM API が必要で非公開・不安定 | 守れない (非スコープと明示) | T12 |
| F12 | 設定ファイル破損 / 不正な正規表現 | 全既定値起動 / null 扱い + ログ | ユーザー設定が一時的に無視される | 起動拒否はアプリ自体が使えなくなる | 警告ログ | T13 |

## 12. 受け入れテスト

TitleParser 表 (入力 Title, titleSeparators=[" - "], titlePattern=null → 期待 ProjectName / 他):

| ID | 入力 Title | 期待出力 | 合否 |
|---|---|---|---|
| T2a | `main.ts - my-project - Visual Studio Code` | `my-project` | 完全一致で合格 |
| T2b | `● main.ts - my-project - Visual Studio Code - Insiders` | `my-project` | 完全一致 |
| T2c | `my-project - Visual Studio Code` | `my-project` | 完全一致 |
| T2d | `a.ts - app [WSL: Ubuntu] - Visual Studio Code` | `app`, Remote=Wsl, RemoteLabel=`WSL: Ubuntu` | 3 値一致 |
| T2e | `x - team (Workspace) - Visual Studio Code` | `team`, IsWorkspaceFile=true | 2 値一致 |
| T2f | `Welcome - Visual Studio Code` | `(No Folder)`, IsNoFolder=true | 2 値一致 |
| T2g | `Visual Studio Code` | `(No Folder)` | 完全一致 |
| T2h | `日本語フォルダ - Visual Studio Code` | `日本語フォルダ` | 完全一致 |
| T4 | `a.ts - my - app - Visual Studio Code` | `app` (既知の限界)。titlePattern=`^\S+ - (?<project>.+) - Visual Studio Code$` 設定時 `my - app` | 両方一致 |
| T5 | `A` を 41 個並べた文字列 (APP_RE 不一致) | `A` 39 個 + `…` (長さ 40)。`A` 40 個の入力は無変更 | 長さ 40 かつ末尾 `…` で合格 |

動作テスト:

| ID | 入力 / 手順 | 期待出力 | 合否 |
|---|---|---|---|
| T1 | notepad・Chrome・Slack(Electron) と VS Code 2 窓を起動し列挙 | VS Code 2 件のみ | 件数 2 かつ他 exe 無しで合格 |
| T3 | 同名 `app` を Stable x2, Insiders x1 で列挙 | DisplayName が `app`, `app #2`, `app (Insiders)` で重複なし。再列挙しても番号不変 | 3 件が一意かつ不変 |
| T6 | 一覧表示後に対象ウィンドウを閉じ、再列挙より前にボタン押下 (テストでは Activate を直接呼ぶ) | Activate が WindowGone を返し、ボタンが消える | 戻り値と UI が一致 |
| T7 | 別アプリが前景で、最小化中の VS Code ボタンを押す | 復元され前景化される。失敗時は ForegroundBlocked + タスクバー点滅 | Success なら GetForegroundWindow()==hwnd で合格。失敗時は戻り値と点滅で合格 |
| T8 | VS Code 窓を閉じる | DetectLatencyMax 以内にボタンが消える。このアプリ最小化中は DetectLatencyMaxHidden 以内 | 時間内に消えれば合格 |
| T9 | 管理者権限で VS Code を起動して列挙 | ボタンが表示され、ログに OpenProcess 失敗が 1 行出る | 表示かつログ有りで合格 |
| T10 | フック登録を失敗させるテストダブルで起動 | HookActive=false。ポーリングのみで T8 が成立 | T8 と同条件で合格 |
| T11 | IWindowEnumerator が例外を投げるモック | 一覧は前回のまま、ステータスバー警告、次回成功で復帰 | 一覧不変かつ復帰で合格 |
| T12 | 別仮想デスクトップに VS Code 窓を置く | 一覧に出ない (非スコープ) | 非表示で合格 |
| T13 | settings.json に不正 JSON / 不正正規表現 | 既定値で起動し、ログに警告 | 起動成功かつログ有りで合格 |
| T14 | クライアント幅 WindowWidth・高さ WindowHeight で 19 個のモック | 3 列 6 行 + 縦スクロールバー表示。DPI 100/150/200% で同一レイアウト | 列数 3、スクロール有りで合格 |
| T15 | EnumBudgetWindows 個のダミー可視ウィンドウ (他クラス名) を作り、本体 Release ビルド・通常電源設定の Windows 実機で Enumerate を 100 回計測 | 100 回の 95 パーセンタイル ≤ EnumBudgetMs | 95 パーセンタイルで合格 |
| T16 | IWindowActivator のテストダブルで SetForegroundWindow を常に 0 返却にし、ActivateMaxAttempts 回の試行を観測 | 試行回数が ActivateMaxAttempts、間隔が ActivateRetryDelayMs、戻り値 ForegroundBlocked、FlashWindowEx が 1 回 | 4 点一致で合格 (F4) |

全体の受け入れ基準: T1〜T16 全通過、単体テスト (T2a〜T2h, T4, T5, Disambiguator, ViewModel 差分更新) 全件パス。

## 13. 範囲外 (1 行)

配布・インストーラ・CI・ビルド手順、VS Code の起動/新規作成、拡張機能化、Windows 以外、仮想デスクトップ切替、VS Code 内部状態取得、管理者権限 VS Code の操作保証は対象外 (NG)。

## 実装メモ (implementation notes)

- `Win32ForegroundApi` は `Win32ForegroundApi.cs` に置く (`IForegroundApi.cs` から移動)。
- `NativeMethods.txt` には `IsWindow` も追加している。
- `GetClassNameW`、DWM、WinEvent フック、前景化系 API は `NativeMethods.txt` に無いため、手書きの `DllImport` を使う。
- T005 性能所見: `Win32WindowEnumerator` を安価なチェック優先の順に並べ替え、pid 単位の exe 名キャッシュを追加。200 窓で p95 約 11ms (変更前 90ms)。`EnumBudgetMs=50` を満たす。
- `WindowActivator` / `WinEventChangeNotifier` は任意の `ILogger` を受け取り、握りつぶしていた例外を型限定 (`OutOfMemoryException` 除外) でログ出力する。`Activate` は従来どおり例外を投げない。
- 実行コマンド: `dotnet build <csproj> -c Release` / `dotnet test tests/VscodeSelector.Tests --filter "Category!=Interactive"` / `dotnet test tests/VscodeSelector.Tests -c Release --filter "Category=Interactive"`。
