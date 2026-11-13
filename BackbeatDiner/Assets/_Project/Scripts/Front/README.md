# Front（表プレイヤー）

**KG-5** — 注文キュー、調理MG、配膳リズム S1、客タイムアウト。

| Script | Role |
|--------|------|
| `FrontShellBootstrap` | Week2 表シーンのランタイム組み立て |
| `FrontHudController` | 日本語 HUD：客待ち / 在庫 / クォータ / CD |
| `DummyCustomerQueue` | ローポリ客 1〜3＋タイムアウトバー（表示のみ） |
| `InventoryBar` | 偽在庫カテゴリ（teal / gold） |

シーン: `Assets/_Project/Scenes/FrontShellPrototype.unity`  
メニュー: **BACKBEAT DINER → Build Front Shell Prototype Scene**

設計サマリ: `Docs/DESIGN_LINKS.md` / Project Context `docs/kg-5-front-cooking-customers.md`
