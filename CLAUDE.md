# Whiteboard

Discord に投稿する定型の前置きを、マウス操作だけで入力するための仮想マクロパッド。
メンバーに配布する Windows 専用の WPF アプリ。

## 目的と使い方

投稿は次のフォーマットに従う。前置き部分（本文の手前まで）を本アプリで入力し、本文はユーザーが自分で打って Enter で送信する。

```
{時刻 HH:mm} {How} {from}→{to} {本文}
例: 14:30 tel 田中→佐藤 折り返しお願いします
例: 14:30 tel&mail 田中→佐藤 資料を送りました
```

- When: `now`（そろった時点の現在時刻）か、手入力した時刻。一度選ぶと、貼り付けや［クリア］の後も保持する（同じ時刻で何件も入力できるように）
- How: 候補（tel・mail）から1つ以上選ぶ。複数選んだ場合は候補の並び順に `&` でつなぐ（押した順ではない）
- from / to: Who の候補から1つずつ選ぶ。Who は exe と同じフォルダの `who\` フォルダに .txt で事前に書いておく。1ファイルが1タブになり、from と to は別のタブから選んでもよい
- `→` の前後にスペースは入れない。末尾にはスペースを1つ付け、すぐ本文を打てるようにする

### 操作の流れ

1. ユーザーは Discord の入力欄にカーソルを置いておく
2. 必要なら When を切り替える。パレットで How を選び（もう一度押すと選択を外す）、Who を2回押す（1回目が from、2回目が to）
3. Who の2回目を押した時点で、その時刻を入れた前置きを Discord の入力欄に一括で貼り付ける。Who を先に2回選んだ場合は、How を1つ押した時点で貼り付ける（この場合 How は1つだけ）
4. How・Who の選択をリセットする（When は残す）

When の時刻入力（`14:30`・`1430`・`930`・`14` などを受け付ける）だけはキーボードが要るので、入力欄をクリックしたときだけパレットをアクティブにし、Enter・Esc・他のボタンのクリックで入力を終えたら直前の窓（Discord）をアクティブに戻す（`Interop/ForegroundSwitcher`）。

途中まで組み立てた文字列は Discord に送らず、パレット上のプレビューにのみ表示する。［クリア］ボタンで選択をやり直せる。

## 設計上の必須要件

### フォーカスを奪わない窓（最重要）

本アプリはスクリーンキーボードと同じ考え方で動く。パレットをクリックしても Discord 側のフォーカスとキャレットが残っていなければならない。

- `ShowActivated="False"`、`Topmost="True"`
- `SourceInitialized` で HWND を取得し、拡張スタイルに `WS_EX_NOACTIVATE`（0x08000000）と `WS_EX_TOOLWINDOW` を付ける
- `HwndSource.AddHook` で `WM_MOUSEACTIVATE`（0x0021）を捕まえ、`MA_NOACTIVATE`（3）を返す
- ボタンなどのコントロールは `Focusable="False"` にする

### 入力はクリップボード経由の貼り付け

IME がオンの状態でも崩れないよう、キー入力を1文字ずつ再現する方式は使わない。

1. 現在のクリップボードのテキストを退避する（最初はテキスト形式のみでよい）
2. 前置き文字列をクリップボードにセットする。このとき `ExcludeClipboardContentFromMonitorProcessing` 形式も付け、Windows のクリップボード履歴に残らないようにする
3. `SendInput` で Ctrl+V を送る
4. 100〜200ms 程度待ってから、退避した内容を復元する（待ち時間は設定で変えられるようにする）
5. クリップボードを開けない（他アプリが使用中）場合は、短い間隔で数回リトライする

**Enter は送らない。** 送信は必ずユーザーが行う。

## 構成

```
Whiteboard/
├ Views/MainWindow.xaml        パレット本体（ボタン・プレビュー）
├ ViewModels/PaletteViewModel  選択状態（How・from・to）とクリック時の処理
├ Core/PrefixBuilder           テンプレート＋選択値＋時刻 → 文字列
├ Interop/NoActivateWindow     WS_EX_NOACTIVATE / WM_MOUSEACTIVATE の処理
├ Interop/InputInjector        クリップボード退避・セット・Ctrl+V・復元
└ Services/SettingsService     設定の読み込み（既定値＋ユーザーごとの上書き）
Whiteboard.Tests/              xUnit。PrefixBuilder・PaletteViewModel・SettingsService のテスト
```

- `PrefixBuilder` は UI と Win32 に依存しない純粋なクラスにし、時刻は引数（または `TimeProvider`）で受け取って単体テストできるようにする
- MVVM には CommunityToolkit.Mvvm を使う
- Win32 API の呼び出しは `Interop/` 配下に閉じ込める

## 設定ファイル

JSON（System.Text.Json）を使う。

```json
{
  "timeFormat": "HH:mm",
  "template": "{time} {how} {from}→{to} ",
  "how": ["tel", "mail"],
  "clipboardRestoreDelayMs": 150,
  "checkForUpdates": true
}
```

- 配布用の既定設定は exe と同じフォルダの `settings.json`
- ユーザーごとの変更と窓の位置は `%APPDATA%\Whiteboard\` に保存し、読み込み時に既定設定を上書きする
- `%APPDATA%` 配下のフォルダ名は、アプリの表示名から自動生成せず**独立した定数として固定する**（表示名を後で変えても設定が引き継がれるようにするため）
- 設定ファイルが無い・壊れている場合は、組み込みの既定値で起動する

### Who の候補（who フォルダ）

exe と同じフォルダの `who\` に置いた `.txt`（UTF-8）を、1ファイル1タブとして読む。

- タブ名はファイル名（拡張子なし）。タブはファイル名順に並ぶ。先頭に `1_` のような「数字＋`_`」を付けると並び順を指定でき、この番号はタブ名に表示しない
- 中身は1行1件。前後の空白は除き、空行・`#` で始まる行・重複は無視する
- `%APPDATA%` 側には置かない（全員で同じ名簿を使う）
- 名簿は各自が exe と同じフォルダの `who\` に置く（アプリは共有フォルダなどを直接読まない）
- メンバーの名前が入るので `who/*.txt` は `.gitignore` でリポジトリから除外している（GitHub で公開するため）。ビルド・publish は手元のファイルを使う
- フォルダが無い・.txt が無い場合はパレットに案内を表示する。中身が空のファイルは空のタブとして表示する
- TabItem は通常フォーカスを得たときに選択されるが、パレットでは `Focusable="False"` なので、クリック時にコードで直接選択する

## 窓の仕様

- タイトルバーなしの小さなパレット。余白部分をドラッグして移動できる
- 終了時の位置を保存し、次回同じ位置に表示する（画面外に出ていたら補正する）
- PerMonitorV2 の DPI 対応をマニフェストで指定する
- 見た目は WPF の Fluent テーマ（`ThemeMode`）。必要なら実験的機能の警告を抑制する
- 左上の ⚙ ボタンで、パレット内のボタンのすぐ下（時計より上）に設定欄を開閉する（ポップアップはフォーカスを奪わない窓と相性が悪いので使わない）。設定欄でテーマを「システム（Windows に合わせる）／ライト／ダーク」から選ぶと、`Application.ThemeMode` をその場で切り替え、`%APPDATA%\Whiteboard\settings.json` の `"theme"`（`system` / `light` / `dark`）に保存する。保存時はほかの項目を残し、ファイルが壊れている場合はユーザーの内容を消さないよう保存しない
- 設定欄では文字サイズも「小 14／中 16（既定）／大 18」と［−］［＋］（1 ずつ、10〜32）で変えられ、`"fontSize"` に保存する。プレビューは本体より 4 大きく表示する
- パレットの一番上中央に時計を2段で表示する（上段に日付と曜日 `yyyy/MM/dd (ddd)`（例: `2026/10/05 (Mon)`）、下段に時刻 `HH:mm:ss` を大きく。1秒ごとに更新）。その下のプレビュー（入力される内容）はアクセント色の枠のカードで囲む。時計の上の行に、設定（⚙）を左上、終了（✕）を右上に置く。設定欄で表示／非表示を切り替え、`"showClock"` に保存する（既定は表示）

### 更新の通知

GitHub Releases の最新版を確認し、新しければパレットの一番上（⚙ と ✕ の間）にボタンを出す。押すとリリースのページを既定のブラウザで開く。自動ダウンロード・自動更新はしない（実行中の exe は自分を上書きできず、`who\` や `settings.json` の扱いも面倒なため）。

- 確認先は csproj の `UpdateRepository`（`owner/repo`）を `AssemblyMetadata` で埋め込んだもの。空にすると確認しない
- `GET https://api.github.com/repos/{owner/repo}/releases/latest` を未認証で呼ぶ（User-Agent 必須。上限は 1 IP あたり 60 回/時）。起動時に1回だけ確認する（定期的には確認しない）
- 今の版は `Assembly.GetName().Version`（csproj の `Version`）、新しい版はタグ（`v0.2.0` など）。足りない桁は 0 として比べる。`-beta` などの接尾辞付きタグ・下書き・プレリリースは通知しない
- オフライン・タイムアウト（10 秒）・404（リリースなし）・403/429（上限）は黙って無視する
- トーストやダイアログはフォーカスを奪うので使わない
- `"checkForUpdates": false` で止められる

## 名前の扱い

- 名前空間・プロジェクト名は `Whiteboard` で固定（内部のコードネーム）
- 配布時の表示名・exe 名・製品情報は csproj の `AssemblyName` / `Product` / `AssemblyTitle` / `Company` / `Version` / `ApplicationIcon` で管理し、コードに直書きしない
- 配布時の名前は **Tsurara**（exe は `Tsurara.exe`、zip は `Tsurara-<Version>.zip`）。`%APPDATA%\Whiteboard\` のフォルダ名は変えない
- アイコンは `Assets\app.ico`（16〜256 の7サイズ入り）を `ApplicationIcon` で exe に埋め込む
- アイコンは MIT の対象にしないため `.gitignore` でリポジトリから除外している。ファイルが無いときはアイコンなしでビルドする（`Condition="Exists(...)"`）。アイコンを後から置いた場合、差分ビルドでは埋め込まれないことがあるので `obj\` を消してからビルド・publish する

## ビルドと配布

- ターゲット: .NET 10、`net10.0-windows`、win-x64
- 配布は追加インストール不要の自己完結型単一 exe

```
dotnet build
dotnet run
dotnet test Whiteboard.slnx
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

WPF はトリミング非対応なので `PublishTrimmed` は使わない。

配布用の zip は `publish.ps1` で作る（`powershell -ExecutionPolicy Bypass -File publish.ps1`）。毎回 `dist\<AssemblyName>\` を空にしてから publish し、`dist\<AssemblyName>-<Version>.zip`（中身は `<AssemblyName>/` フォルダ1つ）を作る。名前とバージョンは csproj から取る。

- zip は GitHub のリリース（誰でもダウンロードできる）に添付するので、メンバーの名前が入った `who\` は**入れない**。名簿は各自で `who\` に置く（`dist\<AssemblyName>\` フォルダには手元の `who\` も入るが、動作確認用）
- 更新は「zip の中身を今のフォルダに上書きコピー」する手順にしている（フォルダごと置き換えると `who\` が消えるため）

- 配布物（zip）は `Tsurara.exe`・`settings.json`・`README.md`。.pdb は `CopyOutputSymbolsToPublishDirectory=false` で含めない
- `publish.ps1` は Windows PowerShell 5.1 で動かすため UTF-8（BOM 付き）で保存する。zip は `ZipFile.CreateFromDirectory` ではなく、区切りを `/` にして1ファイルずつ追加する（5.1 では `\` になってしまうため）
- `README.md` は利用者向けの説明書。開発者向けの内容はこの CLAUDE.md に書く。README は GitHub と zip で公開されるので、所属や運用の話は書かない

新しい版を出す手順:

1. csproj の `Version` を上げて `publish.ps1` を実行する
2. GitHub でタグ `v<Version>` のリリースを作り、`dist\<AssemblyName>-<Version>.zip`（who なし）を添付する（`gh release create v0.2.0 dist\Tsurara-0.2.0.zip`）。プレリリースにすると通知されない

## 現在のフェーズ: 本実装

試作（最前面のパレットにボタンを1つ置き、固定文字列を貼り付ける）で、次の2点を検証済み。

1. フォーカスを奪わない窓のボタンを押しても、Discord の入力欄にキャレットが残ったまま貼り付けられる（IME オンでも可）。`DragMove()` による窓の移動でもフォーカスは奪われない
2. クリップボード復元までの待ち時間は、Discord では 25ms まで取りこぼしなし。PC の負荷による遅れを見込み、既定値は 150ms のままとする

パレットの本番 UI・設定ファイル・窓の位置の保存・Fluent テーマは実装済み（2026-10-05）。

## 未決事項

- 「前回の to を次の from に自動で引き継ぐ」機能を入れるか（まずは引き継ぎなしで実装済み。使ってみて手間なら、設定で ON/OFF できる形で追加する）
- タスクトレイへの格納（入れる場合は H.NotifyIcon などを検討）
  - タスクバーにボタンを出す案（`WS_EX_TOOLWINDOW` を外して `WS_EX_APPWINDOW` を付ける）は試して見送った。タスクバーのボタンを押すとパレットがアクティブになり、その状態で How・Who を押すと Ctrl+V がパレット自身に送られて何も貼り付けられないため

## やらないこと

- Electron / Tauri など WebView ベースの実装（フォーカス制御と相性が悪いため）
- Enter キーの自動送信、時刻指定などによる自動投稿
- Discord の API やユーザートークンを使った投稿（セルフBot は Discord の規約違反）
