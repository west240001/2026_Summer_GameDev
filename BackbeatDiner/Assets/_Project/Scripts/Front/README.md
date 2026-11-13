# Front（表プレイヤー）

**KG-5** — 注文キュー、調理MG、配膳リズム S1（F1+）、客タイムアウト。

| Script | Role |
|--------|------|
| `FrontShellBootstrap` | Week2 表シーンのランタイム組み立て |
| `FrontHudController` | 日本語 HUD：客待ち / 在庫 / クォータ / **Beat CD**（`BeatLexiconDemo` 連動可） |
| `DummyCustomerQueue` | ローポリ客 1〜3＋タイムアウトバー（`TryServeOldest` 対応） |
| `InventoryBar` | 偽在庫カテゴリ（teal / gold） |
| `InstantCookInput` | Instant Cook → `ServeRhythmWindow`。solo: F/Enter；縦スライス2P: **Enter** のみ |
| `ServeRhythmWindow` | **F1+ / S1**：dsp 拍判定。solo: Space/J·K；縦スライス2P: **J·K**（裏ヒット継続）。開いている間 `FrontCommsSendLocked`（表ビートのみ） |

シーン: `Assets/_Project/Scenes/FrontShellPrototype.unity` / 縦スライスは `VerticalSlicePrototype.unity`  
メニュー: **BACKBEAT DINER → Build Front Shell Prototype Scene** / **Build Vertical Slice Prototype Scene**

設計サマリ: `Docs/DESIGN_LINKS.md` / Project Context `docs/kg-5-front-cooking-customers.md`
