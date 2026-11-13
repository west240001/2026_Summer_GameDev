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
4. **Week 1 リズム試作**: `Assets/_Project/Scenes/RhythmPrototype.unity` を開き **Play**（無い場合はメニュー **BACKBEAT DINER → Build Rhythm Prototype Scene**、または batchmode で `RhythmPrototypeSceneBuilder.BuildFromCommandLine`）。
5. **Week 2 表 HUD 殻**: `Assets/_Project/Scenes/FrontShellPrototype.unity` を開き **Play**（無い場合はメニュー **BACKBEAT DINER → Build Front Shell Prototype Scene**、または batchmode で `FrontShellPrototypeSceneBuilder.BuildFromCommandLine`）。
6. **縦スライス（1チャート＋表ミニ）**: `Assets/_Project/Scenes/VerticalSlicePrototype.unity` を開き **Play**（無い場合はメニュー **BACKBEAT DINER → Build Vertical Slice Prototype Scene**、または batchmode で `VerticalSlicePrototypeSceneBuilder.BuildFromCommandLine`）。
7. スキャフォールド確認のみなら **`Assets/Scenes/Bootstrap.unity`** を **Play**。Console に `[BACKBEAT DINER] Bootstrap ready` が出れば OK（`GameBootstrap` は `DontDestroyOnLoad` シェル）。

## Week 1 — Rhythm tick プロト（操作）

| 操作 | 内容 |
|------|------|
| **Space** または **Gamepad South (A)** | ヒット判定（Perfect ±40ms / Good ±80ms）。画面に **判定 / コンボ / BPM**、Console にもログ |
| **1 / 2 / 3 / 4** | KG-3 ビートコミュ用プレースホルダ SE（手続き生成モチーフ） |
| メトロノーム | `Assets/_Project/Audio/MetronomeClick.wav`（手続き PCM・ライセンスは同フォルダ `AUDIO_LICENSE.md`） |

シーンが未生成のとき: Unity メニュー **BACKBEAT DINER → Build Rhythm Prototype Scene** で `RhythmPrototype.unity` を作成し Build Settings 先頭に登録します。エディタがプロジェクトをロックしている場合は **Assets → Refresh** 後に再 Play してください。

## Week 2 — Front HUD shell プロト（操作）

| 操作 | 内容 |
|------|------|
| （表示のみ） | ローポリ客 1〜3 人＋タイムアウトバー、日本語 HUD（**客待ち / 在庫 / クォータ / CD**） |
| **1 / 2 / 3 / 4** | ビート送信 SE → 表側 **エッジパルス**＋波形ストリップ（a11y） |
| **B** | 裏ビート受信のシミュレート（自動パルスもあり） |
| 初回のみ | 日本語チュートリアルトースト「裏からのビートを受信しました」 |

ブランド色マテリアル: `Assets/_Project/Art/Materials/BrandGold.mat`（`#C9A227`）/ `BrandTeal.mat`（`#1A6B6B`）。再生成は **BACKBEAT DINER → Build Front Shell Prototype Scene**。

## 縦スライス — 1チャート＋表ミニ（操作）

左右スプリット（左＝裏キッチン / 右＝表ダイナー）。共有資源は **仕込み**（裏ヒットで増加）と **提供**（表インスタントクック → **配膳リズム F1+** で消費）。

| 操作 | 内容 |
|------|------|
| **Space** または **Gamepad South (A)** | 裏レーンヒット（Perfect/Good → 仕込み +2/+1）。**配膳ウィンドウ中は無効**（Space は配膳用） |
| **F** / **Enter** または **Gamepad West (X)** | インスタントクック → **配膳リズム**（1〜2小節）を開く |
| **Space** / **J·K**（配膳中） | S1 拍合わせ。Perfect チップ×1.5 / Good×1 / Miss＝基本売上のみ（チップなし） |
| **1 / 2 / 3 / 4** | ビートコミュ送信（**配膳中はロック** — `FrontCommsSendLocked`） |
| 終了 | 約 **32拍** または **30秒** で結果オーバーレイ（**売上仮点数**） |
| HUD | 仕込み / 提供 / **配膳リズム** / 残り時間・ビート、在庫バー（表） |

シーン: `Assets/_Project/Scenes/VerticalSlicePrototype.unity`  
再生成: **BACKBEAT DINER → Build Vertical Slice Prototype Scene**（`DaySessionState` / `VerticalSliceBootstrap` / `InstantCookInput` / `ServeRhythmWindow`）。

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

1. **Week 1 — リズム tick**（着手済み）: `RhythmClock` + メトロノーム WAV + `HitInput` / `NoteHighway` / `BeatLexiconDemo`。シーン `RhythmPrototype`。
2. **Week 2 — 非対称 HUD 殻**（着手済み）: 表シーン `FrontShellPrototype` — `FrontHudController` / `DummyCustomerQueue` / `InventoryBar` / `BeatReceivePulse`。裏レーンは Week1 `RhythmPrototype` を併用。
3. **ビート送信プロト**：C3 モチーフ1種の SE + 相手側パルス（KG-3）— キー1–4 の SE と受信パルスは仮置き済み。
4. **1日縦切り**（着手済み）: `VerticalSlicePrototype` — 裏1チャート＋表インスタントクック＋**F1+ 配膳リズム**、共有 `DaySessionState`（仕込み/提供）、結果オーバーレイ。
5. **ネットワーク**：上記が固まってから Netcode（裏 SoT）。事前に **Multiplayer Play Mode** で仮想2クライアント検証可。

詳細マイルストーンは Cursor Project Context の **`docs/unity-production-plan.md`** を正本とします。リポジトリ内要約は `Docs/DESIGN_LINKS.md` を参照。

## 設計書

ゲームデザインの正本は **Cursor Project Context**（KG-2〜5 等）。リポジトリにはリンクとサマリのみ配置します（全文コピーなし）。

## ProjectSettings について

`ProjectSettings/` は Unity 6 URP テンプレート由来の **最小セット** を同梱しています。エディタ版が `ProjectVersion.txt` より新しい場合、**初回オープンで自動更新** される項目があります。問題なければその差分をコミットしてください。
