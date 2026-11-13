# Back（裏プレイヤー）

**KG-2 / KG-4** — フルチャート、資源、ブラインド区間。

プロト順：レーンHUDダミー → 譜面1曲分のタイムライン読み込み枠。

縦スライスでは `RhythmClock` / `NoteHighway` / `HitInput` を `VerticalSlicePrototype` 左側で再利用。ローカル2P: **WASD+Space**=ヒット、**A/S/D/F**=Beat-B1..B4。Perfect/Good が共有 `DaySessionState` の **仕込み** を増やす。

| Script | Role |
|--------|------|
| `SurveillanceThumbnail` | 表 `FrontCamera` の **監視** RawImage（約 **2Hz**）。`NoteHighway.HasActiveNotes` 中は最終フレーム保持＋ **BLIND** オーバーレイ。日本語ラベル「監視」 |

設計サマリ: `Docs/DESIGN_LINKS.md` / Project Context `docs/kg-2-core-loop.md`, `docs/kg-4-back-rhythm-resources.md`
