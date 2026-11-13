# Front（表プレイヤー）

**KG-5** — 注文キュー、調理MG、配膳リズム S1、客タイムアウト。

| Script | Role |
|--------|------|
| `FrontShellBootstrap` | Week2 表シーンのランタイム組み立て |
| `FrontHudController` | 日本語 HUD：客待ち / 在庫 / クォータ / CD |
| `DummyCustomerQueue` | ローポリ客 1〜3＋タイムアウトバー（`TryServeOldest` 対応） |
| `InventoryBar` | 偽在庫カテゴリ（teal / gold） |
| `InstantCookInput` | v0 一ボタン提供（F / Enter / West）— 仕込み消費 |

シーン: `Assets/_Project/Scenes/FrontShellPrototype.unity` / 縦スライスは `VerticalSlicePrototype.unity`  
メニュー: **BACKBEAT DINER → Build Front Shell Prototype Scene**

設計サマリ: `Docs/DESIGN_LINKS.md` / Project Context `docs/kg-5-front-cooking-customers.md`
