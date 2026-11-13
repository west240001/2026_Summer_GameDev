# 設計リンク（Project Context サマリ）

正本は Cursor Project **Context**（`docs/`）。以下は実装時の索引と **要約のみ**（全文は Context を参照）。

## 全体

| ドキュメント | 役割 |
|--------------|------|
| `project-context.md` | ビジョン、2P非対称、音ゲー中心の前提 |

## KG-2 — コアループ・裏リズム（チャートプレイヤー）

- **1プレイ＝1営業日＝1セットリスト**。標準 **4チャート/日**（Intro / A / B / D）。
- **裏が主役**：フルチャートで密度の山（Chart B）と協力間隙（5〜15秒）。
- **表は下流**：調理MG＋ **配膳リズム S1**（短い単レーン、裏の10〜20%程度の入力量）。
- **勝利**：デイリー売上クォータのみ。配膳失敗はボーナス欠落、基本売上は維持。
- **実装アンカー**：`Assets/_Project/Scripts/Rhythm/` — 譜面時刻、dsp同期、判定窓、フェーズラベル。

## KG-3 — ビート・コミュニケーション

- **音声チャットなし**。意味の固定された **短いビートモチーフ**（C3 レキシコン）が主通信。
- **C1/C2**：資源スナップショット要求・返送（時間コスト付き表操作）。
- **受信は聴覚主**、HUDは最小（CD・種別バッジ）。Chart B 高密度・配膳ウィンドウ中は送信制限。
- **C3 レキシコン**：Beat-B1..B4／F1..F4。CD **8s**／同一モチーフ **20s**。配膳中は表送信不可（`FrontCommsSendLocked`）。
- **実装アンカー**：`Assets/_Project/Scripts/Coop/` — `BeatLexicon`（静的定義）、`BeatLexiconDemo`（パッド＋CD）、`BeatReceivePulse`。

## KG-5 — 表（フロント）・調理・客

- ループ：**注文 → 在庫確認 → 調理MG → 配膳 S1 → 売上計上**（提供完了時）。
- 調理MGは非譜面のタイミング系；**音ゲー貢献は配膳リズム**が中心。
- Chart B 中は大口・同時注文で表圧力上昇；需要は受動HUD（C4 提案）＋遅延。
- **実装アンカー**：`Assets/_Project/Scripts/Front/` — キュー、スロット、ダミー客、配膳UI殻。

## その他（参照）

| KG | 内容 |
|----|------|
| KG-4 | 裏資源・監視・5カテゴリ |
| KG-Meta / KG-Career | 日次コイン、キャリア・ストーリー |

## Unity 制作計画

Project Context: `docs/unity-production-plan.md` — パッケージ、フォルダマップ、週次プロト、**ネットワークは縦スライス後**。

縦スライス実装: `Assets/_Project/Scenes/VerticalSlicePrototype.unity`（裏1チャート＋表インスタントクック＋F1+ `ServeRhythmWindow`、共有仕込み/提供、**ローカル2P キー分割** `LocalTwoPlayerBindings`：裏 WASD+Space / A·S·D·F=B*、表 Enter / J·K / 矢印=F*、配膳中は表コミュロック）。セットリスト間隙は `ChartGapCoopWindow`（**スナップ推奨**／Beat-B3 ヒント、CD 解除ブースト、UI pulse）。
