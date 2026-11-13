# Coop（KG-3）

| Script | Role |
|--------|------|
| `BeatLexicon` | Static defs **Beat-B1..B4 / Beat-F1..F4**（日本語ラベル・SE 周波数）。CD 定数 **8s** any / **20s** same-motif |
| `BeatLexiconDemo` | Keys **1–4** → role pads（裏=B* / 表=F*）。日本語コミュパッド UI。**Blocked** while `ServeRhythmWindow.FrontCommsSendLocked`。`MotifSent` / `BeatSent` |
| `BeatReceivePulse` | Edge pulse + optional waveform strip on motif / simulated back beat (**B** / auto); Japanese toast tutorial-only |

## Pad labels（v1）

| Key | 裏 (Back) | 表 (Front) |
|-----|-----------|------------|
| 1 | プッシュ (B1) | ビジー (F1) |
| 2 | クリア (B2) | ビッグオーダー (F2) |
| 3 | スナップ前 (B3) | 在庫赤 (F3) |
| 4 | あとで (B4) | ラスト客 (F4) |
