# BACKBEAT DINER — Unity 制作リポジトリ

**BACKBEAT DINER**（バックビート・ダイナー）は、スタイリッシュなローポリ3Dの **非対称2P協力リズム＋ダイナー** ゲームです。裏プレイヤーがフルチャート、表プレイヤーが調理MGと配膳リズムを担当します。

## 推奨 Unity バージョン

| 項目 | 内容 |
|------|------|
| **採用エディタ** | **Unity 6000.0 LTS** — 本プロジェクトは **`6000.0.28f1`** で固定（`ProjectSettings/ProjectVersion.txt`） |
| **代替** | チーム方針で **2022.3 LTS** に寄せる場合は、Hub でバージョンを揃えたうえで再生成される設定差分をコミットしてください |
| **レンダリング** | **URP**（`Assets/_Project/Settings/PC_RPAsset.asset`） |
| **入力** | **Input System**（新規プロジェクト設定。初回インポートを許可） |
| **マルチプレイ（将来）** | **Netcode for GameObjects** + **Multiplayer Play Mode**（パッケージ済み、プロトはローカル2P優先） |
| **Cinemachine** | パッケージ同梱（**任意**。カメラワーク確定後に有効化） |

## 開き方（Unity Hub）

1. [Unity Hub](https://unity.com/download) をインストールし、エディタ **6000.0.28f1**（または同等の 6000.0.x LTS）を追加します。
2. **Projects → Add →** リポジトリ内の **`BackbeatDiner`** フォルダを選択します（リポジトリルートではありません）。
3. 初回起動で **URP / Input System** のパッケージ解決と、必要に応じた **プロジェクト設定のアップグレード** が走ります。差分はレビューしてコミットしてください。
4. **タイトル／営業開始**: `Assets/_Project/Scenes/TitleMenu.unity` を開き **Play**（Build Settings 先頭。無い場合は **BACKBEAT DINER → Build Title Menu Scene**、または batchmode で `TitleMenuSceneBuilder.BuildFromCommandLine`）。
5. **Week 1 リズム試作**: `Assets/_Project/Scenes/RhythmPrototype.unity` を開き **Play**（無い場合はメニュー **BACKBEAT DINER → Build Rhythm Prototype Scene**、または batchmode で `RhythmPrototypeSceneBuilder.BuildFromCommandLine`）。
6. **Week 2 表 HUD 殻**: `Assets/_Project/Scenes/FrontShellPrototype.unity` を開き **Play**（無い場合はメニュー **BACKBEAT DINER → Build Front Shell Prototype Scene**、または batchmode で `FrontShellPrototypeSceneBuilder.BuildFromCommandLine`）。
7. **縦スライス（1チャート＋表ミニ）**: `Assets/_Project/Scenes/VerticalSlicePrototype.unity` を開き **Play**（無い場合はメニュー **BACKBEAT DINER → Build Vertical Slice Prototype Scene**、または batchmode で `VerticalSlicePrototypeSceneBuilder.BuildFromCommandLine`）。
8. スキャフォールド確認のみなら **`Assets/Scenes/Bootstrap.unity`** を **Play**。Console に `[BACKBEAT DINER] Bootstrap ready` が出れば OK（`GameBootstrap` は `DontDestroyOnLoad` シェル）。

## タイトルメニュー — 役選択 → 営業開始（操作）

Gold `#C9A227` / Teal `#1A6B6B` / Navy `#0E1F33` のタイトル画面。キャッチコピー「見えない相棒と、最高の一皿を。」。**2デバイス（各1画面）**前提で、端末ごとに役を選んで縦スライス日を開始します。

| 操作 | 内容 |
|------|------|
| **裏で始める**（クリック） / **1** | 裏役で `VerticalSlicePrototype` を全画面ロード |
| **表で始める**（クリック） / **2** | 表役で同上 |

シーン: `Assets/_Project/Scenes/TitleMenu.unity`（Build Settings **先頭**）  
再生成: **BACKBEAT DINER → Build Title Menu Scene**（`TitleMenuController` / `TitleMenuSceneBuilder`）。

## Week 1 — Rhythm tick プロト（操作）

| 操作 | 内容 |
|------|------|
| **Space** または **Gamepad South (A)** | ヒット判定（Perfect ±40ms / Good ±80ms）。画面に **判定 / コンボ / BPM**、Console にもログ |
| **1 / 2 / 3 / 4** | KG-3 **Beat-B1..B4**（プッシュ／クリア／スナップ前／あとで）。CD **8s**／同一モチーフ **20s**。日本語パッド UI |
| メトロノーム | `Assets/_Project/Audio/MetronomeClick.wav`（手続き PCM・ライセンスは同フォルダ `AUDIO_LICENSE.md`） |

シーンが未生成のとき: Unity メニュー **BACKBEAT DINER → Build Rhythm Prototype Scene** で `RhythmPrototype.unity` を作成し Build Settings 先頭に登録します。エディタがプロジェクトをロックしている場合は **Assets → Refresh** 後に再 Play してください。

## Week 2 — Front HUD shell プロト（操作）

| 操作 | 内容 |
|------|------|
| （表示のみ） | ローポリ客 1〜3 人＋タイムアウトバー、日本語 HUD（**客待ち / 在庫 / クォータ / CD**） |
| **1 / 2 / 3 / 4** | **Beat-F1..F4**（ビジー／ビッグオーダー／在庫赤／ラスト客）送信 SE → **エッジパルス**＋波形。CD **8s**／同一 **20s**。日本語パッド |
| **B** | 裏ビート受信のシミュレート（自動パルスもあり） |
| HUD CD | `BeatLexicon` 連動（準備OK / 残り秒 / 配膳ロック） |
| 初回のみ | 日本語チュートリアルトースト「裏からのビートを受信しました」 |

ブランド色マテリアル: `Assets/_Project/Art/Materials/BrandGold.mat`（`#C9A227`）/ `BrandTeal.mat`（`#1A6B6B`）。再生成は **BACKBEAT DINER → Build Front Shell Prototype Scene**。

## 縦スライス — 1チャート＋表ミニ（2デバイス・役ごと全画面）

**1モニター左右分割ではありません。** 端末（またはウィンドウ）ごとに **裏 or 表を1役だけ全画面** 表示します。共有資源は **仕込み**（裏ヒットで増加）と **提供**（表インスタントクック → **配膳リズム F1+** で消費）。`DaySessionState` / セットリストは各シーンでホストロジックとして常時動作。キー割当は `LocalTwoPlayerBindings`。ブランド色は Gold `#C9A227` / Teal `#1A6B6B` / Navy `#0E1F33`。

| 役 | 操作 | 内容 |
|----|------|------|
| **裏** | **WASD + Space**（Gamepad South） | レーンヒット（Perfect/Good → 仕込み +2/+1） |
| **裏** | **A / S / D / F** | **Beat-B1..B4**。CD 8s／同一20s |
| **裏** | **Q** | **C1 クエスト要求**（CD **15s**・ノーツ中ロック） |
| **表** | **Enter**（Gamepad West） | インスタントクック → **配膳リズム** |
| **表** | **J · K**（配膳中） | S1 拍合わせ。Perfect チップ×1.5 / Good×1 / Miss＝基本売上のみ |
| **表** | **← ↓ → ↑** | **Beat-F1..F4**（配膳中は表のみロック） |
| **表** | **Tab 長押し** | **C2 スナップショット**（0.8s → 1.2s → CD 25s） |
| — | **F8** | ソロ確認用に裏↔表の全画面ビューを切替 |
| — | 終了 | 約 **32拍** または **30秒** で結果オーバーレイ |

### 役の選び方

1. **TitleMenu**: **裏で始める** / **表で始める**（または **1** / **2**）
2. **VerticalSlice を直接 Play**: 開始オーバーレイで同じ選択（1/2 またはボタン）
3. **ソロ**: プレイ中 **F8** で表示役を切替（入力キーはそのまま両役分）

### Vertical Slice HUD マップ（Cooking Simulator 風・単一役）

```
【裏・全画面】キッチン＋注文票＋監視 PiP＋Bパッド＋C1
【表・全画面】ダイナー＋サービス盤＋在庫＋配膳メーター＋Fパッド＋C2
共有: ブランド / セッション進行 / 間隙バナー / 結果カード
```

| 領域 | 主な UI | スクリプト |
|------|---------|------------|
| 裏・注文票 | 判定・コンボ・BPM・C1 | `VerticalSliceBootstrap` |
| 裏・監視 | 表フロア PiP + BLIND | `SurveillanceThumbnail` |
| 裏・パッド | A/S/D/F = Beat-B* | `BeatLexiconDemo` (Back) |
| 表・サービス | 仕込み・提供・配膳メーター | `DaySessionState` / `ServeRhythmWindow` |
| 表・パッド | ←↓→↑ = Beat-F* | `BeatLexiconDemo` (Front) |
| 役選択 | タイトル / 開始オーバーレイ / F8 | `VerticalSliceRoleSelection` |
| 結果 | 営業終了カード | `ResultOverlayController` |

シーン: `Assets/_Project/Scenes/VerticalSlicePrototype.unity`  
再生成: **BACKBEAT DINER → Build Vertical Slice Prototype Scene**。

## フォルダ構成（`Assets/_Project/`）

```
Scenes/          … 制作シーン（Bootstrap は Assets/Scenes/）
Scripts/
  Core/          … 起動・遷移（GameBootstrap）
  Rhythm/        … 譜面再生・判定（KG-2 / KG-4）
  Coop/          … ビートコミュ（KG-3）
  Back/          … 裏プレイヤー・資源
  Front/         … 表・調理MG・配膳（KG-5）
Art/ Audio/ UI/ Prefabs/ Settings/   … URP・ボリューム等
```

## 次のプロト手順（ローカル2P first）

1. **Week 1 — リズム tick**（着手済み）: `RhythmClock` + メトロノーム WAV + `HitInput` / `NoteHighway` / `BeatLexicon`（Beat-B*）。シーン `RhythmPrototype`。
2. **Week 2 — 非対称 HUD 殻**（着手済み）: 表シーン `FrontShellPrototype` — `FrontHudController` / `DummyCustomerQueue` / `InventoryBar` / `BeatReceivePulse` / **Beat-F* パッド＋CD**。裏レーンは Week1 `RhythmPrototype` を併用。
3. **ビート送信プロト**（着手済み）: KG-3 C3 **BeatLexicon**（B1..F4 静的定義、8s／同一20s CD、日本語パッド、配膳中ロック）。
4. **1日縦切り**（着手済み）: `VerticalSlicePrototype` — **役ごと全画面**（2デバイス）、裏1チャート＋表インスタントクック＋**F1+ 配膳リズム**、共有 `DaySessionState`、結果オーバーレイ。
5. **タイトル → 役選択**（着手済み）: `TitleMenu` → **裏で始める / 表で始める**（1/2）で縦スライス開始。
6. **ネットワーク**：上記が固まってから Netcode（裏 SoT）。事前に **Multiplayer Play Mode** で仮想2クライアント検証可。

詳細マイルストーンは Cursor Project Context の **`docs/unity-production-plan.md`** を正本とします。リポジトリ内要約は `Docs/DESIGN_LINKS.md` を参照。

## 設計書

ゲームデザインの正本は **Cursor Project Context**（KG-2〜5 等）。リポジトリにはリンクとサマリのみ配置します（全文コピーなし）。

## ProjectSettings について

`ProjectSettings/` は Unity 6 URP テンプレート由来の **最小セット** を同梱しています。エディタ版が `ProjectVersion.txt` より新しい場合、**初回オープンで自動更新** される項目があります。問題なければその差分をコミットしてください。
