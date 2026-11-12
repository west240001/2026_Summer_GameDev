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
5. スキャフォールド確認のみなら **`Assets/Scenes/Bootstrap.unity`** を **Play**。Console に `[BACKBEAT DINER] Bootstrap ready` が出れば OK（`GameBootstrap` は `DontDestroyOnLoad` シェル）。

## Week 1 — Rhythm tick プロト（操作）

| 操作 | 内容 |
|------|------|
| **Space** または **Gamepad South (A)** | ヒット判定（Perfect ±40ms / Good ±80ms）。画面に **判定 / コンボ / BPM**、Console にもログ |
| **1 / 2 / 3 / 4** | KG-3 ビートコミュ用プレースホルダ SE（手続き生成モチーフ） |
| メトロノーム | `Assets/_Project/Audio/MetronomeClick.wav`（手続き PCM・ライセンスは同フォルダ `AUDIO_LICENSE.md`） |

シーンが未生成のとき: Unity メニュー **BACKBEAT DINER → Build Rhythm Prototype Scene** で `RhythmPrototype.unity` を作成し Build Settings 先頭に登録します。エディタがプロジェクトをロックしている場合は **Assets → Refresh** 後に再 Play してください。

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
2. **Week 2 — 非対称 HUD 殻**：裏＝レーンUIダミー、表＝客1人表示（操作なし）。
3. **ビート送信プロト**：C3 モチーフ1種の SE + 相手側パルス（KG-3）— キー1–4 の SE は仮置き済み。
4. **1日縦切り**：4チャートなしで「1曲＋表ミニMG」まで。
5. **ネットワーク**：上記が固まってから Netcode（裏 SoT）。事前に **Multiplayer Play Mode** で仮想2クライアント検証可。

詳細マイルストーンは Cursor Project Context の **`docs/unity-production-plan.md`** を正本とします。リポジトリ内要約は `Docs/DESIGN_LINKS.md` を参照。

## 設計書

ゲームデザインの正本は **Cursor Project Context**（KG-2〜5 等）。リポジトリにはリンクとサマリのみ配置します（全文コピーなし）。

## ProjectSettings について

`ProjectSettings/` は Unity 6 URP テンプレート由来の **最小セット** を同梱しています。エディタ版が `ProjectVersion.txt` より新しい場合、**初回オープンで自動更新** される項目があります。問題なければその差分をコミットしてください。
