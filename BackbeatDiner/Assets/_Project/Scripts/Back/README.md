# Back（裏プレイヤー）

**KG-2 / KG-4** — フルチャート、資源、ブラインド区間。

プロト順：レーンHUDダミー → 譜面1曲分のタイムライン読み込み枠。

縦スライスでは `RhythmClock` / `NoteHighway` / `HitInput` を `VerticalSlicePrototype` 左側で再利用。Perfect/Good が共有 `DaySessionState` の **仕込み** を増やす。

設計サマリ: `Docs/DESIGN_LINKS.md` / Project Context `docs/kg-2-core-loop.md`, `docs/kg-4-back-rhythm-resources.md`
